# Phase 12 — Azure Compute & Application Hosting

This phase moves from *"I understand what cloud computing is"* (Phase 11) to
*"I understand where my application can run in Azure and why I'd choose one hosting
option over another."* **No production deployment happens in this phase** — the goal is
understanding the hosting problem before picking an Azure product name.

```
Application requirement → Capability → Architecture decision → Azure service
```

## 1. What is compute?

> Compute is the processing capability used to execute software — CPU, memory, an
> operating system, a runtime, and the running application process.

For StudentConnect:

```
Angular       → the browser executes the frontend code
ASP.NET Core  → compute executes the C# application
```

## 2. Where does an application run?

Ask: *"Where is the ASP.NET Core API actually running right now?"* Today, it's
`dotnet run` on a developer's own machine. That's fine for learning, but a laptop can be
turned off, can fail, has limited capacity, has changing internet connectivity, and
other people can't reliably depend on it — it isn't production infrastructure.
**StudentConnect needs a reliable place to run.** That need is compute/hosting.

## 3. Hosting options, in general (not yet Azure-specific)

```
Option 1 — Physical server
Option 2 — Virtual machine
Option 3 — Managed application platform
Option 4 — Container platform
Option 5 — Serverless/event-driven compute
```

### Physical servers

```
Physical Server → Operating System → .NET Runtime → ASP.NET Core → StudentConnect API
```

The organization manages the hardware, OS, networking, runtime, application, patching,
and capacity — traditional infrastructure, in full.

## 4. Virtual machines

> A virtual machine is a software-defined computer that behaves like a computer with
> CPU, memory, storage, and an operating system.

```
Physical Server
 ├── Virtual Machine A
 ├── Virtual Machine B
 └── Virtual Machine C
```

For StudentConnect: `Virtual Machine → Windows/Linux → .NET → ASP.NET Core → API`.

**Using a VM does not remove infrastructure management.** We still manage the OS,
patching, the .NET runtime, the application, configuration, and security:

```
VM → Operating System → Patching → .NET Runtime → Application → Configuration → Security
```

A VM is cloud infrastructure — but it is still infrastructure.

## 5. Azure Virtual Machines

```
Need: a virtual computer
Capability: virtualized compute
Azure: Azure Virtual Machines
```

### VM thought experiment

If StudentConnect runs on a VM and 10,000 students arrive at once: CPU may spike,
memory may become constrained, response time may increase, the application may become
overloaded, and another VM may be needed. This introduces **scaling**.

## 6. Scaling

> Scaling means adjusting resources to handle changing workload requirements.

- **Vertical scaling** — increase resources for one machine (2 CPU/4 GB → 4 CPU/8 GB).
  School analogy: one classroom becomes larger.
- **Horizontal scaling** — add more application instances (Instance 1, 2, 3...) and
  distribute traffic across them. School analogy: add more classrooms.

```
Vertical = bigger        Horizontal = more
```

Not every application can scale horizontally without changes (see "Stateless API
concept" below).

## 7. The problem with managing VMs

If Azure can provide the VM, why not just use VMs for everything? Because the customer
still owns OS patching, runtime management, security updates, VM configuration,
scaling, monitoring, availability, backups — real, ongoing infrastructure operations.
This leads to: *what if the cloud provider managed more of this for us?*

## 8. Platform as a Service (PaaS)

> PaaS allows developers to focus more on the application while the cloud provider
> manages more of the underlying infrastructure and platform.

```
Developer → ASP.NET Core application → Managed application platform
                                          → Cloud provider manages more infrastructure
```

## 9. Azure App Service

> Azure App Service is a managed platform for hosting web applications, web backends,
> and REST APIs — Microsoft describes it as a PaaS offering where the underlying
> infrastructure is managed for you.

```
Problem: we need to run an ASP.NET Core web API.
Capability: managed web application hosting.
Azure: Azure App Service.
```

### StudentConnect + App Service

```
Internet → Azure App Service → ASP.NET Core API
```

The application still contains exactly what it does today — controllers, services,
business logic, EF Core, authentication, authorization. **Only the hosting environment
changes.**

### What App Service manages vs. what we still manage

```
Developer
 ├── Application
 ├── Configuration
 ├── Identity integration
 └── Application behavior

Azure
 ├── Hosting infrastructure
 ├── Operating environment
 ├── Platform management
 └── Scaling capabilities
```

Azure does **not** automatically handle every application concern — the customer
remains responsible for application-level security, configuration, data, and
appropriate operations.

### App Service Plan

> An App Service plan defines the compute resources on which App Service apps run,
> including region, operating system, and pricing tier/SKU.

```
App Service Plan
       │
       ├── Web App A
       ├── Web App B
       └── Web App C
```

Multiple apps can share an App Service plan in appropriate scenarios. Important
distinction: **App Service** is the managed hosting *service*; an **App Service Plan**
is the underlying compute resource arrangement those apps run on — not simply "the
server."

## 10. Frontend hosting is a different question

Angular's production build output is primarily static files: HTML, CSS, JavaScript,
images. Angular executes in the browser once those assets are delivered — that's a
different hosting requirement than a running API process.

> Static web hosting serves frontend files to browsers.

### Azure Static Web Apps

> Azure Static Web Apps is designed for modern web applications and supports frontend
> frameworks including Angular. It provides integrated deployment workflows and can
> also support APIs/serverless capabilities.

```
Problem: host the Angular frontend.
Capability: managed static web application hosting.
Azure: Azure Static Web Apps.
```

### StudentConnect frontend architecture (conceptual)

```
Browser
   │
   ▼
Azure Static Web Apps
   │
   │ HTTPS API calls
   ▼
Azure App Service
   │
   ▼
ASP.NET Core API
```

This is the same architecture StudentConnect already has locally (Angular ↔ HTTPS/REST
↔ ASP.NET Core). Cloud hosting changes the *infrastructure*, not the fundamental
responsibilities: Angular owns the UI; ASP.NET Core owns business logic, the API,
security, and data access.

### CORS, revisited

Locally, Angular (`http://localhost:4200`) and the API (`http://localhost:5286`) are
different origins, so CORS must explicitly allow the Angular origin (already configured
in `Program.cs` since Phase 3). In the cloud, the same idea applies with different
URLs — e.g. `https://studentconnect-frontend...` calling
`https://studentconnect-api...`. **`AllowAnyOrigin` is not a production solution.**
CORS is a browser security mechanism controlling which web origins can make certain
cross-origin requests — the API must explicitly allow the intended frontend origin(s).

## 11. Configuration and environment separation

Cloud applications shouldn't hardcode environment-specific values (API URLs, connection
strings, auth configuration, file storage paths). Development uses one set of values;
a future cloud environment would use another:

```
Development → localhost, LocalDB, local file storage
Production   → cloud hosting URLs, managed database, managed storage
```

**Configuration should be externalized from application code.** StudentConnect already
does this partially (`appsettings.json` + `appsettings.Development.json`,
environment-specific secrets are not committed). Azure Key Vault is **not** introduced
in this phase — externalized configuration today just means "not hardcoded in C#", not
a specific secrets-management product.

## 12. Containers

> A container packages an application and its dependencies into a standardized unit
> that can run consistently across environments.

```
Container
 ├── Application
 ├── Runtime/dependencies
 └── Configuration expectations
```

A container is **not** "a lightweight VM" — containers share the host operating
system's kernel while providing process isolation; they use a different isolation model
than a full virtual machine.

### StudentConnect container

```
StudentConnect API → Dockerfile → Container Image → Container → runs ASP.NET Core API
```

The goal is consistency: the same packaged application image can run on a developer's
machine and in a cloud environment.

### The Dockerfile (`backend/StudentConnect.Api/Dockerfile`)

A multi-stage build, matching the actual project (verified: `net10.0`, single API
project, no solution file):

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY StudentConnect.Api.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "StudentConnect.Api.dll"]
```

Stage 1 (`sdk` image) restores and publishes the application; stage 2 (`aspnet` image,
much smaller — no compilers/SDK) only contains the published output plus the ASP.NET
Core runtime needed to *run* it. Port 8080 is a common container convention for ASP.NET
Core (matches what App Service for Containers / Container Apps expect, and avoids
binding a privileged port).

### Local container demonstration — environment limitation

Docker CLI (29.6.1) is installed in this environment, but the **Docker Desktop
background service was not running**, and this phase does not start GUI applications
or elevate permissions to do so. `docker build` was attempted and failed with:

```
failed to connect to the docker API at npipe:////./pipe/dockerDesktopLinuxEngine:
check if the path is correct and if the daemon is running
```

The Dockerfile itself is still created, reviewed against the actual project structure,
and documented below with the exact commands to build/run it once Docker Desktop is
running:

```bash
cd backend/StudentConnect.Api
docker build -t studentconnect-api:local .
docker run -p 8080:8080 studentconnect-api:local
```

**Important teaching point even before running it:** the API calls
`db.Database.Migrate()` on startup (see `Program.cs`), which requires a reachable SQL
Server. The current connection string points at `(localdb)\mssqllocaldb` — Windows
LocalDB, which a Linux container cannot reach without additional network configuration
that Phase 12 deliberately does not implement (see "Database warning" below). Running
this container standalone, with no reachable database, is expected to fail at startup
for that reason — which is itself the lesson: **containerizing the application does not
by itself solve where its database lives.**

## 13. Azure Container Apps

> Azure Container Apps is a managed platform for running containerized applications
> without requiring the customer to manage the underlying infrastructure. It supports
> HTTP workloads, scaling, jobs, and other containerized scenarios.

```
Problem: run a containerized application.
Capability: managed container application platform.
Azure: Azure Container Apps.
```

Kubernetes is intentionally not introduced here.

## 14. App Service vs. Container Apps

| Question | App Service | Container Apps |
|---|---|---|
| Main abstraction | Managed application hosting | Managed container application platform |
| Need to understand containers? | Not necessarily | Yes |
| Good for an ASP.NET Core API? | Yes | Yes |
| Infrastructure management | Low | Low |
| Container-first? | Optional | Yes |
| Advanced container scenarios | Limited compared to container platforms | Stronger fit |

Neither is universally "better" — the choice depends on requirements.

## 15. VM vs. App Service

| | Azure VM | Azure App Service |
|---|---|---|
| Infrastructure control | Higher | Lower |
| OS management | Customer | More managed |
| Application focus | Lower | Higher |
| Operational effort | Higher | Lower |
| Flexibility | Higher | More constrained |
| Typical abstraction | IaaS | PaaS |

> More control usually means more responsibility.

## 16. Three hosting choices, summarized

```
Virtual Machine   → "I want more infrastructure control."
App Service       → "I want managed web application hosting."
Container Apps    → "I want managed hosting for containerized applications."
```

The right answer depends on requirements — not on which service sounds newest.

### Scenario reasoning

- *"Host an ASP.NET Core REST API; the team doesn't want to manage OSes and doesn't
  need complex container orchestration."* → **App Service** is a strong candidate (not
  the only possible answer) — it provides managed hosting for web applications and APIs
  with low infrastructure management overhead.
- *"The team already packages its API as a Docker container and wants managed container
  hosting with scaling."* → **Azure Container Apps** — the team is already
  container-first, so a platform built around that is a natural fit.
- *"A workload needs OS-level control and custom software installed."* → **Azure
  Virtual Machines** — only a VM gives that level of control.

## 17. Architecture decision framework

Before picking a service, ask:

```
1. What application are we running?
2. What runtime does it need?
3. Do we need OS-level control?
4. Do we already use containers?
5. How much infrastructure should we manage?
6. How will it scale?
7. How will it be deployed?
8. What availability is needed?
9. What does it cost?
10. What operational skills does the team have?
```

This is more important than memorizing service names.

## 18. Scaling demonstration (conceptual)

```
10 users       → 1 application instance
1,000 users    → need more capacity
10,000 users   → multiple instances
```

The exact scaling strategy depends on workload, application design, database capacity,
session/state handling, caching, and other architecture decisions — **"just add
servers" is not a universal solution.**

### Stateless API concept

> A stateless API does not rely on a particular server instance remembering the user's
> application state between requests.

```
Request 1 → API Instance A
Request 2 → API Instance B
```

...can work when shared state is stored appropriately outside any individual instance.
StudentConnect's Phase 10 token-based authentication already illustrates this — the JWT
carries the caller's identity/claims on every request, so *any* API instance can
validate it without needing to remember a server-side session. Distributed caching is
not introduced yet.

## 19. Local storage limitation (connecting Phase 8)

StudentConnect's `Local File Storage` (Phase 8) writes uploaded learning materials to
the API's own disk. Ask: *"What happens to uploaded files if the application moves
between instances?"*

```
Instance A → local files
Instance B → different local files
```

Application-local disk is **not** the durable, shared storage solution for a scalable
cloud architecture — this is why local disk storage will eventually be replaced by
Azure Blob Storage (a **future** storage phase, not implemented here).

## 20. Database warning

Similarly, `Local SQL Server` (LocalDB) is not yet a cloud database architecture. This
phase does **not** migrate the database.

```
Application hosting ≠ Database hosting
```

The API can run in one environment while its database runs in a completely different
one — hosting the *application* and hosting its *data* are separate architectural
decisions, each with their own requirements.

## 21. Future cloud architecture (target of this learning progression)

```
                         Internet
                            │
              ┌─────────────┴─────────────┐
              │                           │
              ▼                           ▼
       Static Frontend              API Hosting
       Angular                      ASP.NET Core
              │                           │
              └──────── HTTPS/REST ───────┘
                                          │
                                          ▼
                                   Existing Database
                                   + File Storage
