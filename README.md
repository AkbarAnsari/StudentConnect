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
| Azure infrastructure | `infra/vm.bicep` (recommended: single low-cost VM, Postgres in Docker) and `infra/main.bicep` (alternative: managed PaaS — App Service, Postgres Flexible Server, Storage, Key Vault) |

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
infra/                              Azure Bicep templates, one per hosting model (see "Other Azure hosting options")
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

## Azure deployment (cost-optimized: single VM + Docker)

The recommended default is a single small Linux VM running the same three containers as the
local Docker Compose stack (Postgres + API + Angular/nginx) — no Azure Database for
PostgreSQL, App Service, Storage Account, or Key Vault required. This keeps both the number
of Azure services and the monthly bill to a minimum (~$24/month in East US: VM + OS disk +
data disk + static IP), at the cost of owning OS patching and backups yourself (both are
automated for you — see below).

**Provision the VM:**

```powershell
az deployment group create -g <resource-group> -f infra/vm.bicep -p infra/vm.bicepparam `
  -p sshPublicKey="$(Get-Content ~/.ssh/id_ed25519.pub -Raw)"
```

This creates the VM, an NSG allowing 22/80/443 (required — Standard SKU public IPs deny all
inbound by default without one, confirmed via a real deployment where the site was
unreachable until this was added), a static public IP, and a data disk mounted at
`/mnt/pgdata` for Postgres data and uploaded files — independent of the OS disk/container
lifecycle. `infra/cloud-init.yaml` installs Docker, mounts the disk, enables automatic
security patching (`unattended-upgrades`), a host firewall (`ufw`, allowing only 22/80/443 as
defense-in-depth behind the NSG) and a nightly `pg_dump` backup
(14-day retention) as a zero-cost substitute for managed-database backups.

**Deploy the app** (builds locally, same as the Docker workflow above, then ships the
artifacts to the VM over SSH):

```powershell
scp -i ~/.ssh/id_ed25519 .env.example azureuser@<vm-ip>:/opt/studentconnect/.env
ssh -i ~/.ssh/id_ed25519 azureuser@<vm-ip> "nano /opt/studentconnect/.env"   # fill in real secrets
./infra/deploy-vm.ps1 -VmHost <vm-ip> -SshKeyPath ~/.ssh/id_ed25519
```

Re-run `infra/deploy-vm.ps1` after any source change to rebuild and redeploy; it never
deletes data on the VM. For a custom domain + HTTPS, point DNS at the VM's static IP and run
`certbot --nginx` inside the `frontend` container's mounted config, or front the VM with a
reverse proxy that terminates TLS.

**If SSH/SCP is blocked from your machine** (some corporate/home networks block outbound
port 22 while HTTP(S) and the Azure management API work fine), `infra/deploy-vm.ps1` won't be
reachable. Use the run-command-based fallback instead, which ships files entirely through
`az vm run-command` (no port 22 needed, only `az` CLI access):

```powershell
./infra/stage-via-runcommand.ps1 -TarPath <path-to-tarball.tar.gz>
```

This base64-encodes the tarball, transfers it to the VM in ~150,000-character chunks (larger
single invocations silently truncate with no error — verified the hard way), verifies the
transferred size on the remote side before trusting it, then extracts it into
`/opt/studentconnect`. You still need to run your own `docker compose up --build -d` via a
follow-up `az vm run-command invoke` to actually rebuild/restart containers after staging.

For the common case of a **frontend-only change** (no backend/database changes), skip all of
that and just run:

```powershell
./infra/redeploy-frontend-azure.ps1
```

This builds the Angular app, packages `frontend/{Dockerfile,dist,nginx.conf}`, stages it via
`infra/stage-via-runcommand.ps1`, then rebuilds and restarts only the `frontend` container on
the VM — leaving the API, database, and `.env` untouched. Pass `-SkipBuild` to reuse an
already-built `frontend/dist`. The same pattern works for backend-only changes by packaging
`backend/{StudentConnect.Api/Dockerfile,publish,database}` instead and targeting the `api`
service in the final `docker compose up --build -d` step.

If you later need high availability, autoscaling, or fully managed Postgres backups/PITR,
`infra/main.bicep` provisions the equivalent managed-PaaS architecture (App Service, Postgres
Flexible Server, Storage Account, Key Vault, Application Insights) instead.

### Other Azure hosting options

`infra/` contains a separate, independently-deployable Bicep template per hosting model so you
can pick based on budget vs. operational effort — none of them are wired into each other, so
deploying one doesn't require or affect the others.

| File | Model | Resource count | Approx. cost/mo | When to use |
|---|---|---|---|---|
| `vm.bicep` | Single Linux VM + Docker Compose (Postgres in a container) | 1 core (+ disks/IP) | ~$24 | Default recommendation: cheapest, fewest services, you own OS patching (automated via `cloud-init.yaml`) |
| `main.bicep` | App Service + Postgres Flexible Server + Storage + Key Vault + App Insights + Static Web App | 8 | ~$28-35 | Want managed backups/HA/observability and don't mind more services |
| `app-service-minimal.bicep` | 2 single-container Web Apps + Postgres Flexible Server + ACR | 5 | ~$30-33 | Middle ground: managed Postgres, no OS, fewer extras than `main.bicep` |
| `app-service-compose.bicep` | 1 Web App running api+frontend via Docker Compose + Postgres Flexible Server + ACR | 4 | ~$30-33 | Fewest resources of the container-based PaaS options; relies on an App Service feature Microsoft rarely updates — prefer the option above for anything beyond experimentation |
| `container-apps.bicep` | Azure Container Apps (api + frontend, `minReplicas=1`) + Postgres Flexible Server + ACR | 6 | ~$35-45+ | Least ops (auto HTTPS/scaling, no OS) — costs more than a VM since ACA consumption bills 24/7 usage per vCPU/GiB-second even at `minReplicas=1` |
| `container-instances.bicep` | One ACI container group (db+api+frontend together) + Azure Files + ACR | 4 | ~$25-30 | Simplest "just run containers" option; **no built-in HTTPS/custom domain** — would need Application Gateway/Front Door added separately |
| `aks.bicep` | AKS cluster + ACR (infra only, no app manifests) | 3+ (before workloads) | ~$30+ just for one small node, before deploying anything | Not recommended at this scale — only if you already operate other workloads on Kubernetes |

Every option except `vm.bicep`/`main.bicep`/`app-service-compose.bicep` needs images pushed to
its own Azure Container Registry first, e.g.:

```powershell
az acr build -r <acrName> -g <resource-group> -t studentconnect-api:latest -f backend/StudentConnect.Api/Dockerfile backend
az acr build -r <acrName> -g <resource-group> -t studentconnect-frontend:latest frontend
```

Each template's header comment documents its specific trade-offs, including a recurring
nuance worth calling out up front: `frontend/nginx.conf`'s `proxy_pass http://api:8080/api/;`
relies on Docker Compose's short DNS alias `api`, which only exists in `vm.bicep` and
`app-service-compose.bicep`. The other options need `nginx.conf` repointed at the api
container's actual internal address (App Service hostname, Container Apps FQDN, or
`localhost` for ACI) before building the frontend image for that target.

