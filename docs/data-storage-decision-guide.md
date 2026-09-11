# Data Storage Decision Guide

A simple, learning-focused decision guide — not an exhaustive Azure storage decision
tree. Use it to reason about where a *new* piece of StudentConnect data should live.

```
What kind of data are we storing?
             │
       ┌─────┴─────┐
       │           │
 Structured      File/Object
 (fields,        (PDF, DOCX,
 relationships,  image, video —
 queries)        binary content)
       │           │
       ▼           ▼
Relational      Object
Database        Storage
       │           │
       ▼           ▼
  PostgreSQL    Azure Blob
 (local or      Storage
  Azure Database
  for PostgreSQL)
```

## Worked examples from StudentConnect

| Data | Structured or file? | Where it lives |
|---|---|---|
| Student record | Structured | PostgreSQL (`Students` table) |
| Attendance record | Structured | PostgreSQL (`Attendance` table) |
| A PDF worksheet a teacher uploads | File/object | Azure Blob Storage (or local disk in dev) |
| The PDF's title, subject, uploader, size | Structured (metadata *about* the file) | PostgreSQL (`LearningMaterials` table) |
| Activity registration | Structured | PostgreSQL (`ActivityRegistrations` table) |

## Questions to ask

1. **Does this data have a consistent shape with fields and relationships to other
   data?** → Relational database.
2. **Is this fundamentally a binary file (image, document, video) that's
   uploaded/downloaded as a whole?** → Object storage.
3. **Do I need to query, filter, join, or run transactions against this data?**
   → Relational database. (You generally can't `WHERE` inside the bytes of a PDF.)
4. **Am I about to store a file's bytes as a column in a database table "because it's
   easier right now"?** → Stop and reconsider — the metadata belongs in the database;
   the bytes belong in object storage.

This is a learning simplification. Real systems occasionally have legitimate reasons to
store small binary blobs in a database (e.g. thumbnails), but StudentConnect's actual
learning materials (PDFs, DOCX, images) are large enough and accessed in a way — whole
file upload/download — that object storage is clearly the better fit.
