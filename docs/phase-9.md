# Phase 9 — Activities & Student Registration

## 1. What problem did we solve?

The school is organizing activities — a Science Exhibition, a Coding Workshop, a
Football Tournament — and students need to discover which ones are available for their
grade and register for the ones they want to attend.

## 2. What is a business entity?

An **Activity** is a business entity: a real thing the school organizes, with a title,
description, date, location, an eligible grade, and a capacity.

An **ActivityRegistration** is also a business entity — but it doesn't describe a thing,
it describes an **event**: a specific student choosing to attend a specific activity, at
a specific time, with a status that can change.

## 3. What is a relationship?

```
Student ↔ Activity
```

A student can register for many activities. An activity can have many students. Neither
side "belongs" to the other — they're connected.

## 4. What is many-to-many?

```
Student A → Coding Workshop
Student A → Science Exhibition

Student B → Coding Workshop
Student B → Football Tournament
```

If we tried to model this with a single `ActivityId` column on `Student`, a student
could only ever join one activity. If we tried a list of activity IDs stored on the
student, we'd lose the ability to query it efficiently, enforce constraints on it, or
attach extra information to *one specific* connection. The standard relational solution
is a separate table:

```
Student       Activity
-------       --------
A             Coding
A             Science
B             Coding
B             Football
```

## 5. Why do we need ActivityRegistration?

The relationship itself carries information that belongs to neither the student nor the
activity alone:

```
RegistrationDate
Status
```

"When did Student A register for Coding Workshop?" and "Is that registration still
active?" are facts about the *connection*, not about the student or the activity in
isolation. This is why `ActivityRegistration` is a full entity with its own `Id`, not
just a foreign key sitting inside `Student`.

## 6. What is a business rule?

Rules the application must enforce, beyond "is this field filled in":

```
A student cannot register twice for the same activity.
A student cannot register for an activity that belongs to a different grade.
A full activity cannot accept another registration.
A cancelled registration does not count toward capacity.
An activity that has already occurred cannot accept new registrations.
```

## 7. Why does the backend enforce these rules?

Angular can disable a button or show a friendly message immediately — that's good for
user experience. But the browser is not trustworthy: a user could call the API directly
with a tool like Postman, bypassing Angular entirely. **The backend is the only place
that can actually protect the data**, because it's the only part of the system a client
cannot control or bypass.

## 8. What is capacity?

```
Capacity = the maximum number of ACTIVE registrations an activity will accept.
```

`Capacity` is stored on the `Activity` row. The **current** number of registrations is
never stored as a separate counter — it's always calculated by counting
`ActivityRegistration` rows where `Status == Registered`. Storing a manually-maintained
counter alongside the real data risks the two falling out of sync (e.g. a cancellation
that forgets to decrement it); calculating it fresh every time can never drift.

## 9. Why calculate AvailableSeats?

```
AvailableSeats = Capacity - CurrentRegistrations
```

Same reasoning as above — it's derived, not stored, so it's always correct without any
extra code needing to keep it updated.

## 10. Why use Cancelled instead of deleting?

A cancelled registration is still a fact that happened — "Student X was registered for
Activity Y, then changed their mind." Deleting the row would erase that history. Instead,
`Status` flips from `Registered` to `Cancelled`; the row (and its original
`RegistrationDate`) stays.

This also shapes the **re-registration** design: rather than allowing a second row for
the same student+activity, a unique index on `(StudentId, ActivityId)` guarantees at most
one row ever exists. Registering again after cancelling **reactivates the same row**
(`Status` flips back to `Registered`, `RegistrationDate` is refreshed) instead of
inserting a duplicate.

## 11. What happens behind the scenes?

```
Student clicks Register
        ↓
Angular sends HTTP request
        ↓
ASP.NET Core API receives request
        ↓
Backend validates business rules
        ↓
EF Core communicates with SQL Server
        ↓
Registration is stored
        ↓
API returns result
        ↓
Angular updates screen
```

The full validation pipeline for `POST /api/activity-registrations`:

```
1. Student exists?               → 400 if not
2. Activity exists?               → 400 if not
3. Activity date in the past?     → 400 "Registration is closed..."
4. Student's grade == Activity's grade? → 400 "Student is not eligible..."
5. Already Registered (not cancelled)?  → 409 "already registered"
6. Active registrations >= Capacity?    → 409 "already full"
7. Create new row, or reactivate a Cancelled one
8. Server sets RegistrationDate = DateTime.UtcNow (never trusts the client)
9. Save and return the result
```

## 12. Database relationship

```
Grade
  │
  └──< Activity
          │
          └──< ActivityRegistration >── Student
```

```
Activity.GradeId              → Grade.Id
ActivityRegistration.ActivityId → Activity.Id
ActivityRegistration.StudentId  → Student.Id
```

A unique index on `(StudentId, ActivityId)` in `ActivityRegistrations` is the
database-level backstop for "one registration per student per activity" — even a bug in
application code could never create a duplicate row. A check constraint
(`Capacity > 0`) similarly backstops the capacity rule at the schema level.

## 13. Concurrency (a simplified introduction)

Imagine `Capacity = 30`, `CurrentRegistrations = 29`, and two students click "Register"
at nearly the same instant. Both requests could read `29 < 30` as true before either has
saved — and both might then insert a row, pushing the activity to 31.