### Before scaling out: the background-worker constraint

The API hosts several `BackgroundService` pollers in-process — `EmailOutboxProcessor`,
`AnnouncementDispatchProcessor`, `HomeworkDispatchProcessor`, `LearningMaterialDispatchProcessor`,
`NotificationDigestProcessor`, `OperationalGovernanceProcessor`. Each one polls its own table
(e.g. `EmailOutboxStore.GetPendingAsync`) with a plain `SELECT`, not an atomic claim (no
`SELECT ... FOR UPDATE SKIP LOCKED`/equivalent) — verified directly in
`EmailOutboxProcessor.cs`/`EmailOutboxStore.cs`. **Running more than one API instance/replica
at a time means every poller runs once per instance against the same rows**, which can send
duplicate emails/notifications and double-process the same batch. This applies to *every*
option below — App Service autoscale, Container Apps `maxReplicas`, and AKS pod replicas all
hit the same constraint, since they're all running the same api image.

Practical takeaway used in the sizing guidance below: **scale the api component vertically
(a bigger SKU) before scaling it horizontally.** The frontend (stateless nginx) has no such
restriction and can always run multiple replicas safely. If you outgrow a single api instance,
the real fix is splitting the hosted workers into their own single-replica process/deployment
(or adding distributed locking to each poller) — a small code change, not something these
Bicep files solve on their own, so treat "run 2+ api replicas" as blocked until that's done.

### Load tiers used below

