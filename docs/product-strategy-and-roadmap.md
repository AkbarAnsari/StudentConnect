# StudentConnect — Product Strategy & Project Plan
### An independent Product Management review (industry/SaaS-SIS lens)

**Reviewer stance:** this document deliberately treats StudentConnect as a *commercial
product candidate*, not as the "Azure Fundamentals learning project" it was originally
built as. Every gap below is measured against what a school actually needs to run, and
what a buyer (school owner/principal/trust) would expect from a paid School Information
System (SIS) in 2026. Where the existing engineering is genuinely strong, this document
says so explicitly — the goal is an honest gap list, not a rewrite.

---

## 1. Executive Summary

**What exists today** is a surprisingly solid **single-school, single-tenant academic
core**: Student/Teacher/Parent/Admin roles, attendance, marks/assessments, homework,
learning materials, activities, notifications (in-app + async email), search, audit
log, analytics, report cards, a homeroom-teacher "My Classes" view, and real
engineering hygiene (24 backend tests, CI, a layered Domain/Application/Infrastructure
architecture, health checks, IaC prepared in Bicep). That is genuinely more than most
early-stage SIS MVPs ship with.

**What's missing is everything that turns "a school's internal tool" into "a product
you can sell to many schools."** There is no multi-tenancy, no billing, no fee/finance
module, no admissions funnel, no timetable engine, no mobile app, no SMS/WhatsApp, no
compliance posture for handling children's data, and no deployed environment with a
single real customer.

**Verdict:** treat what exists as **Horizon 0 — the academic-core proof of concept**.
It validates that the team (or agent-assisted workflow) can ship working, tested
features fast. The path to an actual product runs through three more horizons below,
starting with the single highest-leverage, hardest-to-retrofit decision: **multi-tenancy**.

---

## 2. Current State Audit — what's actually built

### 2.1 Capability inventory by persona

| Persona | Capabilities (frontend routes → backend endpoints) |
|---|---|
| **Student** | View profile, view attendance (overall + per-subject), view marks (overall + per-subject), view/submit homework, view/download learning materials, browse & register for activities, view/print report card, receive & read in-app notifications |
| **Parent** | Everything a Student can do, scoped to each linked child (`CanAccessStudent` ownership check); no parent-specific views beyond a child switcher on the dashboard |
| **Teacher** | Record attendance (single + bulk-by-class), enter marks (single + bulk), create/manage assessments, create/manage homework + view submissions, upload/manage learning materials, create/manage activities + view registrations, "My Classes" (homeroom roster + per-section performance stats), class performance report, analytics dashboard, cross-entity search |
| **Admin** | All Teacher capabilities, plus: Student CRUD, Teacher CRUD (+ optional login provisioning), Parent CRUD (+ child linking, + optional email for notifications), Academic Structure CRUD (Grades, Subjects, AcademicYears, Terms, Sections + homeroom-teacher assignment), Audit Log viewer, Email Outbox viewer, Operations/health page, analytics dashboard |
| **Platform (no UI persona yet)** | JWT auth (local database, not a cloud IdP), health checks (`/health`), ops snapshot (`/api/ops/info`), async email outbox + background processor, Postgres full-text search, append-only audit log |

### 2.2 Quantified footprint

- **~101 REST endpoints** across 24 controllers (Students, Teachers, Parents, Grades,
  Sections, Subjects, AcademicYears, Terms, Attendance, Assessments, Marks, Homework,
  HomeworkSubmissions, LearningMaterials, Activities, ActivityRegistrations,
  Notifications, Analytics, Reports, Search, AuditLogs, EmailOutbox, Ops, Auth).
- **~45 Angular routes**, route-guarded by authentication + role.
- **24 xUnit tests**, run against a real Postgres test database in CI (GitHub Actions).
- **4-project backend architecture** (`Domain` / `Application` / `Infrastructure` /
  `Api`) — a real separation of concerns, not just a folder convention.
- **Bicep IaC prepared** (Postgres Flexible Server, Blob Storage, App Service, Static
  Web App, Key Vault + managed identity, App Insights) — **compiled clean, never
  deployed.** Zero real users, zero production traffic, zero uptime history.

### 2.3 What's genuinely good (don't lose this in the rewrite)

- Backend never trusts client input for authorization — every ownership check
  (`CanAccessStudent`, `CanActAsTeacher`) happens server-side against JWT claims.
- Async email via a real outbox + background processor pattern (not naive
  fire-and-forget), with visible delivery status.
- Append-only audit log on the mutations that matter (student edits, marks, attendance,
  account/permission changes).
