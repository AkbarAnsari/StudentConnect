# StudentConnect

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![Angular](https://img.shields.io/badge/Angular-22-DD0031?logo=angular&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-4169E1?logo=postgresql&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker&logoColor=white)
![License](https://img.shields.io/badge/license-MIT-green)

A multi-tenant school management portal — schools self-register and get their own
isolated data (students, teachers, classes, marks, attendance, and more).

## Features

- **Roles**: Admin, Teacher, Student, Parent — each with their own dashboard and permissions
- **Academic structure**: grades, subjects, sections, academic years, terms, teacher-subject assignments
- **Attendance** and **marks/assessments**, entered by teachers, viewable by students/parents
- **Homework** (with file submissions) and **learning materials**
- **Extracurricular activities** with student registration
- **Notifications** (in-app + email digest) and an **email outbox** with retry
- **Analytics dashboard** (attendance/marks trends, at-risk students) and an **audit log**
- **Report cards**, **transcripts**, and cross-entity **search**
- **Multi-tenancy**: every school's data is isolated by `SchoolId`
- **Auth**: JWT with refresh tokens, email verification, and rate-limited login

## Tech stack

| Layer | Technology |
|---|---|
| Frontend | Angular 22 (standalone components, signals) |
| Backend | ASP.NET Core 10 Web API, plain ADO.NET/Npgsql (no ORM) |
| Database | PostgreSQL |
| File storage | Local disk by default; pluggable Azure Blob Storage |
| Auth | Custom JWT (HMAC-SHA256) |
| Deployment | Docker Compose (local) or Azure (Bicep in `infra/`, not deployed) |

## Repository structure

```
StudentConnect/
├── backend/
│   ├── StudentConnect.Api/              ASP.NET Core Web API + Dockerfile
│   ├── StudentConnect.Application/      DTOs and service interfaces
│   ├── StudentConnect.Domain/           Entities
│   ├── StudentConnect.Infrastructure/   Data access (ADO.NET stores), auth, email, storage
│   ├── StudentConnect.Api.Tests/        xUnit tests (run against a real Postgres test DB)
│   └── database/                        schema.sql + seed.sql (applied automatically on startup)
├── frontend/                             Angular app + Dockerfile + nginx.conf
├── infra/                                Bicep templates for a possible future Azure deployment
└── docker-compose.yml                    Local db + api + frontend stack
```

## Demo accounts (local dev/seed data, School: "Demo School")

| Username | Password | Role |
|---|---|---|
| `admin1` | `Admin123!` | Admin |
| `teacher1` | `Teacher123!` | Teacher |
| `student1` | `Student123!` | Student |
| `student2` | `Student123!` | Student |

Parent accounts are created by an Admin (Manage Parents) and linked to a child. New
schools can self-register from the login page.

## Running locally

**Database & JWT secret** (once per machine, kept out of the repo via user-secrets):
```bash
cd backend/StudentConnect.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=studentconnect_db;Username=<user>;Password=<password>;SSL Mode=Disable"
dotnet user-secrets set "Jwt:Secret" "<any long random string>"
```
The target database just needs to exist (`CREATE DATABASE studentconnect_db;`) — the API
applies `backend/database/schema.sql` and `seed.sql` automatically on every startup.

**Terminal 1 — API** (`http://localhost:5286`):
```bash
cd backend/StudentConnect.Api
dotnet run --launch-profile http
```

**Terminal 2 — Frontend** (`http://localhost:4200`):
```bash
cd frontend
npm start
```

## Running with Docker

The Docker images are runtime-only (they package a pre-built app, no restore/npm-install
happens inside the build), so build the app on the host first:

```bash
cd backend/StudentConnect.Api
dotnet publish StudentConnect.Api.csproj -c Release -o ../publish --no-self-contained
cd ../../frontend
npm run build
cd ..
docker compose up --build -d
```

Then open **http://localhost:4200**. nginx serves the Angular app and reverse-proxies
`/api/*` to the API container, so no CORS setup is needed. The API is also reachable
directly at `http://localhost:5286`. All three services (Postgres, API, frontend) run
in containers; re-run the two build commands above and `docker compose up --build -d`
again after making code changes.

## Tests

```bash
cd backend
dotnet test
```
Requires a second, disposable Postgres database (`studentconnect_test_db` by default,
or point `STUDENTCONNECT_TEST_CONNECTION_STRING` at one).

## License

MIT — see [LICENSE](LICENSE).