This implementation wraps the check-then-insert sequence in a database transaction
(`BeginTransactionAsync` / re-counting active registrations immediately before deciding
to insert, then `CommitAsync`), which narrows this window compared to doing the count
and the insert as two completely separate, uncoordinated operations. **It does not fully
eliminate the race** — a true guarantee would need a stronger isolation level (e.g.
serializable transactions) or an optimistic-concurrency token on `Activity`, both of
which add real complexity. For this learning project, the code clearly checks capacity
server-side inside a transaction, and this limitation is intentionally documented rather
than solved with distributed locks or message queues, which would be out of proportion
for the problem at this stage.

## 14. Database changes

```
Activities
ActivityRegistrations
```

New migration: `AddActivitiesAndRegistrations`. No existing data was touched. Seed data
adds 5 fictional activities across the existing Grade 8/9/10 students, plus a small set
of registrations — including one `Cancelled` row, to demonstrate it doesn't count toward
capacity.

## 15. API endpoints

```
GET    /api/activities
GET    /api/activities/{id}
GET    /api/activities/grade/{gradeId}
POST   /api/activities
PUT    /api/activities/{id}
DELETE /api/activities/{id}

GET    /api/activity-registrations/student/{studentId}
GET    /api/activity-registrations/activity/{activityId}
GET    /api/activity-registrations/{id}
POST   /api/activity-registrations
PUT    /api/activity-registrations/{id}/cancel
```

`PUT /api/activities/{id}` only allows `Title`, `Description`, `Date`, `Location`, and
`Capacity` to change — `GradeId` is fixed after creation (same reasoning already used for
`Homework`'s update endpoint: changing it would silently invalidate the eligibility of
every existing registration). Reducing `Capacity` below the current number of active
registrations is rejected. `DELETE /api/activities/{id}` is rejected with `409` if any
registration (even a cancelled one) references it, so registration history is never
silently orphaned.

## 16. Angular features

- `ActivityService` / `ActivityRegistrationService` — all HTTP calls live here; Angular
  never re-implements a business rule, it only reflects what the API returns.
- `StudentActivities` (`/students/:id/activities`) — activities available for the
  student's grade, with inline Register/Cancel and a "My Registered Activities" table.
- `ActivityDetail` (`/students/:id/activities/:activityId`) — full details plus a
  register/cancel button reflecting current state (registered / full / closed).
- `ActivityManagement` (`/activity-management`) — temporary demo admin/teacher page:
  list, create, edit, delete, and jump to an activity's registrations.
- `CreateActivity` / `EditActivity` — forms with client-side validation (required
  fields, capacity > 0); the backend validates the same rules again.
- `ActivityRegistrationsView` (`/activity-management/:id/registrations`) — the roster
  view: every registration for one activity, with status.

## 17. Architecture

```
Browser
   ↓
Angular
   ↓
ASP.NET Core Web API
   ↓
SQL Server
```

Angular never connects to SQL Server directly, never holds database credentials, and
never decides whether a registration is valid — it only displays what the API returns.

## Preserved from earlier phases

Students, Grades, Subjects, Attendance, Assessments, Marks, Homework, Homework
Submissions, and Learning Materials (including file upload/download/delete) were
verified working after this phase's changes — no existing functionality was modified or
broken.

## How to run

Terminal 1 — API (applies pending migrations automatically):
```bash
cd backend/StudentConnect.Api
dotnet run --launch-profile http
```
Terminal 2 — Frontend:
```bash
cd frontend
npm start
```

## How to test

- `GET /api/activities` → 5 seeded activities with computed `currentRegistrations`/`availableSeats`.
- Create an activity with `capacity=1`; register one student → `201`; register a second
  student → `409` "already full".
- Register the same student twice → `409` "already registered".
- Register a student from a different grade → `400` "not eligible".
- Try registering for an activity with a past date → `400` "registration is closed".
- Cancel a registration → status becomes `Cancelled`, seat becomes available again.
- Re-register the same student after cancelling → the same row reactivates (no
  duplicate row created).
- Try reducing an activity's capacity below its current registration count → `400`.
- Try deleting an activity that has registrations → `409`.
- Restart the API → activities, registrations, and their statuses all persist.

## 18. Future Azure connection (conceptual only)

Not implemented in this phase. Later, `ASP.NET Core API → SQL Server` could be hosted
using a managed relational database capability such as **Azure SQL Database** — the
same idea already introduced for the rest of StudentConnect's data.

## Student learning summary

> An activity is a business entity. Students and activities have a many-to-many
> relationship, so we use a registration entity between them. The registration record
> can store information such as registration date and status. The backend applies
> business rules such as grade eligibility, capacity, duplicate registration, and
> cancellation.

```
Data + Relationships + Business Rules + Workflow = Application Behavior
```

## Known limitations (intentionally deferred)

- No authentication or authorization — student/teacher identity is still a
  development/demo mechanism, exactly as in every earlier phase.
- No notifications (email/SMS) when registering or cancelling.
- No Azure deployment of any kind.
- Concurrency protection is a transaction + re-count, not a full guarantee under heavy
  simultaneous load — documented above, not solved with locks/queues here.

## Next phase

**Identity & Access** — understanding users, authentication, authorization, and why
applications need to know who is using them. Not implemented yet.