- A deliberate, documented decision trail (see `docs/phase-15-roadmap.md`) — gaps were
  identified honestly *before* being built, which is rare and valuable discipline.

---

## 3. Market & Competitive Landscape

### 3.1 Who else sells this

| Segment | Established players | Typical price point |
|---|---|---|
| Enterprise K-12 / district (US) | PowerSchool, Infinite Campus, Blackbaud, Skyward | $8–$25 per student/year, multi-year contracts |
| Private schools (US/global) | Veracross, Alma, Gradelink, Sycamore | $3–$10 per student/year |
| Emerging-market low-fee private & NGO schools | Teachmint, Entab, Fedena, Frappe School, MySchoolWorx | $0.50–$3 per student/year, or free/grant-funded tiers |
| LMS-adjacent (not full SIS) | Google Classroom, Schoology, ClassDojo | Free or bundled with Workspace/district licenses |

### 3.2 Positioning signal from the codebase itself

The workspace lives at `NGO\Student-Portal`, seed data uses Indian names and an
INR-adjacent grade structure ("Grade 8/9/10", `Section A/B`), and every feature so far
is what a **single school administrator** would need — not a district. This strongly
suggests the real opportunity is:

> **"An affordable, WhatsApp/SMS-friendly school operating system for NGO-run,
> community, and low-fee private schools in emerging markets — a segment enterprise
> SIS vendors don't compete for, and that currently runs on Excel + WhatsApp groups."**

This is a *recommendation*, not a confirmed decision — flagged again in §9 as the
first thing to validate with real stakeholders before committing engineering budget to
a specific horizon.

---

## 4. Gap Analysis

Each gap is rated **Priority** (P0 = blocks selling to a second customer at all, P1 =
blocks retention/expansion, P2 = differentiator/competitive parity) and **Effort**
(S/M/L/XL, roughly days/1-2 weeks/1+ month/multi-month).

### A. Platform & SaaS-readiness gaps (the actual blockers)

| Gap | Why it matters | Priority | Effort |
|---|---|---|---|
| **No multi-tenancy** — every table is global, no `SchoolId`/`TenantId` anywhere | Cannot onboard a second school without data leaking between them. This is the single highest-leverage architecture decision left, exactly like the Section/AcademicYear model was in Phase 15 | **P0** | XL |
| **No self-service onboarding** — accounts are seeded via SQL only | A product needs "sign up → create school → invite admin" without an engineer running SQL | **P0** | M |
| **No subscription/billing** — no Stripe/Razorpay, no plans, no seat/student limits | There is no way to charge anyone today | **P0** | M |
| **No platform-admin console** — nobody can see/manage tenants, plans, or usage across schools | Needed the moment there are 2+ customers | **P0** | M |
| **Never deployed** — Bicep exists but no live environment, no uptime history | Zero evidence the product survives real traffic | **P0** | S (infra already written) |
| No white-labeling (school logo/branding/subdomain) | Schools expect their own identity, not "StudentConnect" branding | P1 | M |
| No feature flagging per plan/tenant | Needed to sell tiered plans (e.g., Transport module only on higher tier) | P1 | M |

### B. Financial / billing module (the biggest *feature* gap)

| Gap | Why it matters | Priority | Effort |
|---|---|---|---|
| No fee-structure management (fee heads, per-grade fee plans, scholarships/discounts) | Fee collection is the #1 reason schools buy an SIS in this segment | **P0** | L |
| No invoicing / online fee payment (Razorpay/Stripe/PayPal) | Parents expect to pay fees through the portal, not in person | **P0** | L |
| No payroll/staff compensation | Common secondary ask, not a blocker | P2 | L |
| No expense/accounting ledger | Same | P2 | L |

### C. Academic/SIS core-feature gaps

| Gap | Why it matters | Priority | Effort |
|---|---|---|---|
| No timetable/scheduling engine (weekly periods per section/subject/teacher, conflict detection) | Every school runs on a timetable; currently attendance/marks/homework reference a subject with no actual schedule | **P0** | L |
| No admissions/enrollment funnel (inquiry → application → entrance test → offer → enroll) | Students are admin-created directly; no pipeline for the school's actual recruiting season | P1 | L |
| No year-end promotion/rollover workflow (bulk-promote a section to the next grade/year) | Manual re-entry every academic year otherwise | **P0** | M |
| No certificate generation (Transfer Certificate, Bonafide, character certificate) | Routine, frequent admin request at every school | P1 | M |
| Exam management is "Assessments + Marks" only — no exam-term scheduling, seating plans, hall tickets, grading scales (GPA/percentile), or transcripts | Report cards work, but exam *operations* aren't covered | P1 | L |
| No library management | Common module in this segment (issue/return/fines) | P2 | L |
| No transport management (routes/buses) | Valuable but reasonably deferred as a paid add-on | P2 | XL |
| No hostel/dormitory management | Only relevant for boarding schools — niche | P2 | L |
| No discipline/behavior or health/immunization tracking | Increasingly expected, not urgent | P2 | M |
| No alumni management | Nice-to-have, not urgent | P2 | M |

