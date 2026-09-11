# Phase 14 — Enterprise Architecture Review & Roadmap

## Learning goal

Phases 1–13 built a working **academic records system**: students, attendance, marks,
homework, learning materials, activities, JWT auth with role-based access. This phase
is a step back — an enterprise-architect-style review of that system — to answer two
questions: *what would a real school management platform still need?* and *which of
that is worth building next?*

This document is the roadmap; the items marked **(this phase)** below are the ones
actually implemented now. Everything else is deliberately left as documented,
prioritized backlog — not built yet.

## Current state (end of Phase 13)

```
Angular SPA  →  ASP.NET Core Web API  →  PostgreSQL
                        │
                        └── IFileStorage → Local disk | Azure Blob Storage
```

Domain coverage: Students, Grades, Subjects, Student↔Subject enrollment, Attendance,
Assessments, Marks, Teachers, Homework + Submissions, Learning Materials, Activities +
Registrations. Roles: Student, Teacher, Admin. Single tenant, single environment,
no automated tests, no CI, no observability, no notifications, no analytics.

## Gap analysis

| Domain | Status | Missing |
|---|---|---|
| Academics | Partial | Timetable, exam scheduling, report cards/GPA, promotion workflow |
| People | Partial | **Parent role/portal (this phase)**, HR/staff records, bulk onboarding |
| Communication | Missing | **In-app notifications (this phase)**, email/SMS, messaging, event calendar |
| Finance | Missing | Fee structure, invoicing, online payments |
| Operations | Missing | Library, transport, asset management, multi-campus |
| Analytics | Missing | **Dashboards (this phase)**, exportable reports |
| Compliance | Missing | Audit trail, retention policy, consent management |
| Platform | Missing | **Automated tests + CI (this phase)**, App Insights, Key Vault, multi-tenancy, rate limiting, refresh tokens |

## Roadmap (by horizon)

### Near-term — this phase
1. **Automated tests + CI** — xUnit backend test project, GitHub Actions workflow
   (build + test on every push/PR).
2. **Parent role + portal** — new `Parent` role, `ParentStudent` linking table
   (many children per parent), read-only views scoped to the parent's own children
   (attendance, marks, homework, materials, activities).
3. **In-app notifications** — `INotificationService` abstraction (same pattern as
   `IFileStorage`), DB-backed outbox, triggered on: homework assigned, absence marked,
   new mark recorded. Bell icon + list in the UI. Real email/SMS delivery is a future
   provider swap (`Local` outbox today → Azure Communication Services / SendGrid later),
   deliberately mirroring the `Storage:Provider` pattern from Phase 13.
4. **Dashboards/analytics** — aggregate endpoints (attendance %, grade distribution,
   at-risk students) surfaced as an Admin/Teacher dashboard.

### Mid-term (not yet built)
5. Timetable/scheduling module with conflict detection.
6. Report card generation (PDF, grade computation rules).
7. Fee management + payment gateway integration.
8. Refresh tokens + revocation, MFA, audit log table for grade/attendance changes.
9. Multi-tenant readiness (`SchoolId` scoping on every entity).

### Longer-term (not yet built)
10. Library, transport, HR/payroll modules — only if scope genuinely expands.
11. Event-driven backbone (Azure Service Bus) for async notification/report generation.
12. Complete paused Azure hosting (Container Apps + managed Postgres + Key Vault + CDN).
13. Data compliance pass (retention, export/delete-my-data, consent) if real student
    data is ever involved outside a teaching sandbox.

## Why this order

Tests + CI come first because every feature added afterward needs a regression net —
building Parent/Notifications/Dashboards without it would make the codebase
progressively riskier to change. Parent portal was prioritized over Notifications
because notifications are only useful once there's a parent-facing surface to notify
into. Dashboards last because they consume data produced by the other three (parent
engagement, notification events) rather than gating them.

## Addendum — EF Core removed, replaced with plain ADO.NET (post-launch change)

After the above shipped, the data-access layer itself was reworked at explicit user
request: EF Core (`DbContext`, LINQ queries, code-first migrations) was removed
entirely and replaced with plain `Npgsql` ADO.NET calls, organized into one "store"
class per domain area (`backend/StudentConnect.Api/Data/Stores/*.cs`), each holding
hand-written, parameterized SQL — the "SQL helper + query package" pattern requested,
short of full PostgreSQL stored procedures/functions. Schema and seed data moved from
EF Core migrations (`Migrations/*.cs`, an opaque history table) to two plain,
reviewable files: `backend/database/schema.sql` and `backend/database/seed.sql`,
applied idempotently on API startup. Controllers kept their existing validation and
business-rule logic essentially unchanged — only the data-access lines changed.
Tests that need a database now run against a real, disposable Postgres database
(`studentconnect_test_db`) instead of EF Core's InMemory provider, since there's no
longer an ORM abstraction to substitute.
