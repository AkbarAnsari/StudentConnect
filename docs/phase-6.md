# Phase 6 — Marks & Assessments

## What is an assessment?

A test, exam, quiz, assignment, or practical — anything a teacher uses to evaluate
students in a subject. An `Assessment` describes **the evaluation itself**: its title,
subject, date, and maximum marks. It says nothing about how any particular student did.

## What is a mark?

A `Mark` is the score **one student** received for **one assessment**. The same
assessment ("Unit Test 1") produces many marks — one per student who took it.

## Why are Assessment and Mark separate?

```
Unit Test 1
```
is one assessment — created once by a teacher.

```
Ananya → 87
Rahul → 72
Aisha → 94
```
are multiple marks for that same assessment — one row per student. Combining these into
a single "StudentMarks" table would mean repeating the assessment's title, subject, date,
and maximum marks on every single row — and if the teacher corrected the assessment's
date, every row would need updating instead of just one.

```
Student
   |
   | receives
   v
Mark
   |
   | belongs to
   v
Assessment
   |
   | belongs to
   v
Subject
```

## What is a relationship?

```
Assessment → Marks     (one assessment, many students' marks)
Student    → Marks     (one student, many marks across many assessments)
Subject    → Assessment (one subject, many assessments over time)
```

A `Mark` is the junction point: it belongs to exactly one student and exactly one
assessment.

## What is a business rule?

> A student cannot score more than the maximum marks for an assessment.

And:

> A student should have only one mark for a particular assessment.

Both rules are enforced by the API before saving, **and** the second rule is backed by a
database unique index on `(StudentId, AssessmentId)` — so it can never be violated even
by a bug elsewhere in the code.

## What is validation, and why both frontend and backend?

