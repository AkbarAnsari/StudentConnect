# SQL Basics

A short, beginner-level introduction to the SQL concepts used in StudentConnect's
database, `StudentConnectDb`.

## Database

**Simple explanation:** A database is an organized place to store information so it can
be reliably saved, searched, and updated.

**Analogy:** A filing cabinet for an entire school — every drawer holds a different kind
of record.

**Technical explanation:** A relational database (like SQL Server) stores data in
tables, enforces structure (each table has defined columns and types), and lets you
query it using SQL.

**StudentConnect example:** `StudentConnectDb` is our database — it currently holds the
`Grades` and `Students` tables.

## Table

**Simple explanation:** A table stores many records of the same kind of thing.

**Analogy:** One drawer in the filing cabinet, dedicated to one type of form — e.g. a
"Students" drawer.

**Technical explanation:** A table has a fixed set of columns (a schema); every row in
the table follows that schema.

**StudentConnect example:** The `Students` table; the `Grades` table.

## Row

**Simple explanation:** A row is one single record.

**Analogy:** One completed form inside the drawer — e.g. Ananya Sharma's student record.

**Technical explanation:** A row is one instance of the table's schema, with a specific
value in every column.

**StudentConnect example:** The row where `StudentId = 'STU001'` represents Ananya Sharma.

## Column

**Simple explanation:** A column is one piece of information that every row has.

**Analogy:** One field on the form — e.g. "First Name."

**Technical explanation:** A column has a name and a data type (e.g. `FirstName` is text,
`GradeId` is an integer).

**StudentConnect example:** `Students.FirstName`, `Students.Email`, `Students.GradeId`.

## Primary key

**Simple explanation:** A value that uniquely identifies one row in a table.

**Analogy:** A student's unique roll number — no two students share one.

**Technical explanation:** `Student.Id` is the primary key of the `Students` table. The
database guarantees it's unique and uses it to quickly find a specific row.

**Why does Student have an Id?** So every student record can be uniquely and reliably
referenced — by the API, and by other tables that need to point to it.

## Foreign key

**Simple explanation:** A value in one table that points to a row in another table.

**Analogy:** Writing a classroom number on a student's form that refers to a specific
classroom listed elsewhere.

**Technical explanation:** `Student.GradeId` is a foreign key — it stores the `Id` of a
row in the `Grades` table, linking each student to their grade.

**Why does Student have a GradeId?** So we can record which grade a student belongs to
without repeating the grade's full name in every single student row.

## Relationship

**Simple explanation:** How rows in one table connect to rows in another.

**Analogy:** One classroom can contain many students, but each student is only in one
classroom.

**Technical explanation:** `Grade` and `Student` have a one-to-many relationship: one
`Grade` row can be referenced by many `Student` rows via `GradeId`.

**Why can one Grade have many Students?** Because many students are in "Grade 8" at the
same time — the relationship is naturally one-to-many, not one-to-one.

## Persistence

**Simple explanation:** Data stays saved even after the program that used it stops running.

**Analogy:** Papers stay in the filing cabinet overnight, even after everyone goes home.

**Technical explanation:** SQL Server runs as its own separate process/service, entirely
independent of our ASP.NET Core API. When the API restarts, it simply reconnects to the
same database and finds the same rows still there.

**Why does data remain after restarting the application?** Because the data was never
"inside" the API process to begin with — it was always stored by SQL Server, on disk.

## Basic SQL examples

Get all students:

```sql
SELECT *
FROM Students;
```
> Get all students.

Get students in a specific grade:

```sql
SELECT *
FROM Students
WHERE GradeId = 1;
```
> Get students belonging to Grade 8, if Grade 8 has Id 1.

Insert a new student:

```sql
INSERT INTO Students (StudentId, FirstName, LastName, Email, GradeId, Section)
VALUES ('STU011', 'Riya', 'Patel', 'riya@example.com', 1, 'A');
```

Update a student:

```sql
UPDATE Students
SET Section = 'B'
WHERE Id = 1;
```

Delete a student:

```sql
DELETE FROM Students
WHERE Id = 1;
```

This is intentionally simple SQL — enough to understand what EF Core is doing behind the
scenes, not a full SQL course.
