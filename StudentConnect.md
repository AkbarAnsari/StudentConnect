# StudentConnect – GitHub Copilot Instructions

## Project Purpose

StudentConnect is a simple educational student portal used to teach beginners how a modern web application works.

The application is being built as part of an Azure Fundamentals learning program.

The target students have ZERO prior knowledge of:
- Web development
- Frontend/backend concepts
- APIs
- Databases
- Cloud computing
- Azure
- Networking

The application must therefore remain simple, readable, and easy to explain.

## Technology Stack

Frontend:
- Angular
- TypeScript
- HTML
- CSS

Backend:
- C#
- ASP.NET Core Web API
- Entity Framework Core

Database:
- SQL Server locally
- Azure SQL Database in Azure

Cloud:
- Azure App Service
- Azure Blob Storage
- Microsoft Entra ID where authentication is introduced
- Azure Application Insights for monitoring

Source control:
- GitHub

Development environment:
- Visual Studio Code
- GitHub Copilot

## Application

StudentConnect is a small, realistic **school management and learning platform** with three user roles. It is the practical application that students evolve throughout the Azure Fundamentals program — every new feature should introduce a new web/cloud concept and, eventually, a new Azure capability.

### Roles and capabilities (MVP)

**Student**
- Sign in
- View profile
- View subjects
- View attendance
- View marks
- View homework
- Download learning materials
- View school activities

**Teacher**
- Sign in
- View assigned subjects/classes
- Record attendance
- Enter marks
- Create homework
- Upload learning material
- Create/view activities

**Admin**
- Manage students
- Manage teachers
- Manage grades/classes
- Manage subjects
- Assign teachers
- View basic reports

Keep the application intentionally small. Do not add unnecessary features such as:
- Chat
- Payments
- Social networking
- Notifications
- AI features
- Microservices
- Parent portal, homework submission, analytics dashboards, certificates, search

unless explicitly requested — these are optional post-MVP features, not part of the core build.

## Development Phases

Build StudentConnect incrementally. Each phase must produce a working application and introduce one new concept before the next phase begins. Do not skip ahead.

| Phase | Adds | Teaches |
|---|---|---|
| 1 | Static "Hello, Website" pages (Home, About, Students, Contact) | Browser, HTML/CSS/JS, Angular, HTTP, web server |
| 2 | Student profile page with mock/local data | Components, routing, services, models, JSON |
| 3 | C# ASP.NET Core Web API returning student data | Frontend vs backend, REST calls, `GET /api/students/1001` |
| 4 | Real database (SQL Server → Azure SQL) | Tables, keys, relationships, SQL, CRUD |
| 5 | Login for Student/Teacher/Admin | Authentication, authorization, roles, Microsoft Entra ID |
| 6 | Attendance module | Teacher writes data another user (student) later reads |
| 7 | Marks module | Business rules, validation, calculations |
| 8 | Homework module (with file attachments) | Why files don't belong in a database → Blob Storage |
| 9 | Learning materials (per grade/subject) | Files, metadata, containers, access control |
| 10 | Activities + registration | Transactions/workflows, not just read-only data |
| 11 | Full teacher module (classes, homework, marks, materials) | End-to-end write→API→DB→API→read flow |
| 12 | Deploy to Azure | Cloud hosting, App Service, Azure SQL, Blob Storage, Entra ID, Monitor/App Insights |

Deployment itself should be staged (see Azure section): local → Angular hosted → API hosted → database moved → files moved → identity added → monitoring added.

## Architecture

### Local development architecture

```
Browser (Angular)
  ↓ HTTPS
ASP.NET Core Web API
  ↓
Entity Framework Core
  ↓
SQL Server (local)
```

For file uploads:

```
Angular
  ↓
ASP.NET Core API
  ↓
Local file storage (dev) / Azure Blob Storage (prod)
```

### Azure architecture (target end state)

