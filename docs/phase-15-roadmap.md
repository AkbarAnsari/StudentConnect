# Phase 15 — Platform Vision & Gap Analysis (Planning Only)

## Purpose

A comprehensive architectural vision document (reproduced in full at the end of this
file for reference) proposes growing StudentConnect from its current phase-14 state
into a small but complete education platform: ~19-20 business/platform modules, a
richer academic domain model (AcademicYear → Term → Grade → Section → Class →
Subject/Enrollment instead of today's flat `Student.Section` string), a layered
backend (`Domain`/`Application`/`Infrastructure`), a feature-based Angular structure,
and an eventual Azure production topology (App Service, Azure SQL/Blob, Entra ID,
Front Door, Monitor).

**This document is a plan only — nothing described below has been implemented yet.**
It exists to (a) honestly compare the vision against what actually exists today, (b)
break the vision into an ordered, realistic sequence of future phases, and (c) flag
where the vision's own stated principle ("problem before product") argues for
deferring something rather than building it now.

## 1. Current state snapshot (verified against the actual codebase, phase 14 complete)

**Personas implemented**: Student, Teacher, Admin, **Parent** (the vision's core three
plus one already delivered in phase 14 — parents aren't in the vision doc's persona
list but map naturally under "Student" as a dependent/guardian view).

**Domain entities (Postgres, plain ADO.NET — see phase-14 addendum)**: Grade, Subject,
Teacher, Student, StudentSubject, Attendance, Assessment, Mark, Homework,
HomeworkSubmission, LearningMaterial, Activity, ActivityRegistration, ApplicationUser,
ParentStudent, Notification.

**Backend**: `Controllers/` → `Data/Stores/*.cs` (one hand-written-SQL store per
domain area) → `Database.cs` (thin ADO.NET helper). No `Domain`/`Application`/
`Infrastructure` layering yet — it's a pragmatic two-layer app (Controllers +
Stores), which matches the vision's own advice not to force Clean Architecture too
early.

**Frontend**: Angular, route-per-feature-folder already (`students/`, `attendance/`,
`marks/`, `homework/`, `learning-materials/`, `activities/`, `parents/`, `analytics/`,
`notifications/`, `auth/`) — this is already close to the vision's proposed
`core`/`shared`/`features`/`layout` structure, just without the `core`/`shared`
umbrella folders formally named yet.

**Cross-cutting**: JWT auth (local, not Entra ID), role + ownership authorization,
CORS, xUnit tests + GitHub Actions CI, in-app notifications (synchronous DB writes,
not event-driven), an Analytics dashboard (overview stats, attendance/marks by grade,
at-risk students) — this is a real, working start on the vision's "Reporting &
Analytics" module (§16), just without report-card generation or Power BI/Fabric.

**Not implemented at all**: Class/Section as a first-class entity, AcademicYear/Term,
Teacher administration UI (teachers exist as a table but have no CRUD screen —
they're only seeded), Grade/Section/Subject admin CRUD screens (grades/subjects are
seeded, read-only via API), a Search module, an Audit log, an Operations/Health page,
API versioning, rate limiting, correlation IDs, health check endpoints, Microsoft
Entra ID, Azure SQL/Blob in production, and an event-driven/async notification
pipeline (today's notifications are synchronous — acceptable at this data volume, but
not the queue-based pattern the vision describes in §14).

## 2. Gap analysis by vision module

