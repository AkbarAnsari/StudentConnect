# StudentConnect

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![Angular](https://img.shields.io/badge/Angular-22-DD0031?logo=angular&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-4169E1?logo=postgresql&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker&logoColor=white)
![License](https://img.shields.io/badge/license-MIT-green)

StudentConnect is a multi-tenant school operations portal. Each school owns an isolated
set of users, students, classes, academic records, communications, files, and reports.

## Current capabilities

- **Five roles:** Admin, Teacher, Student, Parent, and activity-only Coordinator.
- **Identity and access:** school registration, JWT access/refresh rotation, email verification,
	password recovery/invitations, managed contacts, account lifecycle, and active-session revocation.
- **Academic setup:** years, terms and locks, grades, sections, subjects, grading bands,
	Teacher-Subject-Section assignments, Grade/Section curriculum defaults, and controlled rollover.
- **Enrollment:** Student lifecycle, CSV import/export, default subject enrollment, optional login
	invitation, optional guardian link, family relationship types, primary contacts, and link history.
- **Attendance:** Present, Absent, Late, and Excused states; period filters; correction history;
	learner/family leave requests and Admin approval; assignment-scoped keyboard-first entry.
- **Assessments and marks:** Draft/Published release, comments, optimistic versions, append-only
	history, CSV round-trip, class validation, term locks, and spreadsheet-style keyboard movement.
- **Homework:** class targeting, lifecycle/scheduling, learner submissions, file replacement,
	Teacher feedback, review filters, bulk ZIP download, CSV manifest, and due-date operations.
- **Learning materials:** class targeting, topics, scheduling, immutable file versions,
	Draft-only replacement, retained-version download, and local/Azure Blob storage providers.
- **Activities:** lifecycle, Teacher ownership, Coordinator moderation, calendar/list filters,
	capacity and waitlists, consent documents, optional fees, payment reconciliation, check-in,
	participant export, and bulk Registered/Waitlisted communication.
- **Announcements and notifications:** scoped audience preview, Draft editing, scheduling, priority,
	validated attachments, dispatch retries/errors, deep links, read receipts, in-app/email channels,
	digest mode, and UTC quiet hours.
- **Reports and insight:** assignment-scoped analytics, risk thresholds, report templates,
	Grade/Term batch issuance, signatures, download receipts, immutable report-card/transcript history,
	CSV exports, cross-entity search, and tenant-scoped audit history.
- **Operations:** email outbox recovery and ownership, database/delivery health, alert email/webhook
	destinations, retention/redaction controls, runbook links, and an hourly governance processor.
- **Security:** server-side role, tenant, ownership, assignment, learner eligibility, and file-access
	checks. Guessed or foreign object IDs do not grant cross-school access.

## Architecture

| Layer | Technology |
|---|---|
| Frontend | Angular 22 standalone components, signals, RxJS, responsive route-level lazy loading |
| API | ASP.NET Core 10 controllers |
| Application | DTOs and service interfaces |
| Domain | Framework-independent entities and enums |
| Infrastructure | Plain ADO.NET/Npgsql stores, hosted workers, JWT, email, and file storage |
| Database | PostgreSQL; idempotent `schema.sql` and `seed.sql` run at API startup |
| File storage | Local disk by default; Azure Blob Storage via `DefaultAzureCredential` |
| Local deployment | Runtime-only Docker images orchestrated with Docker Compose |
| Azure infrastructure | Bicep templates under `infra/`; deployment is not currently active |

## Repository structure

```text
backend/
	StudentConnect.Api/              API, hosted workers, configuration, Dockerfile
	StudentConnect.Application/      DTOs and interfaces
	StudentConnect.Domain/           entities and enums
	StudentConnect.Infrastructure/   Npgsql stores, auth, email, notifications, storage
	StudentConnect.Api.Tests/        xUnit integration tests
	database/                        schema.sql and seed.sql
frontend/                           Angular application, nginx, runtime Dockerfile
infra/                              Azure Bicep templates
docker-compose.yml                  PostgreSQL, API, and frontend services
```

## Demo accounts

Local seed data creates these Demo School accounts:

| Username | Password | Role |
|---|---|---|
| `admin1` | `Admin123!` | Admin |
| `teacher1` | `Teacher123!` | Teacher |
| `student1` | `Student123!` | Student |
| `student2` | `Student123!` | Student |

Admins create Parent and Coordinator accounts through invitation workflows. Do not reuse
demo credentials outside local development.

## Local development

Configure secrets once:

```powershell
Set-Location backend/StudentConnect.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=studentconnect_db;Username=<user>;Password=<password>;SSL Mode=Disable"
dotnet user-secrets set "Jwt:Secret" "<long-random-secret>"
```

Run the API and frontend in separate terminals:

```powershell
Set-Location backend/StudentConnect.Api
dotnet run --launch-profile http
```

```powershell
Set-Location frontend
npm start
```

Frontend: `http://localhost:4200`
API: `http://localhost:5286`

## Docker deployment

The local network permits host package restore but can block package registries inside
containers. Build artifacts on the host, then package runtime-only images:

```powershell
dotnet publish backend/StudentConnect.Api/StudentConnect.Api.csproj -c Release -o backend/publish --no-self-contained
Set-Location frontend
npm run build
Set-Location ..
docker compose up --build -d
docker compose ps
```

The frontend nginx container serves the SPA and proxies `/api/*` to the API. PostgreSQL data
and local uploaded files use named Docker volumes. Rebuild host artifacts before redeploying
after source changes.

## Validation

```powershell
dotnet build backend/StudentConnect.slnx --no-restore
Set-Location frontend
npm run build
```

The latest verified production build succeeds with an initial Angular bundle of 858.18 kB,
under the 950 kB warning gate. The Docker database, API, and frontend are currently healthy.
Automated test expansion remains deferred; existing tests can be run when that work resumes:

```powershell
dotnet test backend/StudentConnect.slnx
```

Backend integration tests require a disposable PostgreSQL database, configured through
`STUDENTCONNECT_TEST_CONNECTION_STRING` or the default `studentconnect_test_db`.

## License

MIT. See [LICENSE](LICENSE).