```
Browser (Angular)
  ↓ HTTPS
Azure Static Web Apps / App Service (frontend hosting)
  ↓
Azure App Service — ASP.NET Core Web API
  ↓                              ↓
Azure SQL Database        Azure Blob Storage
(students, marks,         (homework, learning
 attendance, homework       materials)
 metadata, activities)

Microsoft Entra ID  → authentication for all roles
Azure Monitor / Application Insights → logging, metrics, diagnostics
```

## Azure Service Mapping

Introduce an Azure service only when the application has a real requirement for it — never teach a service in isolation.

| Requirement | Technology | Azure capability |
|---|---|---|
| Website UI | Angular | Azure Static Web Apps / App Service |
| Backend API | ASP.NET Core | Azure App Service |
| Student/attendance/marks records | SQL | Azure SQL Database |
| Homework & learning material files | Files | Azure Blob Storage |
| Student/teacher login | Authentication | Microsoft Entra ID |
| Roles (Student/Teacher/Admin) | Authorization | Entra ID / app-level authorization |
| Config/secrets | Configuration | App Service configuration / Key Vault (later) |
| Monitoring | Logs/metrics | Azure Monitor + Application Insights |
| Deployment | CI/CD | GitHub Actions / azd |
| Scaling | Compute | App Service scaling |
| Backup/recovery | Database | Azure SQL backup |

## Development Principles

1. Keep the code beginner-friendly.
2. Prefer simple solutions over clever solutions.
3. Avoid unnecessary abstractions.
4. Avoid premature optimization.
5. Use clear naming.
6. Keep classes and methods small.
7. Add comments where they help a beginner understand the code.
8. Do not hide important logic behind excessive frameworks or abstractions.
9. Explain important architectural decisions.
10. Follow standard security practices.

## API Principles

Use REST-style APIs.

Use appropriate HTTP methods:
- GET
- POST
- PUT/PATCH where appropriate
- DELETE where appropriate

Use appropriate HTTP status codes.

Use DTOs where appropriate, but keep them simple.

Do not expose database entities directly if doing so creates security or maintenance problems.

## Database Principles

Use Entity Framework Core.

Use migrations.

Keep the database model simple and introduce entities only as their phase is reached.

Entities (introduced progressively, phase by phase):

```
Student
Teacher
Grade
Subject
StudentSubject
Attendance
Exam
Mark
Homework
HomeworkSubmission
LearningMaterial
Activity
ActivityRegistration
```

Relationships:

```
Grade
  └── Students
        ├── Attendance
        ├── Marks
        └── Homework

Teacher
  ├── Subjects
  ├── Homework
  ├── Marks
  └── Attendance

Subject
  ├── Learning Materials
  ├── Homework
  └── Exams
```

Relationships should be clear and easy for students to understand.

## Frontend Principles

Use Angular components.

Keep components small.

Use services for API communication.

Use interfaces/types for important models.

Keep UI simple and accessible.

Do not introduce advanced Angular patterns unless necessary.

## Error Handling

The application should handle common errors gracefully.

Do not expose stack traces or sensitive information to users.

Provide simple user-friendly error messages.

## Security

Never hard-code:
- passwords
- API keys
- connection strings
- Azure secrets
- access tokens

Use environment/configuration mechanisms.

When Azure services are introduced, prefer managed identity or secure configuration where appropriate.

## Azure

Do not introduce Azure services unnecessarily.

Always explain:
1. What problem the service solves
2. Why it is needed
3. What alternatives exist
4. How it fits into StudentConnect

The goal is to teach cloud concepts, not memorize Azure products.

## Copilot Behavior

Before making significant architectural changes:
- Explain the proposed change.
- Identify affected files.
- Explain why the change is needed.

When generating code:
- Prefer complete, runnable code.
- Do not modify unrelated files.
- Do not introduce dependencies without explaining why.
- Tell me which commands need to be run.

When something can be implemented in a simpler way, choose the simpler approach.

## Educational Requirement

For important implementation work, explain:

WHAT are we building?

WHY are we building it?

HOW does it work?

WHAT happens behind the scenes?

HOW does it connect to the rest of StudentConnect?

## Important Rule

Do not build the entire application at once.

Implement the application incrementally.

Each stage should produce a working application.