```

Future phases replace the local database and file storage with managed cloud services
(Azure SQL Database, Azure Blob Storage) — not part of Phase 12.

## Student learning summary

- An application needs compute to execute its code.
- Cloud providers offer different ways to provide compute: VMs, managed application
  platforms (PaaS), containers, and serverless.
- We choose a hosting model based on requirements — not because a particular Azure
  service exists.
- App Service is a strong starting point for hosting the ASP.NET Core API; Container
  Apps fits a team already using containers; a VM fits a need for OS-level control.
- Angular's static-asset output has different hosting needs than the API — Azure Static
  Web Apps fits that.
- Scaling can be vertical (bigger) or horizontal (more) — horizontal scaling benefits
  from stateless application design.
- Hosting the application and hosting its data are separate decisions; local disk
  storage and LocalDB do not survive moving between/adding application instances.

## Known limitations (intentionally deferred)

- No production deployment of any kind.
- No Azure SQL Database, no Azure Blob Storage.
- No Microsoft Entra ID implementation.
- No Azure Key Vault.
- No Application Insights.
- No production networking.
- No CI/CD pipelines.
- No Kubernetes.
- The Dockerfile was created and reviewed but could not be built/run in this session
  because the local Docker Desktop background service was not running (see "Local
  container demonstration" above) — the exact commands to do so are documented.

## Next phase

**Azure Storage & Data** — understand cloud storage, relational databases, object
storage, and migrate StudentConnect's local data/storage concepts toward Azure-managed
services. Not implemented yet.
