# Phase 4 — SQL Database & Persistent Data

## The problem

In Phase 3, students lived in a C# `List<Student>` — created fresh every time the API
started. Stop the API, and the list is rebuilt from scratch next time. Add a student,
restart the API, and that student is gone.

That's fine for 10 students in a demo. It falls apart with 10,000 real students, or the
moment anyone expects their data to still be there tomorrow.

**We need somewhere to store data that survives the application restarting.** That's a
**database** — specifically a **relational database**, which organizes data into tables
made of rows and columns, with relationships between tables. We use **SQL Server** as
the database engine, and **Entity Framework Core (EF Core)** as the bridge that lets our
C# code talk to it without writing raw SQL for every operation.

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
                           v
                   ENTITY FRAMEWORK CORE
                           |
                           | SQL
                           v
                    SQL SERVER DATABASE
                           |
                           v
                    StudentConnectDb
```

```
StudentConnectDb
      |
      +── Grades
      |
      └── Students
```

**Analogy:** A school keeps student records in a filing cabinet. The filing cabinet
keeps the records safe even when a teacher leaves the room for the day. Similarly, our
database keeps StudentConnect's data safe even when the API process stops running.
(This is an analogy — it's not literally how a database works internally, but it
captures the key idea: **application** and **data** are separate things.)

## Database design

### Grade

```
Id    (primary key)
Name  (e.g. "Grade 8")
```

### Student

```
Id         (primary key)
StudentId  (e.g. "STU001")
FirstName
LastName
Email
GradeId    (foreign key → Grade.Id)
Section
```

### The relationship

```
Grade
   |
   | 1
   |
   | many
   ↓
Student
```

One grade can have many students:

```
Grade 8
   |
   +-- Ananya
   +-- Rahul
   +-- Aisha
```

- **Primary key** — a column that uniquely identifies a row in its own table (`Student.Id`).
- **Foreign key** — a column that points to a primary key in *another* table
  (`Student.GradeId` points to `Grade.Id`). This is how the database records "this student
  belongs to this grade."
- **One-to-many relationship** — one `Grade` row can be referenced by many `Student` rows.

We deliberately did **not** create a separate `Section` entity, or model attendance,
marks, homework, etc. yet — keeping the model small keeps the lesson focused.

## Entity Framework Core

```
C# Application
       ↓
Entity Framework Core
       ↓
SQL Database
```

EF Core is an **ORM** (Object-Relational Mapper). It lets our C# code work with database
data as ordinary C# objects (`Student`, `Grade`) and navigate relationships
(`student.Grade.Name`) instead of hand-writing SQL for every query. It translates C#
LINQ queries (like `db.Students.Where(s => s.GradeId == 1)`) into SQL behind the scenes.

This does **not** mean developers never need SQL — understanding basic SQL (see
[sql-basics.md](sql-basics.md)) is still essential for debugging, performance, and more
advanced scenarios.

### DbContext

`StudentConnectDbContext` (in `backend/StudentConnect.Api/Data/StudentConnectDbContext.cs`)
represents our application's session with the database. It exposes:

```csharp
DbSet<Student> Students
DbSet<Grade> Grades
```

Each `DbSet<T>` is roughly "the table of `T` rows, as a C# collection I can query."

### Migrations

A **migration** is a versioned, code-based description of a change to the database
schema — "add this table," "add this column." EF Core compares our C# model to the
previous migration and generates the difference.

```
C# Model
   ↓
EF Core Migration
   ↓