| # | Vision module | Status | Gap |
|---|---|---|---|
| 1 | Identity & Access | Partial | Local JWT works; no Entra ID, no granular permission model beyond 4 roles, no 401/403 dedicated UI pages (currently redirects) |
| 5 | Student module | **Mostly done** | Dashboard/Profile/Attendance/Marks/Homework/Materials/Activities/Notifications all exist. Missing: monthly attendance view, performance summary widgets |
| 6 | Teacher module | Partial | Attendance/Assessment/Marks/Homework/Materials/Activity management all exist. Missing: "My Classes" roster view, submission feedback/comments field, teacher dashboard KPIs |
| 7 | Administration | Partial | Student admin CRUD exists; Parent admin exists (phase 14). Missing: Teacher admin CRUD, Grade/Section/Subject admin CRUD, Class/Section domain, admin dashboard KPIs |
| 8 | Academic Management (AcademicYear→Term→Grade→Section→Class) | **Not started** | Biggest structural gap — today's `Grade`+string `Section` would need to become a real hierarchy |
| 9 | Attendance | Mostly done | Missing: monthly view, low-attendance alerts (notification trigger exists for single absences already; a *percentage-based* alert doesn't) |
| 10 | Assessment & Marks | Mostly done | Missing: grade/ranking calculation, report cards, performance trends over time |
| 11 | Homework | Done | Attachments/file-per-submission not implemented (submissions are text comments only) |
| 12 | Learning Materials | Done | Metadata in Postgres + bytes in local disk/Azure Blob already matches the vision's exact architecture. Missing: versioning, malware scanning, temporary signed URLs |
| 13 | Activities | Done | Missing: waitlist, attendance-at-activity, certificates |
| 14 | Communication & Notification | Partial | In-app notifications exist (phase 14); missing email/push/SMS channels and the async event-worker pattern |
| 15 | Search | **Not started** | No cross-entity search at all yet |
| 16 | Reporting & Analytics | Partial | Analytics dashboard (phase 14) covers overview/at-risk stats; missing dedicated per-student/per-teacher report pages and ETL/analytics-store separation |
| 17 | Administration & Configuration | **Not started** | No settings/configuration admin area |
| 18 | Audit | **Not started** | No audit log table or middleware |
| 19 | Platform/Operations | **Not started** | No health-check endpoint, no ops dashboard |

## 3. Recommended phased roadmap

Ordered so each phase only depends on what's already built, and structural/domain
changes (which are expensive to retrofit) happen before the features that assume them.

### Phase 15 — Academic domain model foundation
The single highest-leverage structural change: introduce `AcademicYear`, `Term`, and a
real `Section` entity (replacing `Student.Section` string), plus admin CRUD screens for
Grades/Sections/Subjects/Teachers that don't exist yet. Doing this **before** more
features are built on top of the current flat model avoids a much larger migration
later.

### Phase 16 — Administration completeness
Teacher admin CRUD, Class/roster view ("My Classes" for teachers), Admin dashboard
KPIs (§7) — straightforward once Phase 15's entities exist.

### Phase 17 — Audit
Append-only audit log (who/what/when/before-after) for the mutations that matter most:
grade/mark changes, attendance corrections, student record edits, permission changes.
Cheap to add now (a single `AuditLog` table + a small logging call from each
mutating endpoint) and valuable evidence for the "enterprise-grade" story.

### Phase 18 — Search
Start with a simple cross-entity Postgres full-text search (`tsvector`) across
Students/Teachers/Homework/Materials/Activities — deliberately **not** Azure AI
Search yet, per the vision doc's own "understand the problem before the service"
principle (§15). Azure AI Search becomes a natural later phase once search exists
and has real query patterns to justify it.

### Phase 19 — Reporting depth
Per-student and per-teacher report pages (not just the admin analytics dashboard),
report-card style output (PDF), grade/ranking calculation. Explicitly **not**
Power BI/Fabric yet (§16's own guidance) — that's a Phase 22+ concern once there's a
real transactional-vs-analytical distinction to teach.

### Phase 20 — Communication depth
Real email delivery (the `INotificationService` abstraction already anticipates this
— a new provider implementing the same interface), plus the synchronous→asynchronous
architecture lesson (§14): introduce a queue (Azure Service Bus, or a simple Postgres
outbox pattern first, to teach the concept before the managed service).

### Phase 21 — Operations & health
`/health` endpoint (ASP.NET Core health checks), a minimal ops-only page (app version,
environment, dependency status), foundation for later Azure Monitor/App Insights wiring.

### Phase 22 — Identity evolution
Microsoft Entra ID as a real identity provider, replacing local JWT — the biggest
auth-related change, deliberately late since it invalidates the current dev-account
seed data story and needs careful planning around the existing Parent/Student/Teacher
account linkage.

### Phase 23 — Clean architecture layering
Only once enough modules exist that the current flat Controllers→Stores shape starts
to strain, split into `Domain`/`Application`/`Infrastructure` per §22 — premature
before then, and the vision doc explicitly agrees ("do not force Clean Architecture
immediately").

### Phase 24 — Azure production topology
App Service + Azure SQL or continue Postgres Flexible Server, Blob Storage (already
proven in phase 13), Entra ID, Front Door, Monitor — the "real deployment" phase,
picking back up the paused Azure hosting work from earlier in this project.

## 4. What this plan deliberately does NOT do yet, and why

- **No Azure AI Search / Power BI / Fabric now** — the vision document itself warns
  against introducing the managed service before the underlying problem is taught;
  Phases 18 and 19 build the plain version first.
- **No Clean Architecture layering now** — same reasoning, explicitly stated in the
  vision doc; premature structure adds friction without enough modules to justify it.
- **No Entra ID migration bundled into near-term phases** — it's disruptive to every
  existing seeded account and auth flow; sequenced late (Phase 22) once the rest of
  the domain is stable.
- **No async/event-driven notifications yet** — today's synchronous notification
  writes are correct and simple at this data volume; the queue-based pattern is
  valuable to teach but only once there's a real throughput reason for it (Phase 20).

## 5. Immediate next action

Recommend starting with **Phase 15** (academic domain model: AcademicYear, Term,
Section entity, Grade/Subject/Teacher admin CRUD) since every later phase in this plan
either depends on it directly (Phase 16 Class/roster views) or benefits from it
existing (Phase 19 reporting needs a real Section to report against). This is a
genuine schema-and-migration-shaped change, not a quick add — worth confirming scope
and getting explicit go-ahead before starting implementation.

---

## Appendix — original vision document (verbatim, for reference)

See the pasted architectural vision provided by the user in this conversation for the
full text (personas, module list, domain model, architecture diagrams). Not
reproduced here to avoid duplicating a very long document; refer to the chat history
of this phase's planning discussion.