### D. Communication gaps

| Gap | Why it matters | Priority | Effort |
|---|---|---|---|
| No SMS gateway integration | In the target market, SMS reaches parents who don't check email or lack a smartphone | **P0** | S |
| No WhatsApp Business API integration | The dominant parent-communication channel in India/Africa/LatAm; bigger differentiator than a native app | **P0** | M |
| No broadcast circulars/announcement board (today's Notifications are 1:1 transactional only) | Schools need "send to all parents of Grade 8" in one click | P1 | M |
| No parent–teacher meeting scheduling | Frequently requested workflow | P1 | M |
| No two-way parent↔teacher messaging/chat | Reduces WhatsApp-group dependency | P2 | L |

### E. Mobile & accessibility gaps

| Gap | Why it matters | Priority | Effort |
|---|---|---|---|
| No mobile app (native or PWA) — Angular web only | Most parents in the target segment are mobile-first; a desktop-only portal will suppress adoption | **P0** | L (PWA) / XL (native) |
| No offline tolerance | Target markets have inconsistent connectivity | P1 | M |
| No i18n/multi-language UI | English-only excludes many target-market parents/teachers | P1 | M |
| No accessibility (WCAG 2.1 AA) audit | Both an ethical baseline and a requirement for public/government contracts | P2 | M |

### F. Compliance, privacy & trust gaps (children's data — non-negotiable before scale)

| Gap | Why it matters | Priority | Effort |
|---|---|---|---|
| No Privacy Policy / Terms of Service in-product | Legally required anywhere this collects a minor's PII | **P0** | S |
| No verifiable parental-consent flow (COPPA-style) or DPDP Act (India, 2023) alignment | Handling children's names, attendance, grades, contact info without a documented lawful basis is a real legal risk, not a "nice to have" | **P0** | M |
| No data retention/deletion ("right to be forgotten") process | Same | P1 | M |
| No documented encryption-at-rest/in-transit posture or security review/pen-test | Needed before any serious school trust/district will sign | P1 | M |
| No SOC 2 / ISO 27001 track | Blocks larger institutional customers later, not the first 10 schools | P2 | XL (org-level, not just engineering) |

### G. Operational-readiness gaps

| Gap | Why it matters | Priority | Effort |
|---|---|---|---|
| No customer support channel (Contact page is static, no ticketing) | Every early customer will need a way to reach a human | **P0** | S |
| No data import tool (CSV bulk upload for students/teachers) | Schools switching from Excel need a migration path, not manual re-entry | **P0** | M |
| No data export/portability for a school's own data | Retention/compliance requirement, also a trust signal | P1 | S |
| No staging environment distinct from local dev | Currently "prod" and "dev" are the same laptop | **P0** | S (infra ready) |
| No backup/disaster-recovery procedure (schema/seed reapply ≠ backup) | One bad migration away from real data loss once there are real customers | **P0** | M |
| No rate limiting / API abuse protection | Public-facing API today has none | P1 | S |
| No load/performance testing at any point | Unknown behavior above trivial demo data volumes | P1 | M |

### H. Analytics/reporting gaps

| Gap | Why it matters | Priority | Effort |
|---|---|---|---|
| No CSV/Excel/PDF export of raw data (only the printable report card exists) | Admins routinely need data out of the system, not just dashboards | P1 | S |
| No multi-year trend/cohort comparison | Current analytics is current-state only | P2 | M |
| No BI/Power BI or Fabric integration | Correctly deferred already (see phase-15 roadmap) — still a gap for larger customers | P2 | L |

### I. Integration/extensibility gaps

| Gap | Why it matters | Priority | Effort |
|---|---|---|---|
| No public API/webhooks | Blocks any third-party integration story | P2 | M |
| No SSO (Google Workspace / Microsoft 365) | Reasonable to defer — local auth was an explicit, deliberate decision — but larger schools will eventually ask | P2 | L |
| No LTI/Google Classroom interoperability | Nice-to-have, not urgent for the target segment | P2 | L |
| No calendar (iCal) export/sync | Cheap, useful | P2 | S |

---

## 5. Recommended Product Vision

> **"StudentConnect: the affordable, WhatsApp-first school operating system for
> NGO-run, community, and low-fee private schools — everything a school needs to run
> its academic year and collect fees, at a price a $2/student/year budget can afford."**

This recommendation is built from evidence in the repository (naming, structure,
folder location), not confirmed with a real stakeholder — **validate this before
committing a roadmap budget to it** (see §9, Immediate Next Steps, item 1).

### Candidate business models (need a decision, not a default)

1. **Per-student/year subscription** (~$1–3), tiered by feature set (Starter = academic
   core, Growth = + fees/finance + SMS, Pro = + transport/library/multi-branch).
2. **Free/subsidized tier funded by grants or CSR sponsorship** for schools under a
   student-count threshold — plausible given the NGO-adjacent positioning, and a
   realistic path to initial adoption without a sales team.
3. **Flat per-school annual fee** with student-count bands — simpler to sell, harder to
   scale margins.

---

## 6. Prioritized Roadmap

### Horizon 1 (0–3 months) — "Productize the Core" — P0, blocks any second customer

| Epic | Outcome |
|---|---|
| Multi-tenancy retrofit | Every table gets `SchoolId`; all queries tenant-scoped; JWT carries tenant claim |
| Self-service school onboarding | Sign up → create tenant → seed default Grades/Subjects → invite first Admin |
| Subscription/billing skeleton | Stripe or Razorpay, plan definitions, student-count limits enforced |
| Platform-admin console | Super-admin view across tenants: usage, plan, support flags |
| Fee/finance MVP | Fee heads, per-grade fee plans, invoice generation, manual payment recording |
| Data import tool | CSV bulk upload for Students/Teachers with validation report |
| Privacy Policy, ToS, consent capture | In-product pages + signup-time acceptance + parental consent flow |
| Deploy to a real staging environment | Apply the existing Bicep, stand up CI/CD deploy, first real uptime data |
| Backup/DR procedure | Automated Postgres backups + a tested restore drill |
| Support channel | At minimum a ticketing inbox (Freshdesk/Zendesk/email alias) linked from the Contact page |

**Exit criteria:** a second, independent school can sign up, onboard its own data, and
run a full attendance/marks/homework cycle without an engineer touching the database.

### Horizon 2 (3–6 months) — "Reach & Engage"

| Epic | Outcome |
|---|---|
| SMS + WhatsApp Business API integration | Attendance/fee/homework notifications reach parents on the channel they actually use |
| Timetable/scheduling engine | Weekly period-based schedule per section, conflict detection |
| Broadcast circulars/announcements | "Send to all parents of Grade 8" in one action |
| Parent–teacher meeting scheduling | Slot booking, reminders |
| Online fee payment | Real payment gateway checkout, not just invoice recording |
| Year-end promotion/rollover workflow | Bulk-promote a section/grade at year-end |
| Certificate generation | Transfer Certificate, Bonafide, character certificate templates |
| PWA polish + push notifications | Installable, offline-tolerant, mobile-first experience |
| i18n (at least one additional language relevant to the target market) | Removes an adoption barrier for non-English-first staff/parents |

**Exit criteria:** a school can run an entire academic year — including fee collection
and promotion — without leaving the product.

### Horizon 3 (6–12 months) — "Scale & Differentiate"

| Epic | Outcome |
|---|---|
| Admissions/enrollment funnel | Inquiry → application → entrance test → offer → enroll |
| Multi-branch/multi-campus support | One account manages several schools under one NGO/trust — a strong differentiator for the recommended segment |
| Library management | Catalog, issue/return, fines |
| Transport management (paid add-on) | Routes, buses, basic tracking |
| Public API + webhooks | Enables partner integrations |
| Advanced analytics + exportable custom reports | Multi-year trends, CSV/Excel/PDF export |
| Security/compliance hardening track | Pen-test, encryption review, SOC 2 readiness roadmap |
| Alumni management | Retention/community feature |

### Ongoing/parallel hygiene track (every horizon)

- Expand automated test coverage (add E2E, load/performance testing).
- Rate limiting, API versioning, request/response schema stability.
- Accessibility (WCAG 2.1 AA) audit and remediation.
- Formal design system (currently per-component CSS).

---

## 7. Execution Plan

### 7.1 Suggested team shape (minimum viable team to execute Horizon 1)

| Role | Allocation | Focus |
|---|---|---|
| Product Manager | 1, full-time | Backlog, customer validation, pricing/packaging decisions |
| Backend engineer | 2, full-time | Multi-tenancy retrofit, billing integration, fee module |
| Frontend engineer | 1–2, full-time | Onboarding flow, platform-admin console, fee/payment UI |
| QA/automation | 1, full-time or shared | Regression suite growth, tenant-isolation test coverage |
| DevOps/security (part-time) | 0.5 | Deploy the prepared Bicep, backups, rate limiting, basic pen-test |
| Designer (part-time) | 0.5 | Onboarding UX, mobile-first pass, design system seed |

### 7.2 Timeline (rough, sprint-based, 2-week sprints)

| Sprint | Focus |
|---|---|
| 1–2 | Multi-tenancy design spike + migration plan (schema, JWT claim, query-scoping strategy) |
| 3–5 | Multi-tenancy implementation across all controllers/stores |
| 6 | Self-service onboarding flow |
| 7 | Billing skeleton (Stripe/Razorpay) + plan/limit enforcement |
| 8 | Fee/finance MVP |
| 9 | Data import tool + backup/DR drill |
| 10 | Deploy to staging via existing Bicep; support channel; privacy/ToS/consent |
| 11–12 | Stabilization, first real pilot school onboarded, retro → re-plan Horizon 2 |

*(~6 months for Horizon 1 as scoped above with a lean team; compress with more
engineers, don't compress by cutting the compliance/backup items — those are the ones
that cause irreversible damage if skipped.)*

### 7.3 Key milestones

1. **M1 — Multi-tenant architecture live** (internal): two fake tenants coexist with
   provably isolated data.
2. **M2 — First self-service signup completes end-to-end** (internal or friendly pilot).
3. **M3 — First real school pilot onboarded** on staging with real (anonymized-if-needed)
   data, running a live attendance/marks cycle.
4. **M4 — First paid invoice collected** through the product (even if payment recording
   is manual at this stage).
5. **M5 — Production deployment with a support channel and backup/DR drill completed.**

---

## 8. Success Metrics

| Category | Metric |
|---|---|
| Business | # schools onboarded, # active students, MRR/ARR, gross churn, CAC (even if $0 initially via direct outreach) |
| Product adoption | DAU/MAU by role, % of teachers using bulk-attendance vs. manual, homework submission rate, report-card downloads |
| Support/trust | Support ticket volume & time-to-resolution, NPS from admins and teachers |
| Technical | Uptime %, p95 API latency, deployment frequency, test coverage %, MTTR on incidents |

---

## 9. Immediate Next Steps (do these before writing more code)

1. **Validate the target market/positioning** in §5 with whoever actually owns this
   project — is this genuinely for an NGO network of schools, a single school, or a
   general-purpose resellable SaaS? The entire roadmap ordering depends on this answer.
2. **Decide the business model** (per-student subscription vs. grant-funded free tier
   vs. flat fee) — affects how billing is built in Horizon 1, not just a later
   marketing decision.
3. **Start the multi-tenancy design spike** — this is the Phase-15-Academic-Model
   equivalent for this next stage of the project: the single decision every other
   Horizon-1 epic depends on, and the most expensive one to retrofit later if gotten
   wrong.
4. **Stand up the already-written Bicep as a real staging environment** — the fastest,
   lowest-risk way to start generating real operational evidence (uptime, performance,
   actual bugs under real network conditions) while the multi-tenancy work is underway.
5. **Publish a Privacy Policy and Terms of Service**, even a first draft — this product
   already handles children's names, grades, and attendance; that data is not
   hypothetical once any real school pilots it.

---

## 10. Risks & Open Questions

- **Market not yet validated** — every priority in §6 assumes the Option-A positioning
  in §3.2/§5; if the real target is a single school (not a resellable product), most of
  §6 Horizon 1 (multi-tenancy, billing, platform-admin) is unnecessary and should be
  descoped entirely.
- **Multi-tenancy is a major, high-risk retrofit** — comparable in scope to the
  Clean Architecture refactor already completed, but riskier: a missed tenant-scoping
  check anywhere is a data-leak bug, not just a compile error.
- **Compliance requirements are jurisdiction-specific** — FERPA/COPPA (US), DPDP Act
  (India), GDPR (EU) impose materially different obligations; needs a real legal review
  once the target market (§9, item 1) is confirmed, not just an engineering checklist.
- **No resourcing exists today beyond the current build process** — the team-shape in
  §7.1 is aspirational; the realistic near-term plan may be "one person/agent,
  sequenced Horizon-1 epics one at a time," which changes the timeline in §7.2
  proportionally.
