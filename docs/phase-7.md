# Phase 7 — Homework & Assignments

## What is homework?

A task assigned by a teacher to students in a subject and grade — a title, instructions,
and a due date. Creating homework does not require any student action; it simply
becomes available for students to see.

## What is a submission?

A student's response to a specific homework assignment — comments/answer text, and a
timestamp recording when it was sent. One homework assignment can be answered by many
students, so submissions are stored separately from the homework itself.

## Why are Homework and HomeworkSubmission separate?

"Algebra Practice" is created **once** by a teacher. But:

```
Ananya  → submitted
Rahul   → submitted
Aisha   → no submission
Arjun   → submitted
```

are up to one submission **per student**. Storing them together would mean repeating
the homework's title, subject, and due date on every student's row — Homework describes
the task; HomeworkSubmission describes one student's response to it.

```
Teacher
   |
   | creates
   v
Homework
   |
   | belongs to
   v
Subject / Grade

Student
   |
   | submits
   v
HomeworkSubmission
   |
   | belongs to
   v
Homework
```

## Why is "Not Submitted" never stored?

If every student got a `HomeworkSubmission` row the moment homework was created (with a
status of "Not Submitted"), the database would need to know about a submission before
the student did anything — and every new student added to the class would silently need
retroactive rows for every existing homework assignment. Instead, "Not Submitted" is
**derived**: the API takes the class roster (students in that homework's grade) and
combines it with whatever submissions actually exist. A student with no matching
`HomeworkSubmission` row is reported as "Not Submitted" — no row needs to exist for that
to be true.

## What is a workflow?

```
Create
 ↓
Available (created homework is immediately visible — no separate "publish" step in this phase)
 ↓
Student submits
 ↓
Teacher reviews
```

## What is business logic? (late submission)

The due date and the submission timestamp are both known to the system, so **whether a
submission is late is calculated, not asked for**:

```
SubmittedDate <= DueDate  → Submitted
SubmittedDate >  DueDate  → Late
```

The server computes this at the moment a submission is created — Angular never sends a
status, and the server never trusts a client-supplied timestamp either:

```
Student clicks Submit
        ↓
API receives request
        ↓
Backend records current date/time (DateTime.UtcNow)
        ↓
Backend compares it to Homework.DueDate
        ↓
Status = Submitted or Late
```

## What is structured data?

```
Student
Subject
DueDate
Status
```

Rows and columns with a fixed shape, stored and queried through SQL Server via EF Core —
everything built in Phases 4-7 so far.

## What is a file?

```
PDF
Word document
Image
```

Unstructured content with no fixed schema. Phase 7's homework submission is
**text/comments only, on purpose** — no file upload yet.

## The next storage problem (preview, not implemented)

```
Student submits homework
        |
        +---- Structured information ---- SQL Database
        |
        +---- Document/file ------------- ???
```

A relational database is built for structured, tabular data. A student's uploaded PDF or
photo doesn't fit that shape well — large binary content in SQL Server is possible but
usually the wrong tool. That mismatch is the reason **object/file storage** exists as its
own category of service, and why the next phase's "Learning Materials" feature will
introduce it — eventually mapped to **Azure Blob Storage**. Nothing about file storage
is implemented in Phase 7.

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
```

New migration: `AddHomework`. No existing data was touched.

### Relationships

```
Subject
   |
   | 1
   | many
   v
Homework
   ^
   | many
   | 1
   |
Grade

Teacher
   |
   | 1
   | many
   v
Homework
```

```
Homework
    |
    | 1
    | many
    v
HomeworkSubmission
    ^
    | many
    | 1
    |
Student
```

## API endpoints

```
GET    /api/homework
GET    /api/homework/{id}
GET    /api/homework/student/{studentId}
GET    /api/homework/teacher/{teacherId}
GET    /api/homework/subject/{subjectId}
POST   /api/homework
PUT    /api/homework/{id}
DELETE /api/homework/{id}

GET    /api/homework-submissions/{id}
GET    /api/homework-submissions/homework/{homeworkId}
GET    /api/homework-submissions/student/{studentId}
POST   /api/homework-submissions
PUT    /api/homework-submissions/{id}
```

## Homework workflow

```
Teacher
 ↓
Create Homework
 ↓
API
 ↓
Database
 ↓
Student sees Homework
 ↓
Student submits
 ↓
API validates
 ↓
Database
 ↓
Teacher sees Submission
```

## Business rules

- `Title` is required; `Subject`, `Grade`, and `Teacher` must exist (`400` otherwise).
- A student can only have one submission per homework — enforced by both the API
  (`409 Conflict`) and a database unique index on `(StudentId, HomeworkId)`.
- Submission timestamp and Submitted/Late status are computed **on the server**, never
  accepted from the client.
- `PUT /api/homework/{id}` only allows correcting `Title`, `Description`, and `DueDate` —
  the `Subject`/`Grade`/`Teacher` relationships are fixed after creation, since changing
  them would silently invalidate the meaning of any existing submissions.
- `DELETE /api/homework/{id}` removes its submissions along with it (`Cascade` on that
  foreign key) — a submission has no independent meaning once the assignment it answers
  no longer exists. This was chosen over blocking deletion to keep the teacher workflow
  simple; it's a deliberate, documented trade-off for this phase.

## Angular components/services

- `HomeworkService` / `HomeworkSubmissionService` — all HTTP calls live here, never in
  a component or template directly.
- `StudentHomeworkList` (`/students/:id/homework`) — "My Homework" list with status
  badges (Not Submitted / Submitted / Late).
- `HomeworkDetail` (`/students/:id/homework/:homeworkId`) — instructions + submission
  form, or the existing submission's details if already submitted.
- `TeacherHomework` (`/teacher-homework`) — homework created by a fixed demo teacher
  (`DEMO_TEACHER_ID`), clearly labeled as temporary/unauthenticated.
- `HomeworkSubmissions` (`/homework/:id/submissions`) — the derived roster view showing
  every student in the homework's grade and their status.
- `CreateHomework` (`/create-homework`) — teacher-style creation form.
- `StudentDetail`'s Homework panel now shows real assignment titles and statuses.

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

- `GET /api/homework` → 3 seeded assignments.
- `GET /api/homework/student/1` → Algebra Practice "Submitted", States of Matter "Late",
  Grammar Exercise "Not Submitted".
- `GET /api/homework/student/3` → all three "Not Submitted" (Aisha has no submissions).
- `GET /api/homework-submissions/homework/1` → roster of all 4 Grade 8A students with
  derived statuses.
- `POST /api/homework-submissions` for a student/homework pair that already has one →
  `409`.
- Visit `/students/1/homework` → status badges; open an item → instructions + submission
  form or existing submission details.
- Visit `/teacher-homework` → `/create-homework` → create a homework → appears in the list.
- Visit `/homework/{id}/submissions` → roster with Submitted/Late/Not Submitted.
- Restart the API → homework and submissions are still there.

## Student learning summary

- Homework is a task created by a teacher; a submission is a student's response to it.
- One homework assignment can have many submissions; one student can submit many
  assignments (one submission each).
- The backend enforces business rules (duplicate submissions, valid relationships).
- Not every piece of information needs its own database record — "Not Submitted" is
  calculated from the **absence** of a submission row, not stored as one.
- Some information (Submitted vs. Late) is calculated from other data (`SubmittedDate`
  vs. `DueDate`), and that calculation belongs on the server.
- Structured application data (this phase) and documents/files (a future phase) are
  different storage problems with different solutions.

```
Teacher
   ↓
Homework
   ↓
Student
   ↓
Submission
```
```
Structured data   → SQL Database
Files/documents   → Future Object Storage
```

## Next phase

Phase 8 introduces **Learning Materials and File Storage** — teachers uploading PDFs,
worksheets, and images. The architecture will grow a second storage path alongside SQL:

```
Angular
   ↓
C# API
   ├──────────────→ SQL Server (structured data)
   │
   └──────────────→ Object/File Storage (documents)
```

This will eventually map to **Azure Blob Storage**, but Phase 8 will first demonstrate
the general file-storage concept locally before introducing the Azure service.