SQL Database Schema
```

We never hand-wrote `CREATE TABLE` statements — the `InitialCreate` migration
(`backend/StudentConnect.Api/Migrations/`) was generated from our `Student`/`Grade`
classes, and applying it (`dotnet ef database update`) created the actual tables (plus
seed data) in `StudentConnectDb` on SQL Server LocalDB.

### Seed data

The migration also inserts 3 grades and 10 fictional students, defined in
`StudentConnectDbContext.OnModelCreating` via `HasData(...)`. This lets us immediately
verify the API is reading from SQL, not from memory.

## API endpoints

```
GET    /api/students        List all students
GET    /api/students/{id}   Get one student (404 if not found)
POST   /api/students        Create a student (409 if StudentId already exists)
PUT    /api/students/{id}   Update a student (404 if not found)
DELETE /api/students/{id}   Delete a student (404 if not found)
GET    /api/grades          List all grades (used by Angular's Add/Edit forms)
```

## Full request flow: opening `/students/1`

1. User opens the StudentConnect page and navigates to `/students/1`.
2. Angular Router loads `StudentDetail`.
3. The component asks `StudentService` for student 1.
4. `StudentService` sends an HTTP GET to `/api/students/1`.
5. ASP.NET Core receives the request.
6. `StudentsController.GetStudentById` handles it.
7. The controller uses `StudentConnectDbContext`.
8. EF Core translates this into a SQL query and executes it against SQL Server.
9. SQL Server returns the matching row.
10. EF Core maps the row back into a C# `Student` object.
11. The controller converts it to a `StudentResponseDto` and ASP.NET Core serializes it as JSON.
12. Angular receives the JSON.
13. Angular displays the student profile.

## Persistence, demonstrated

**Phase 3:** add student `STU011` → restart the API → student is gone (it only ever
existed in a `List<Student>` in memory).

**Phase 4:** add student `STU011` → restart the API → student is still there (it was
written to SQL Server, which is a separate, independently-running process).

```
Application
     ≠
Data
```

The application can restart without losing the data — that's the whole point of a
database.

## EF Core vs. SQL — who does what

- **SQL Server** — stores the data, in tables, on disk.
- **Entity Framework Core** — a library that helps our C# application talk to SQL Server
  using C# objects instead of hand-written SQL for most operations.
- **ASP.NET Core** — hosts the web API that Angular talks to over HTTP.
- **Angular** — renders the browser UI and never talks to SQL Server directly.

`Angular → API → EF Core → SQL Server` does **not** mean EF Core *is* the database — it's
the translator standing in front of it.

## Why Angular never talks to SQL directly

```
Angular
   ↓
HTTP
   ↓
C# API
   ↓
EF Core
   ↓
SQL Server
```

- **Security** — a browser cannot safely hold database credentials.
- **Separation of responsibilities** — the frontend renders UI; the backend owns data access.
- **Business logic & validation** — required fields, duplicate `StudentId` checks, etc.
  live in one place (the API), not scattered across every client.
- **Controlled access** — the API decides exactly what a client is allowed to read or change.

## How to run

1. Confirm LocalDB is available: `sqllocaldb info` (should list `MSSQLLocalDB`).
2. Apply migrations (only needed once, or after model changes — the API also does this
   automatically on startup):
   ```bash
   cd backend/StudentConnect.Api
   dotnet ef database update
   ```
3. Start the API:
   ```bash
   dotnet run --launch-profile http
   ```
4. Start Angular:
   ```bash
   cd frontend
   npm start
   ```
5. Visit `http://localhost:4200/students`.

## How to test

- `GET http://localhost:5286/api/students` → 10 students, each with `gradeName`.
- `GET http://localhost:5286/api/students/1` → single student.
- `GET http://localhost:5286/api/students/999` → 404.
- `POST http://localhost:5286/api/students` with a new `studentId` → 201 Created.
- `POST` again with the **same** `studentId` → 409 Conflict.
- `PUT http://localhost:5286/api/students/1` → 200 with updated fields.
- `DELETE http://localhost:5286/api/students/1` → 204, then `GET` the same id → 404.
- Add a student in Angular, restart the API, refresh Angular — student still there.

## Beginner troubleshooting

- **SQL Server/LocalDB is not running** — run `sqllocaldb info` and `sqllocaldb start MSSQLLocalDB`.
- **Connection string is incorrect** — check `ConnectionStrings:DefaultConnection` in
  `backend/StudentConnect.Api/appsettings.json`.
- **Migration fails** — usually means the database is in a state that doesn't match the
  migration history; check the exact SQL error in the console output.
- **Database doesn't exist** — running `dotnet ef database update` (or just starting the
  API, which calls `Database.Migrate()`) creates it from the migrations.
- **API returns 500** — check the API's console output; the real exception is logged
  there but never sent to the browser.
- **Angular cannot reach the API** — is the API running? Correct port (5286)? CORS policy
  allows `http://localhost:4200`? Correct `apiBaseUrl` in `environment.development.ts`?
- **Data isn't persistent** — confirm the API is actually reading from
  `StudentConnectDbContext`/SQL and not an old in-memory list.

## Student learning summary

After Phase 4, you should be able to explain:

- Angular is the frontend; ASP.NET Core is the backend; SQL Server is the database.
- The API is how the frontend talks to the backend.
- EF Core helps the C# application talk to the database using C# objects.
- The frontend never connects directly to the database.
- Data now survives an API restart.

```
Frontend ≠ Backend ≠ Database
Angular → API → Database
```

## Next phase

Phase 5 introduces the first real StudentConnect business feature: **Attendance**.
Architecture stays the same (`Angular → C# API → SQL Database`), but we add new data and
a new relationship:

```
Student
   |
   └── Attendance
          |
          ├── Date
          ├── Subject
          └── Status
```

This teaches how a real application grows its data model and business features on top
of the same architecture.
