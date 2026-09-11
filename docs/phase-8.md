# Phase 8 — Learning Materials & File Storage

## The problem

Teachers want to share worksheets, notes, and practice papers — Mathematics worksheets,
Science notes, PDFs, images. Should these files go straight into SQL Server, the same
way Students, Marks, and Homework do?

## What is a file?

```
PDF
DOCX
PNG
JPG/JPEG
```

Unstructured binary content with no fixed set of "columns" — unlike a `Student` row,
a PDF doesn't have a predictable schema Learning materials introduce **file data**
alongside the **structured data** built in every earlier phase.

## What is structured data?

```
Student
Subject
Marks
Attendance
```

Rows and columns with a known shape, queried with SQL through EF Core.

## Why not store everything in one place?

A relational database is optimized for structured, tabular data — fast lookups,
relationships, constraints. Large binary files stored directly inside SQL Server rows
work technically, but bloat the database, slow backups, and aren't what the database
engine is designed for. The better split:

```
                 Learning Material
                       |
             +---------+---------+
             |                   |
             v                   v
        File metadata         Actual file
             |                   |
             v                   v
        SQL Database       File Storage
```

> The database tells us **about** the file. The file storage contains the file.

## What is metadata?

Information describing a file, without being the file itself:

```
Title
FileName
FileSize
ContentType
UploadedDate
```

For `Algebra Practice.pdf`, SQL Server stores the title, subject, grade, original file
name, size, and content type. The actual PDF bytes live in file storage, referenced by
a generated `StoredFileName` — never the client's original file name (see "File
naming" below).

## What is object storage?

A storage service designed for arbitrary files/blobs — identified by a name/key rather
than organized into rows and columns like a database table. Phase 8 implements this
concept with plain local disk storage; the *interface* used to talk to it is what will
later let this be swapped for a managed object storage service.

## Local architecture

```
Angular
   |
   | HTTP
   v
ASP.NET Core API
   |
   +------------------------+
   |                        |
   v                        v
SQL Server             Local File Storage
   |                        |
   v                        v
Metadata                 Actual files
```

```
database
   |
   └── LearningMaterials
           |
           ├── Title
           ├── SubjectId
           ├── GradeId
           ├── TeacherId
           ├── FileName          (original name, e.g. "Algebra Practice.pdf")
           ├── StoredFileName    (generated, e.g. "8f7c2a1e....pdf")
           ├── ContentType
           ├── FileSize
           └── UploadedDate

file storage
   |
   └── storage/learning-materials/
          |
          ├── 8f7c2a1e-....pdf
          ├── 3b91cd02-....docx
          └── 5a02ef14-....png
```

## File naming

Two teachers could both upload `worksheet.pdf`. If the server saved both using that
literal name, the second upload would silently overwrite the first. Instead, every
upload gets a server-generated unique `StoredFileName` (a GUID + the original
extension) — the *original* name is kept separately in the `FileName` column purely so
downloads can present it back to the user.

## Database changes

```
Grades
Students
Teachers
Subjects
StudentSubjects
Attendance
Assessments
Marks
Homework
HomeworkSubmissions
LearningMaterials
```

New migration: `AddLearningMaterials`. Foreign keys to `Subjects`, `Grades`, and
`Teachers` (all `Restrict` on delete, consistent with every other phase). A unique
index on `StoredFileName` is a defensive database-level guarantee against name
collisions, on top of the GUID generation already making them astronomically unlikely.

**No learning materials were seeded.** Seeding a metadata row with no matching physical
file would create a database record pointing to a file that doesn't exist — exactly
what this phase warns against. Materials only exist once uploaded through the API/UI.

## File storage structure

```
backend/StudentConnect.Api/storage/learning-materials/
```

Configured via `appsettings.json` → `FileStorage:RootPath` (relative to the API's
content root — never a machine-specific absolute path) and `FileStorage:MaxFileSizeBytes`
(default 10 MB, read from one place rather than hard-coded in multiple spots).

## Storage abstraction

