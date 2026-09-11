# Phase 2 — Student Data & Angular Application

## What problem did we solve?
The Students page could only show a fixed, hard-coded list. A real school portal needs
to display many students, let a teacher/parent find one quickly, and open a profile
page for a single student. We solved this using local (mock) data instead of a
database — the database comes later, in Phase 4.

## What is data?
Data is the information StudentConnect works with — in this phase, a list of student
records (id, name, grade, section, email). Right now that data lives inside our
Angular application (`student.service.ts`). Later it will live in a database.

## What is a model/interface?
`student.model.ts` defines a TypeScript `Student` interface — a contract describing
the exact shape of a student record (which fields exist and their types). Using a
model instead of passing around loose objects means the compiler catches mistakes
(e.g. a typo like `frstName`) before the app ever runs.

## What is an Angular service?
`StudentService` is the single place that owns student data and knows how to fetch
it. Components (like `Students` and `StudentDetail`) ask the service for data instead
of holding or hard-coding it themselves. This separation — UI vs. data — means Phase 3
can swap the service's internals from "return a local array" to "call the API" without
touching the components at all.

## What is data binding?
Angular templates use `{{ }}` interpolation and `[value]`/`(input)` bindings to keep
the HTML in sync with TypeScript data automatically. When `searchTerm` or
`gradeFilter` change, the list re-renders without manual DOM manipulation.

## What is routing?
`/students` and `/students/1` are two different "pages" handled entirely inside the
browser by Angular's router — no full page reload happens. The router reads the `:id`
segment of the URL and gives it to `StudentDetail`, which asks `StudentService` for
that specific student.

## What happens when I click "View Profile"?
```
User clicks "View Profile"
       ↓
Angular Router navigates to /students/{id}
       ↓
StudentDetail component activates
       ↓
Component reads the :id route parameter
       ↓
StudentService.getStudentById(id)
       ↓
Student found (or undefined)
       ↓
Template renders the profile (or "Student not found")
```

## What happens behind the scenes?
```
User
 ↓
Browser
 ↓
Angular Router
 ↓
Component (Students / StudentDetail)
 ↓
StudentService
 ↓
Student data (local array)
 ↓
Template
 ↓
Browser
```

## What is still missing?
The student data is hard-coded inside `StudentService` — it resets every time the app
reloads and isn't shared between users. The next step is:
```
Angular
   ↓
HTTP
   ↓
C# ASP.NET Core Web API
```
`StudentService`'s public methods (`getStudents`, `getStudentById`) will stay the
same; only their implementation will change to call the API.