These are engineering rules-of-thumb for a multi-tenant CRUD/reporting app like this one, not
lab-measured numbers for this specific codebase — validate real capacity with
[Azure Load Testing](https://learn.microsoft.com/azure/load-testing/) against a staging
deployment before committing budget to a tier.

| Tier | Scale | Peak concurrent users | Notes |
|---|---|---|---|
| **1 — Pilot / single small school** | <300 students, <30 teachers, 1 tenant | ~5-15 | Matches the seeded "Demo School" scale. Traffic is bursty (morning attendance, occasional report-card lookups), mostly reads. All defaults in this repo's `.bicepparam` files target this tier. |
| **2 — Single medium school or a few small schools** | 300-2,000 students, up to ~5 tenants | ~30-100 | Multiple sections entering marks/attendance concurrently, more announcement/notification fan-out. |
| **3 — District / multi-school SaaS** | 2,000-20,000+ students, many tenants | 200-1,000+ | Needs HA, connection pooling in front of Postgres, and (per the constraint above) splitting request-handling from background-worker capacity. |

### `vm.bicep` — single Linux VM + Docker Compose

**Why:** cheapest option by a wide margin; only one Azure resource type to reason about;
Postgres-in-Docker matches the "very few services" goal directly.
**Why not:** no horizontal scale-out story at all — a VM is one machine — and you own OS
patching (mitigated by `cloud-init.yaml`'s `unattended-upgrades`, but still your
responsibility to verify).

- **Tier 1:** `vmSize=Standard_B1ms` (1 vCPU/2 GiB) — the repo default.
- **Tier 2:** `vmSize=Standard_B2s` or `Standard_B2ms` (2 vCPU/4-8 GiB) — also bump the data
  disk from Standard SSD to Premium SSD if `pg_isready`/query latency becomes noticeable under
  concurrent bulk-save load.
- **Tier 3:** not recommended — move to a managed compute option below, since a single VM has
  no built-in redundancy or scale-out.

### `main.bicep` — App Service + Postgres Flexible Server + Storage + Key Vault + App Insights + Static Web App

**Why:** fully managed (backups, patching, TLS, observability all handled by Azure); Static
Web Apps' free tier for the frontend; Key Vault + managed identity for secrets hygiene.
**Why not:** most resource types of any option here (8) — more to configure/secure, and most
of that (Key Vault, Log Analytics, App Insights) is overhead at Tier 1 scale.

- **Tier 1:** App Service `B1` (1 core/1.75 GiB), Postgres `Standard_B1ms` Burstable.
- **Tier 2:** App Service `S1`/`S2` (Standard — unlocks autoscale rules and staging slots),
  Postgres `Standard_B2s`/`Standard_B2ms` Burstable. Autoscale the *frontend/Static Web App*
  freely; per the constraint above, keep the api Web App at 1 instance.
  Enable `alwaysOn` health-check-based restarts to keep the hosted workers resilient without
  needing a second instance.
- **Tier 3:** App Service `P1v3`/`P2v3` (Premium v3), Postgres General Purpose
  `Standard_D4ds_v4`+ with zone-redundant HA and a read replica for `AnalyticsController`/
  `ReportStore`'s heavier aggregate queries (keeps reporting load off the OLTP path). Split the
  api into a stateless "web" App Service (autoscaled) + a separate single-instance "worker"
  App Service running just the hosted services, to get real horizontal scale without the
  duplicate-processing risk.

### `app-service-minimal.bicep` — 2 single-container Web Apps + Postgres Flexible Server + ACR

**Why:** same managed-platform benefits as `main.bicep` (no OS, Always On, easy TLS) with far
fewer resource types (5) — no Key Vault/Storage/App Insights/Static Web App.
**Why not:** secrets live in plain App Settings instead of Key Vault; no built-in
observability beyond basic App Service logs.

- **Tier 1:** App Service `B1`, Postgres `Standard_B1ms` Burstable.
- **Tier 2:** App Service `S1`, Postgres `Standard_B2s`/`Standard_B2ms` Burstable — same
  "autoscale frontend, not api" guidance as `main.bicep`.
- **Tier 3:** App Service `P1v3`, Postgres General Purpose `Standard_D4ds_v4`+ with HA — at
  this point you're close enough to `main.bicep`'s resource count that adding Key Vault/App
  Insights back in is worth it for a production district deployment; consider migrating.

### `app-service-compose.bicep` — 1 Web App (api+frontend via Docker Compose) + Postgres Flexible Server + ACR

**Why:** fewest resource types of any container-based PaaS option (4); the only PaaS option
where `nginx.conf`'s Compose DNS alias (`http://api:8080`) still works unchanged.
**Why not:** relies on an App Service feature Microsoft has not meaningfully invested in for
years; both containers share one App Service instance, so you can't scale frontend and api
independently at all.

- **Tier 1 only, realistically:** App Service `B1`, Postgres `Standard_B1ms` Burstable.
- **Tier 2:** possible with App Service `S1`, but since frontend and api can't scale
  independently here, prefer `app-service-minimal.bicep` once you're past Tier 1.
- **Tier 3:** not recommended — migrate to `app-service-minimal.bicep` or
  `container-apps.bicep` first.

### `container-apps.bicep` — Azure Container Apps (api + frontend) + Postgres Flexible Server + ACR

**Why:** least ops of any option (no OS, automatic HTTPS, built-in autoscaling primitives,
scale-to-zero available for non-always-on workloads elsewhere if you add more apps later).
**Why not:** Consumption plan bills per vCPU/GiB-second continuously at `minReplicas≥1`, so
it's pricier than a VM for the same always-on capacity — you're paying for less ops, not a
lower bill.

- **Tier 1:** api `0.5 vCPU/1 GiB`, frontend `0.25 vCPU/0.5 GiB`, both `minReplicas=1
  maxReplicas=1` (the repo default), Postgres `Standard_B1ms` Burstable.
- **Tier 2:** api `0.5-1 vCPU/1-2 GiB` still `maxReplicas=1` (background-worker constraint);
  frontend can safely go `maxReplicas=2-3` with an HTTP-concurrency scale rule since nginx is
  stateless. Postgres `Standard_B2s`/`Standard_B2ms` Burstable.
- **Tier 3:** api `1-2 vCPU/2-4 GiB`. To truly scale api horizontally at this tier, split it
  into two Container Apps sharing the same image: a stateless "web" app (`maxReplicas` 2-10,
  hosted services disabled via an env var/build flag) and a single-replica "worker" app running
  just the `BackgroundService`s — this is the cleanest fix for the constraint above on this
  platform, since ACA makes running two revisions of one image cheap. Postgres General
  Purpose `Standard_D4ds_v4`+ with zone-redundant HA + read replica.

### `container-instances.bicep` — one ACI container group (db+api+frontend) + Azure Files + ACR

**Why:** simplest "just run my containers" option, no VM, no App Service Plan concept to
reason about.
**Why not:** no built-in ingress/TLS/custom domain (would need Application Gateway/Front Door
added — more services, working against the whole point of this comparison); a container group
doesn't scale — it's fundamentally one instance of everything, including Postgres.

- **Tier 1 only:** `db`/`api`/`frontend` at `1 vCPU/1 GiB` each (the repo default). Good for a
  quick demo or proof-of-concept, not a real pilot deployment.
- **Tier 2/3: not supported by this model** — a single container group can't scale
  horizontally or split Postgres out; migrate to `vm.bicep` or an App Service/Container Apps
  option before outgrowing Tier 1.

### `aks.bicep` — minimal AKS cluster + ACR (infrastructure only)

**Why:** only if you already operate other workloads on Kubernetes and want to consolidate,
or specifically need k8s-native tooling (Helm, GitOps, custom operators, HPA/VPA).
**Why not:** most operational overhead of any option here by far, and this file provisions
the cluster only — an ingress controller, TLS (cert-manager or the AKS App Routing add-on),
and Kubernetes manifests (Deployment/Service/Ingress) for db/api/frontend are all still
needed before anything is reachable. Not recommended for this app's scale.

- **Tier 1/2: not worth it** — every other option reaches the same outcome with far less to
  operate; a single `Standard_B2s` node alone costs more than the equivalent VM/App Service
  tier before you've deployed the app.
- **Tier 3 (the only tier where this makes sense):** 3+ `Standard_D2s_v3`+ nodes for real HA,
  a `Deployment` with `HorizontalPodAutoscaler` for the stateless web replicas, and a separate
  single-replica `Deployment` (or a `Leases`-based leader-election sidecar) for the background
  workers — same constraint as every other option above, just expressed as Kubernetes
  manifests instead of Bicep. Postgres: still Azure Database for PostgreSQL Flexible Server
  (General Purpose, zone-redundant HA) rather than running Postgres in-cluster, unless you're
  already committed to operating StatefulSets + an operator (e.g. CloudNativePG) for it.

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