Angular validates immediately so a teacher gets instant feedback (e.g. "marks cannot be
greater than 100") without waiting for a network round-trip. But the API validates the
**same rules again**, because:

- A browser's JavaScript can be bypassed, disabled, or tampered with.
- Other clients (a future mobile app, a script, Postman) might call the API directly.
- The database is the last line of defense — it doesn't trust any application code.

```
Frontend validation + Backend validation + Database constraints
```
together provide much stronger protection than any single layer alone.

## Database changes

The database now contains:

```
Grades
Students
Subjects
StudentSubjects
Attendance
Assessments
Marks
```

New migration: `AddMarksAndAssessments`. No existing data was touched — this migration
only *adds* two new tables.

### Relationships

```
Grade
  |
  | 1
  | many
  v
Student
  |
  | 1
  | many
  v
Mark
  ^
  | many
  | 1
Assessment
  ^
  |
  | many
  | 1
Subject
```

```
Student
   ↕
StudentSubject
   ↕
Subject
```

`StudentSubject` (introduced in Phase 5) still represents which subjects a student
studies — unchanged in this phase.

## API endpoints

```
GET    /api/assessments
GET    /api/assessments/{id}
GET    /api/assessments/subject/{subjectId}
POST   /api/assessments
PUT    /api/assessments/{id}
DELETE /api/assessments/{id}

GET    /api/marks/student/{studentId}
GET    /api/marks/student/{studentId}/subject/{subjectId}
GET    /api/marks/student/{studentId}/summary
GET    /api/marks/assessment/{assessmentId}
POST   /api/marks
POST   /api/marks/bulk
PUT    /api/marks/{id}
DELETE /api/marks/{id}
```

## Percentage and result category

```
percentage = totalMarksObtained / totalMaximumMarks × 100
```

Calculated **on the backend** (`GET /api/marks/student/{id}/summary`), rounded to the
nearest whole number, and guarded against division by zero (0 assessments → 0%, not a
crash). Angular only displays the number it's given.

A simple result label is added alongside the percentage:

```
90–100 → Excellent
75–89  → Very Good
60–74  → Good
40–59  → Needs Improvement
Below 40 → Needs Support
```

This lives in exactly one place (`ResultCategory.From(...)` in the API) instead of being
scattered as magic numbers, and is explicitly a **demonstration**, not an official
academic grading system.

## Data vs. information

Raw data:

```
Ananya
87
Unit Test 1
Mathematics
```

Information:

```
Ananya scored 87% in Mathematics Unit Test 1.
```

Summary information:

```
Mathematics average: 89%
```

Applications transform stored data into useful information — the same idea introduced
with attendance percentages in Phase 5, now applied to marks.

## Querying related data

The API combines information from three tables — `Marks`, `Assessments`, `Subjects` — into
one useful response shape:

Database:
```
Marks
   ↓
Assessment
   ↓
Subject
```

API response (`MarkRecordDto`):
```json
{
  "id": 1,
  "assessmentId": 1,
  "assessmentTitle": "Unit Test 1",
  "subjectId": 1,
  "subjectName": "Mathematics",
  "assessmentDate": "2026-09-15",
  "marksObtained": 87,
  "maximumMarks": 100
}
```

Angular never needs to know the database stores these as three separate tables joined
together.

## Architecture

```
                         USER
                           |
                           v
                       BROWSER
                           |
                           v
                    ANGULAR FRONTEND
                           |
                           | HTTP
                           v
                 ASP.NET CORE WEB API
                           |
                     Business Logic
                           |
                           v
                 ENTITY FRAMEWORK CORE
                           |
                           | SQL
                           v
                     SQL SERVER
                           |
          +----------------+----------------+
          |                |                |
          v                v                v
       Students         Subjects       Assessments
                                             |
                                             v
                                           Marks
```

## Request flows

### Student viewing marks

```
Student opens Marks page
        ↓
Angular component loads
        ↓
MarksService
        ↓
HTTP GET
        ↓
ASP.NET Core API
        ↓
Marks controller
        ↓
EF Core
        ↓
SQL Server
        ↓
Marks + Assessment + Subject data
        ↓
C# DTO
        ↓
JSON response
        ↓
Angular
        ↓
Student sees marks
```

### Teacher entering marks

```
Teacher selects subject → assessment
        ↓
Angular loads students
        ↓
Teacher enters marks
        ↓
Angular validates input
        ↓
HTTP POST /api/marks/bulk
        ↓
ASP.NET Core API
        ↓
Backend validation
        ↓
EF Core
        ↓
SQL Server
        ↓
Marks stored
```

Then a student's next `GET` sees the same data — one user's action becomes another
user's information. The application is now clearly multi-user.

## Business rules enforced

- `MaximumMarks` must be greater than zero (API validation + DB check constraint).
- `MarksObtained` must be `>= 0` and `<= MaximumMarks` (API validation + DB check
  constraint for the `>= 0` half — the `<= MaximumMarks` half requires comparing two
  tables, so it's enforced in the API).
- The referenced student, subject, and assessment must exist (`400 Bad Request` if not).
- A student can only have one mark per assessment (`409 Conflict` on the single-create
  endpoint; the bulk endpoint upserts instead of rejecting, matching how a teacher
  actually corrects a class's marks).
- Deleting an assessment that already has marks against it fails with `409 Conflict`
  rather than silently orphaning or cascading.

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

- `GET /api/assessments` → 4 seeded assessments.
- `GET /api/marks/student/1/summary` → Mathematics 89%, Science 84%, English 92%.
- `POST /api/marks` with `marksObtained` greater than the assessment's maximum → `400`.
- `POST /api/marks` for a student/assessment pair that already has a mark → `409`.
- `POST /api/marks/bulk` for a whole class → `200` with a count.
- Visit `/students/1` → Marks panel shows real per-subject percentages → "View Marks".
- Visit `/students/1/marks` → full summary table with Overall row and result labels.
- Click a subject → `/students/1/marks/{subjectId}` → assessment/score list.
- Visit `/assessments` → list grouped by subject; `/assessments/add` → create one.
- Visit `/enter-marks` → select subject + assessment → enter marks → Save → success message.
- Restart the API → assessments and marks are still there.

## Student learning summary

- An assessment is the thing being evaluated; a mark is a student's result for it.
- One assessment can have marks for many students; one student can have many marks.
- A mark belongs to both a student and an assessment; the assessment belongs to a subject.
- Business rules prevent invalid data (marks out of range, duplicates).
- The API validates data before storing it; the database enforces the same rules as a
  safety net.
- The API calculates percentages and result categories — Angular just displays them.

```
Assessment
   ↓
Student receives Mark
   ↓
Database stores Mark
   ↓
API retrieves Mark
   ↓
Angular displays Result
```

## Next phase

Phase 7 introduces **Homework & Assignments**:

```
Student
   |
   +── Attendance
   |
   +── Marks
   |
   +── Homework
           |
           ├── Subject
           ├── Teacher
           ├── Due Date
           └── Description
```

This will introduce the distinction between **structured database information** (what
we've built so far) and **documents/files** — preparing the ground for object/file
storage and, eventually, Azure Blob Storage.
