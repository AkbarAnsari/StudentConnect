# Solution Architecture — Multi-Tenancy Foundation (Horizon 1, Epic 1)

**Author role:** Solution Architect
**Status:** Approved for implementation (this session) — Epic 1 scope only; see §7 for what
is explicitly deferred.

## 1. Problem statement

StudentConnect today is single-tenant: one Postgres database, no concept of "which
school does this row belong to." Per `docs/product-strategy-and-roadmap.md` §6
Horizon 1, this is the single highest-leverage, hardest-to-retrofit decision blocking
a second customer. This document defines the technical approach before any code is
written, per the project's own established discipline (see Phase 15's academic-model
design-first precedent).

## 2. Chosen approach: shared database, shared schema, discriminator column

Three standard multi-tenancy patterns were considered:

| Pattern | Isolation | Ops cost | Migration cost | Verdict |
|---|---|---|---|---|
| Database-per-tenant | Strongest | High (N databases to patch/back up) | Low (no schema change) | Rejected — too costly at this stage's scale (dozens, not thousands, of schools) |
| Schema-per-tenant | Strong | Medium | Medium | Rejected — Npgsql/plain-ADO.NET stores would need per-request schema switching; adds real complexity for marginal isolation gain over the row-level approach at this scale |
| **Shared DB, shared schema, `SchoolId` column** | Application-enforced | Low (one database) | **This document** | **Chosen** — matches the existing plain-ADO.NET architecture, cheapest to operate, consistent with how `StudentId`/`TeacherId` foreign keys already scope data today |

**Trade-off accepted:** isolation is enforced in application code (every query must
filter by `SchoolId`), not by the database engine. This is a real, ongoing discipline
requirement — every new store method written from now on must take a `schoolId`
parameter. This is the correct trade-off for the project's current scale and team size,
not a permanent decision — Postgres Row-Level Security (RLS) policies are a documented
follow-up hardening option (§7) once the column exists everywhere.

## 3. New entity: `School`

```
Schools
  Id            integer PK
  Name          text            -- "Riverside Community School"
  Code          text UNIQUE     -- short slug, e.g. "riverside" — reserved for future subdomain/branding use
  CreatedAtUtc  timestamptz
  IsActive      boolean         -- soft "suspend a tenant" switch, no billing dependency yet
```

No `PlanTier`/billing columns yet — deliberately deferred to the billing epic so this
table doesn't get ahead of a business-model decision that hasn't been made (see
roadmap §9, item 2).

## 4. Scope of this pass: which tables get `SchoolId`

Tenant isolation matters most where an Admin/Teacher directly CRUDs or looks up an
entity by ID — that's where a School A caller could otherwise reach School B's row.
This pass adds `SchoolId` to the **identity and org-structure layer**:

`Users`, `Students`, `Teachers`, `Grades`, `Subjects`, `AcademicYears`, `Terms`, `Sections`

These 8 tables are chosen because:
1. They're queried directly by ID from Admin-facing CRUD endpoints today.
2. Every other table in the schema (`Attendance`, `Marks`, `Homework`, `Activities`,
   `Notifications`, `AuditLogs`, `EmailOutbox`, etc.) reaches the tenant boundary
   transitively through a `StudentId`/`TeacherId`/`GradeId` foreign key into one of
   these 8 tables.

**Explicitly deferred (see §7, disclosed, not silent):** the ~20 leaf/transactional
tables are not given their own `SchoolId` column in this pass, and — more importantly —
the controllers that operate on them (Attendance, Marks, Homework, Activities, ...) do
not yet re-verify that a given `StudentId`/`TeacherId` route parameter belongs to the
caller's school before acting on it. This is real follow-up work, tracked as Epic 1b.

## 5. Tenant resolution: JWT claim

StudentConnect already authenticates via a locally-issued JWT (`JwtTokenService`).
`SchoolId` becomes a new claim on that token, issued at login by looking up the user's
`Users.SchoolId`. A new `ICurrentTenantService.SchoolId` (in `StudentConnect.Application`,
implemented in `StudentConnect.Infrastructure`, mirroring the existing
`ICurrentUserService` pattern exactly) reads it back out on every request.

No header/subdomain-based resolution is introduced yet — unnecessary until there's a
public marketing site or branded login per school, which is a later horizon.

## 6. Self-service onboarding

New **public** endpoint `POST /api/schools/register` (the only unauthenticated
mutation in the API besides login):

1. Validates `SchoolName`, `AdminUsername`, `AdminPassword`.
2. Creates the `School` row.
3. Creates the first `Users` row (`Role = Admin`, `SchoolId` = the new school).
4. Returns a JWT identical in shape to `/api/auth/login`'s response, so the new admin
   is immediately signed in — no separate "check your email" step in this pass (email
   verification is a disclosed follow-up, not a security-critical gap at this scale
   since only self-registration of a *new, empty* tenant is possible — there's no
   existing data to protect until this call succeeds).

Deliberately **not** in this pass: seeding default Grades/Subjects for the new school.
The Admin uses the existing Academic Structure screens (already built) to set those up
— reusing existing UI is cheaper than building a setup wizard, and is a reasonable v1.

## 7. Explicitly deferred (Epic 1b and later — not done in this pass)

- Tenant-scoping the ~20 leaf/transactional tables and their controllers (Attendance,
  Marks, Homework, HomeworkSubmissions, LearningMaterials, Activities,
  ActivityRegistrations, ParentStudents, Notifications, AuditLogs, EmailOutbox,
  StudentSubjects, Assessments).
- Postgres Row-Level Security (RLS) as defense-in-depth under the application-level checks.
- Platform-admin console (cross-tenant visibility for the product's own operators).
- Billing/plan enforcement (student-count limits, feature gating by tier).
- Email verification / invite-based admin provisioning as an alternative to instant self-signup.

## 8. Migration plan for existing data

All rows created before this change belong to one implicit tenant. A `DO $$ ... $$`
migration block (matching the project's established seed.sql idempotent-migration
pattern) creates a **"Demo School"** row and backfills `SchoolId` on all 8 tables'
existing rows to it, so the current dev/demo data keeps working unchanged after the
migration — exactly the same discipline already used for the `Student.Section` →
`Sections` migration in Phase 15.

## 9. Definition of done for this pass

- A second, independently self-registered school's Admin cannot see, list, or fetch by
  ID any Student/Teacher/Grade/Subject/AcademicYear/Term/Section belonging to the Demo
  School or any other tenant, and vice versa.
- Existing Demo School data and all 24 existing tests continue to pass unchanged.
- `POST /api/schools/register` creates a working, immediately-usable tenant end-to-end,
  verified live (not just unit-tested).