```csharp
public interface IFileStorage
{
    Task SaveFileAsync(string storedFileName, Stream content, ...);
    Task<Stream?> GetFileAsync(string storedFileName, ...);
    Task DeleteFileAsync(string storedFileName, ...);
    Task<bool> FileExistsAsync(string storedFileName, ...);
}
```

```
IFileStorage
      |
      v
LocalFileStorage   (today — Storage/LocalFileStorage.cs)
```

Every controller talks only to `IFileStorage`, never to `System.IO` directly. Later:

```
IFileStorage
      |
      v
AzureBlobStorage   (future — not implemented yet)
```

swaps in without any change to `LearningMaterialsController`, the Angular app, or any
other business logic — that's the entire point of the abstraction.

## API endpoints

```
GET    /api/learning-materials?subjectId=&gradeId=
GET    /api/learning-materials/{id}
GET    /api/learning-materials/grade/{gradeId}
GET    /api/learning-materials/subject/{subjectId}
GET    /api/learning-materials/teacher/{teacherId}
GET    /api/learning-materials/student/{studentId}
POST   /api/learning-materials
GET    /api/learning-materials/{id}/download
DELETE /api/learning-materials/{id}
```

`PUT .../file` (replace-file) was intentionally **not** implemented — Upload, Download,
and Delete are the priority for this phase; adding replace would add complexity without
a clear learning benefit yet.

## Upload flow

```
Browser
   ↓
Angular (multipart/form-data: Title, Description, SubjectId, GradeId, TeacherId, File)
   ↓
ASP.NET Core API
   ↓
Validate metadata (title, subject/grade/teacher exist)
   ↓
Validate file (not empty, allowed extension, size limit, magic-byte signature)
   ↓
Generate unique StoredFileName
   ↓
Save file via IFileStorage
   ↓
Save metadata row in SQL Server
   ↓
Response (metadata only)
```

### Keeping file + metadata consistent

Two failure scenarios matter:

- **File saved, then the database save fails** → without cleanup this orphans a file
  with no matching record. The API deletes the just-saved file before returning an
  error, so the two stores never disagree.
- **Database save fails before any file is written** → nothing to clean up.

This is simple compensating cleanup, not a distributed transaction — appropriate for a
learning project with two independent stores that don't share a transaction coordinator.

## Download flow

```
Browser clicks "Download"
   ↓
Angular navigates to GET /api/learning-materials/{id}/download
   ↓
ASP.NET Core API looks up metadata
   ↓
IFileStorage retrieves the file by StoredFileName
   ↓
404 if the file is missing; otherwise stream the file back
   ↓
Browser downloads it, using the ORIGINAL FileName (not the internal GUID name)
```

Angular never fetches the file into memory just to hand it back to the browser — a
plain `<a href="...">` pointing at the download endpoint lets the browser handle the
download natively.

## Security considerations

- **Never trust the client's file name or path.** `StoredFileName` is always generated
  server-side (`Guid.NewGuid()` + extension) — the client's original name is stored only
  as descriptive metadata, never used to build a file system path.
- **No path traversal is possible.** Because stored names are always server-generated
  GUIDs, a value like `../../secrets.txt` can never reach the file system layer.
