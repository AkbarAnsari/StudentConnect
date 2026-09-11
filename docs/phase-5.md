# Phase 5 — Attendance Management

## What problem are we solving?

Schools need to know, for every class: who showed up, on what date, for which subject.
That's not just "a Student record" — it's information about an **event** that happened
in the real world. A real application stores far more than static objects; it stores
records of activities.

## What is business data?

**Simple explanation:** Data that describes something that happened, not just something
that exists.

**Analogy:** A student's name is a fact about them. "Ananya was present in Mathematics
on 11 September" is a record of an event.

**Technical explanation:** An `Attendance` row is business data — it's created by an
action (a teacher marking attendance) and has meaning tied to a specific date.

**StudentConnect example:** Each row in the `Attendance` table represents one student,
one subject, one date, one status.

## What is an entity?

```
Student
Subject
Attendance
```

- `Student` — who.
- `Subject` — what class.
- `Attendance` — the event connecting a student, a subject, a date, and a status.

## What is a relationship?

```
Student → Attendance
Subject → Attendance
```

One student can have many attendance records; one subject can have many attendance
records. Each attendance record points to exactly one student and exactly one subject.

Students and subjects also have their own many-to-many relationship (`StudentSubject`):
a student studies several subjects, and each subject is studied by many students.

## What is a business rule?

> A student should not have two attendance records for the same subject on the same date.

This is enforced in two places:
- The API checks for an existing record before creating a new one (`409 Conflict` if found).
- The database has a **unique index** on `(StudentId, SubjectId, Date)` — so even a bug
  in the API code couldn't create a duplicate row.

> Client-side validation improves the experience, but important business rules must also
> be enforced by the backend and the database.

## What is CRUD, for attendance?

```
Create  → POST   /api/attendance        (a teacher records a new entry)
Read    → GET    /api/attendance/...    (a student/teacher views records)
Update  → PUT    /api/attendance/{id}   (correct a mistake, e.g. Absent → Present)
Delete  → DELETE /api/attendance/{id}   (remove a record entirely)
```

## What is a summary?

Raw attendance rows are just individual events:

```
Student 1, Mathematics, 01 Sep, Present
Student 1, Mathematics, 02 Sep, Present
Student 1, Mathematics, 03 Sep, Absent
```

A **summary** turns many rows into one useful number:

```
Mathematics Attendance: 67%
```

This calculation (`present / totalClasses * 100`) happens on the **backend**
(`GET /api/attendance/student/{id}/summary`), not in Angular — Angular only displays
numbers the API already calculated. This is an important pattern: applications turn
stored data into useful information.

## What happens behind the scenes

**Teacher records attendance:**

```
Teacher
   ↓
Angular
   ↓
HTTP POST
   ↓
C# API
   ↓
Validation
   ↓
EF Core
   ↓
SQL Server
   ↓
Attendance saved
```

**Student views attendance:**

```
Student
   ↓
Angular
   ↓
HTTP GET
   ↓
C# API
   ↓
SQL query
   ↓
Attendance records
   ↓
Summary
   ↓
JSON
   ↓
Angular
   ↓
Student
```

## Architecture

```
                         STUDENT / TEACHER
                                |
                                v
                            BROWSER
                                |
                                v
                       ANGULAR FRONTEND
                                |
                              HTTP
                                |
                                v
                     ASP.NET CORE WEB API
                                |
                         Business Logic
                                |
                                v
                      ENTITY FRAMEWORK CORE
                                |
                               SQL
                                |
                                v
                         SQL SERVER
                                |
              +-----------------+----------------+
              |                 |                |
              v                 v                v
           Students          Subjects        Attendance
```

The frontend never talks to SQL Server directly — everything goes through the API.

## Database diagram

```
Grade
  |
  | 1
  |
  | many
  v
Student
  |
  | many
  |
  v
StudentSubject
  ^
  |
  | many
  |
Subject

Student
   |
   | 1
   |
   | many
   v
Attendance
   ^
   |
   | many
   |
Subject
```

## API endpoints

```
GET    /api/attendance
GET    /api/attendance/student/{studentId}
GET    /api/attendance/student/{studentId}/subject/{subjectId}
GET    /api/attendance/student/{studentId}/summary
POST   /api/attendance
POST   /api/attendance/bulk
PUT    /api/attendance/{id}
DELETE /api/attendance/{id}
GET    /api/subjects
```

## API design: the API doesn't have to expose the database structure

Database row:

```
Attendance
----------------
StudentId
SubjectId
Date
Status
```

API response:

```json
{
  "id": 1,
  "studentId": 1,
  "studentName": "Ananya Sharma",
  "subjectId": 1,
  "subjectName": "Mathematics",
  "date": "2026-09-01",
  "status": "Present"
}
```

The API joins `Attendance` with `Student` and `Subject` and returns a combined,
easier-to-use shape (`AttendanceRecordDto`) — Angular never has to know how the tables
are related internally.

## Date handling

Attendance is tied to a **calendar date**, not an exact moment in time — "11 September"
means the same thing everywhere, unlike a timestamp which shifts across time zones. The
`Attendance.Date` column uses .NET's `DateOnly` type (mapped to SQL Server's `date`
column type), avoiding time-of-day/timezone bugs entirely.

## Temporary: unauthenticated "Record Attendance" page

`/record-attendance` lets anyone select a grade, subject, and date, and save attendance
for the whole class. This is intentionally open right now because **authentication
hasn't been introduced yet** (a later phase). In a production application, only an
authorized teacher should be able to reach this page — it is clearly labeled as
temporary in the UI.

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

- `GET /api/subjects` → 4 subjects.
- `GET /api/attendance/student/1/summary` → per-subject percentages.
- `GET /api/attendance/student/1` → list of records with subject names.
- Visit `/students/1` → Attendance panel shows per-subject percentages → "View Attendance".
- Visit `/students/1/attendance` → full summary table with Overall row.
- Click a subject → `/students/1/attendance/{subjectId}` → date/status list.
- Visit `/record-attendance` → select grade + subject + date → mark statuses → Save →
  success message.
- Re-save the same grade/subject/date → records are updated, not duplicated.
- `POST /api/attendance` with an existing (student, subject, date) → `409 Conflict`.
- `POST /api/attendance` with an unknown student/subject → `400 Bad Request`.
- Restart the API → attendance data is still there.

## Student learning summary

- Attendance is business data — it records something that happened, not just something
  that exists.
- An attendance record connects a student, a subject, a date, and a status.
- A student can have many attendance records; a subject can have many attendance records.
- The backend validates business rules (duplicate prevention, valid student/subject/status).
- The database enforces the same rule with a unique constraint, as a safety net.
- The API calculates the attendance percentage — Angular just displays it.
- A teacher-facing page can create data that a student-facing page later consumes.

```
Teacher
   ↓
Angular
   ↓
HTTP
   ↓
C# API
   ↓
EF Core
   ↓
SQL Database
   ↓
Attendance
   ↓
Student
```

## Next phase

Phase 6 introduces **Marks & Assessments**:

```
Student
   |
   └── Assessment
          |
          ├── Subject
          ├── Exam/Test
          ├── Date
          ├── Maximum Marks
          └── Marks Obtained
```

The same architecture (`Angular → C# API → EF Core → SQL`) will be reused for this new
requirement, showing how one application pattern scales to different school processes.
