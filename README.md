# StudentConnect

A beginner-friendly, cloud-bound student learning and school management portal, built
incrementally as a teaching project for an Azure Fundamentals program. See
[StudentConnect.md](StudentConnect.md) for the full blueprint (roles, phases, Azure
mapping) and `docs/phase-*.md` for what each phase taught and built.

## Repository structure

```
StudentConnect/
├── frontend/     Angular app (studentconnect UI)
├── backend/      ASP.NET Core Web API (StudentConnect.Api)
│                 includes Dockerfile (Phase 12, container packaging)
├── docs/         Phase-by-phase explanations
│   └── labs/     Hands-on reasoning exercises
└── StudentConnect.md   Living project blueprint / Copilot instructions
```

## Architecture

### Phase 12 (previous)

Conceptual compute/hosting overview + a Dockerfile for the API (never deployed). See
[docs/phase-12.md](docs/phase-12.md).

### Phase 13 (current) — Azure Storage & Data

Extends `IFileStorage` (Phase 8) with a real **`AzureBlobStorage`** implementation
alongside the existing `LocalFileStorage`, selected purely via configuration
(`Storage:Provider` = `Local` | `AzureBlob`) — no controller or Angular code knows or
cares which one is active. This part was **implemented and live-tested against a real
Azure subscription**: a storage account + private `learning-materials` container were
created, and upload/download/delete were verified end-to-end through the actual
running API using Azure AD auth (`DefaultAzureCredential`) — no storage key or
connection string anywhere in the code. The database side of this phase (Azure
Database for PostgreSQL) is **documented only, not provisioned** — the app continues
running against local PostgreSQL by default. See
[docs/phase-13.md](docs/phase-13.md), [docs/azure-storage-glossary.md](docs/azure-storage-glossary.md),
[docs/data-storage-decision-guide.md](docs/data-storage-decision-guide.md),
[docs/labs/phase-13-storage-data.md](docs/labs/phase-13-storage-data.md), and the
updated [docs/studentconnect-cloud-architecture.md](docs/studentconnect-cloud-architecture.md).

```
ASP.NET Core API
   ├── PostgreSQL (local; Azure Database for PostgreSQL documented, not provisioned)
   └── IFileStorage
          ├── LocalFileStorage   (default)
          └── AzureBlobStorage   (real Azure resource — see docs/phase-13.md)
```

### Phase 12

Conceptual compute/hosting overview + a Dockerfile for the API (never deployed). See
[docs/phase-12.md](docs/phase-12.md),
[docs/azure-compute-decision-guide.md](docs/azure-compute-decision-guide.md),
[docs/labs/phase-12-compute-hosting.md](docs/labs/phase-12-compute-hosting.md).

Conceptual Phase 12 hosting target (not deployed):

```
Browser
   ↓
Azure Static Web Apps   (Angular)
   ↓
HTTPS
   ↓
Azure App Service       (ASP.NET Core API)
   ↓
Current database/storage (local PostgreSQL + local file storage)
```

**Database update:** the API now runs against a local PostgreSQL database
(`studentconnect_db`, EF Core via `Npgsql.EntityFrameworkCore.PostgreSQL`) instead of
SQL Server LocalDB — same EF Core models/migrations workflow, just a different
relational engine. See the connection string in `appsettings.json` and the
"Running locally" section below.

**Docker note:** a Dockerfile was created and reviewed against the actual project, but
could not be built/run in this environment because the local Docker Desktop background
service wasn't running — see `docs/phase-12.md` for the exact commands to use once it
is. No Azure SQL, Blob Storage, Entra ID, Key Vault, Application Insights, CI/CD, or
Kubernetes were introduced.

### Phase 10

```
                         Browser
                            │
                            ▼
                       Angular App
                            │
                     Login / Token
                            │
                            ▼
                  ASP.NET Core Web API
                            │
                 Authentication
                            │
                 Authorization
                            │
                ┌───────────┴───────────┐
                │                       │
                ▼                       ▼
          SQL Server              Local File Storage
```

### Phase 11 (current) — Introduction to Cloud Computing & Azure