- **Restricted file types.** Only `.pdf`, `.docx`, `.png`, `.jpg`, `.jpeg` are accepted —
  checked by extension **and** by inspecting the first few bytes of the file (a "magic
  number" signature), since a client can freely lie about a file's extension or
  declared content-type.
- **Restricted file size**, configurable in one place (`FileStorage:MaxFileSizeBytes`).
- **Files are stored outside the application's source/publish directory**
  (`storage/learning-materials/`, not inside `wwwroot` or any executable path) and are
  never executed by the server.
- **Physical storage paths are never exposed** to Angular — only IDs and metadata.
- **Internal exceptions are logged server-side, never returned to the client** — the API
  responds with a generic "Unable to upload/download" message instead.

## Error handling

| Situation | Response |
|---|---|
| File missing on download | `404` "Learning material file is unavailable." |
| Unsupported file type | `400` "This file type is not supported." |
| File too large | `400` "File exceeds the maximum allowed size of 10 MB." |
| Upload/save failure | `500` "Unable to upload learning material." (technical error logged server-side) |

## Local storage limitation (and why Azure Blob Storage is next)

```
Developer Laptop
      |
      +── SQL Server
      |
      +── Local files (storage/learning-materials/)
```

This works for one machine. If StudentConnect is later deployed so the API runs on a
different machine (or multiple instances), files saved on `Machine A`'s disk simply
don't exist on `Machine B`. Local disk storage isn't durable or shareable the way a
cloud deployment needs. That's the bridge to **object storage** as a managed service —
and specifically, **Azure Blob Storage** for this application. Nothing Azure-related is
implemented yet; only the `IFileStorage` abstraction exists to make that swap possible
later without touching Angular or business logic.

## Future cloud architecture (conceptual only — not implemented)

```
                         INTERNET
                            |
                            v
                         BROWSER
                            |
                            v
                    Angular Application
                            |
                            v
                    ASP.NET Core API
                            |
              +-------------+-------------+
              |                           |
              v                           v
        Azure SQL Database        Azure Blob Storage
              |                           |
              v                           v
       Structured data              Files / Objects
```

- **Azure SQL Database** — the managed relational database capability.
- **Azure Blob Storage** — the managed object storage capability for files.

## Instructor demonstration

1. **Upload** `Algebra Practice.pdf` through the Upload Material form. Show the new row
   in the `LearningMaterials` table (title, subject, grade, file name, size, content
   type) — then show the actual file sitting in `storage/learning-materials/` on disk.
   The database stores information *about* the file; the file storage holds the file.
2. **Download** it from a student's Learning Materials page and confirm the browser
   saves it as `Algebra Practice.pdf`, not the internal GUID name.
3. **Delete** it and confirm both the database row and the physical file are gone.
4. **Restart the API**, then download the same (still-existing) file again — the file
   lived on disk the whole time, entirely independent of the running C# process.

## How to run

```bash
cd backend/StudentConnect.Api
dotnet run --launch-profile http   # applies pending migrations automatically
```
```bash
cd frontend
npm start
```

## How to test

- Upload a valid PDF/DOCX/PNG/JPG → `201 Created`.
- Upload an unsupported type (e.g. `.exe`) → `400`.
- Upload a file over 10 MB → `400`.
- Upload with an empty file, missing title, missing subject, or missing grade → `400`.
- `GET /api/learning-materials/{id}/download` for an existing material → file streams
  back with the original filename.
- Delete a material, then try to download it → `404`.
- Visit `/students/1/materials` → filter by subject → download a file.
- Visit `/teacher-materials` → `/upload-material` → upload → appears in the list →
  delete it.
- Restart the API, then download a previously-uploaded file — it's still there.

## Student learning summary

- A database stores structured application data; a file is different from a database
  record.
- File storage stores the actual document; the database stores metadata *about* it.
- The API controls access to both — the frontend never touches the file system or SQL
  Server directly.
- Uploaded files are treated as untrusted input: validated type, validated size, and
  given a server-generated name.
- Local file storage is fine for development but doesn't survive moving to a different
  machine — which is why cloud applications use a managed object storage service
  instead.

```
Structured Data → SQL Database
Files           → File/Object Storage
```
```
Angular
   ↓
C# API
   ↓
+----------------+
|                |
SQL            Files
```

## Next phase

Phase 9 introduces **Activities & Student Registration** — the school organizes
activities, and students discover and register for them:

```
Activity
   |
   +── Title
   +── Description
   +── Date
   +── Location
   +── Grade
   |
   v
ActivityRegistration
   |
   +── Student
   +── Activity
   +── RegistrationDate
   +── Status
```

This introduces many-to-many registration relationships, capacity limits, and status
business rules — continuing the pattern of building each new StudentConnect feature on
top of the same `Angular → C# API → SQL Database` architecture.