Phase 11 is **conceptual only — no application code changed**. StudentConnect still
runs exactly as it did at the end of Phase 10, entirely locally. This phase adds
learning material that explains why cloud computing exists, what Azure provides, and
how to map an application requirement to a cloud capability *before* picking an Azure
service name:

```
Application requirement → Capability → Architecture decision → Azure service
```

Covers: traditional IT vs. cloud computing, cloud characteristics (on-demand,
elasticity, broad network access, resource pooling, measured consumption), CapEx vs.
OpEx, IaaS/PaaS/SaaS/serverless, shared responsibility, Azure core concepts
(subscription → resource group → resource), region vs. availability zone, the Azure
Portal, and cost awareness (cloud ≠ free). Includes a conceptual (not implemented)
future architecture mapping StudentConnect's current pieces to Azure capabilities —
see [docs/phase-11.md](docs/phase-11.md), [docs/cloud-glossary.md](docs/cloud-glossary.md),
and [docs/studentconnect-cloud-architecture.md](docs/studentconnect-cloud-architecture.md).

No Azure SDK packages, Azure configuration, Azure resources, Microsoft Entra ID, Azure
SQL, or Blob Storage were added — those remain future phases.

### Phase 9

```
                                         +---- SQL Server (structured data)
Browser → Angular → HTTP → ASP.NET Core API
                                         +---- Local File Storage (learning material files)
```

Phase 10 adds **Identity & Access**: local JWT-based authentication (no Microsoft Entra
ID yet), three roles (`Student`, `Teacher`, `Admin`), and role/ownership-based
authorization enforced on every API endpoint. A student can view their own attendance,
marks, homework, and learning materials, and register themselves for activities — but
cannot view another student's data or perform teacher/admin actions, even by editing a
request. Endpoints that previously accepted a client-supplied `studentId` for
"self" actions (submitting homework, registering for an activity) now derive the
student from the authenticated token instead. Angular has a login page, an
`AuthService` + HTTP interceptor (attaches the bearer token automatically), route
guards (`authGuard`, `roleGuard`), a role-aware dashboard/nav, and logout. Passwords are
hashed with ASP.NET Core's `PasswordHasher<T>` (never stored or returned in plaintext).
No Microsoft Entra ID and no Azure yet — see [docs/phase-10.md](docs/phase-10.md),
[docs/phase-9.md](docs/phase-9.md), [docs/phase-8.md](docs/phase-8.md),
[docs/phase-7.md](docs/phase-7.md), [docs/phase-6.md](docs/phase-6.md),
[docs/phase-5.md](docs/phase-5.md), [docs/phase-4.md](docs/phase-4.md), and
[docs/sql-basics.md](docs/sql-basics.md).

### Development accounts (local learning/testing only)

| Username | Password | Role |
|---|---|---|
| `student1` | `Student123!` | Student (Ananya Sharma) |
| `student2` | `Student123!` | Student (Rahul Verma) |
| `teacher1` | `Teacher123!` | Teacher (Priya Mehta) |
| `admin1` | `Admin123!` | Admin |



## Running locally

Terminal 1 — API (applies pending EF Core migrations automatically on startup):
```bash
cd backend/StudentConnect.Api
dotnet run --launch-profile http
```
Runs at `http://localhost:5286`. Requires a local PostgreSQL server reachable at the
connection string in `appsettings.json` (`Host=localhost;Port=5432;Database=studentconnect_db;...`)
— the database itself is created once (`CREATE DATABASE studentconnect_db;`) and its
schema is kept up to date automatically by EF Core migrations on startup.

Terminal 2 — Frontend:
```bash
cd frontend
npm start
```
Runs at `http://localhost:4200`.

Both must be running for the Students pages to load data.

By default, uploaded learning materials are stored on local disk
(`Storage:Provider = Local`). To run against the real Azure Blob Storage account
created for Phase 13 instead (requires `az login`), set two environment variables
before `dotnet run` — see [docs/phase-13.md](docs/phase-13.md):
```powershell
$env:Storage__Provider = "AzureBlob"
$env:AzureBlobStorage__AccountUrl = "https://<account>.blob.core.windows.net/"
```

### Database commands (only needed after model changes)

```bash
cd backend/StudentConnect.Api
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

