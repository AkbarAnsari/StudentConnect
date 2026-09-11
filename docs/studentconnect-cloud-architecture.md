# StudentConnect — Current vs. Future Cloud Architecture

See [docs/phase-11.md](phase-11.md) for the full conceptual explanation. This document
shows only the two architecture diagrams side by side.

## Current architecture (implemented, Phases 1–10)

```
Browser
  ↓
Angular
  ↓
ASP.NET Core API
  ↓
PostgreSQL
  +
Local File Storage / Azure Blob Storage (config-selectable, see Phase 13)
```

Everything runs locally: Angular via `ng serve`, the API via `dotnet run`, a local
PostgreSQL server for structured data, and either local disk
(`storage/learning-materials/`) or a real Azure Blob Storage account for uploaded
files, selected via the `Storage:Provider` configuration setting (Phase 13).
Authentication (Phase 10) is local JWT-based, not a cloud identity provider.

## Future cloud architecture

> **Conceptual future architecture — not implemented in Phase 11.**

```
Internet
   ↓
Angular
   ↓
Managed Application Hosting
   ↓
ASP.NET Core API
   ├── Azure SQL Database
   ├── Azure Blob Storage
   ├── Microsoft Entra ID
   └── Azure Monitor
```

## Phase 12 conceptual hosting target

> **Conceptual target — not deployed in Phase 12.** Only compute/hosting is addressed
> here; the database and file storage remain local/development resources at this stage.
> See [docs/phase-12.md](phase-12.md) for the full explanation.

```
Browser
   ↓
Azure Static Web Apps        (Angular — static frontend hosting)
   ↓
HTTPS
   ↓
Azure App Service            (ASP.NET Core API — managed application hosting)
   ↓
Current database/storage     (PostgreSQL + local file storage / Azure Blob Storage)
```

Alternative compute directions considered in Phase 12, chosen based on requirements
rather than by default:

```
ASP.NET Core API
   ↓
Container image (Dockerfile in backend/StudentConnect.Api/)
   ↓
Azure Container Apps          (if/when the team standardizes on containers)

ASP.NET Core API
   ↓
Azure Virtual Machines        (if OS-level control is genuinely required)
```

| StudentConnect need | General capability | Future Azure direction |
|---|---|---|
| Angular application | Web hosting | Azure web hosting capability |
| ASP.NET Core API | Application hosting | Azure managed application hosting |
| Student data | Relational database | Azure Database for PostgreSQL |
| Learning materials | Object storage | Azure Blob Storage |
| Identity | Identity provider | Microsoft Entra ID |
| Monitoring | Observability | Azure Monitor / Application Insights |
| Secrets/configuration | Secret/configuration management | Azure Key Vault / app configuration |

Nothing in this document has been deployed or implemented — it exists to show the
*direction* the application could move in once the underlying cloud concepts (Phase 11)
are understood. No Azure SDK packages, configuration, or resources have been added to
the StudentConnect codebase.

## Phase 13 — Azure Storage & Data

> See [docs/phase-13.md](phase-13.md) for the full explanation. Unlike the sections
> above, part of this phase **was actually implemented and tested against a real
> Azure subscription** — this updates the table above with real status, not just intent.

```
ASP.NET Core API
   ├──────────────► PostgreSQL (local — Azure Database for PostgreSQL documented, not yet provisioned)
   │
   └──────────────► IFileStorage
                         ├── LocalFileStorage   (default)
                         └── AzureBlobStorage   (real Azure resource, config-selectable)
```

| Capability | Status |
|---|---|
| `AzureBlobStorage` (`IFileStorage` implementation, Azure AD auth via `DefaultAzureCredential`) | ✅ Implemented, live-tested (upload/download/delete) |
| Real Azure resources: resource group `studentconnect-rg`, storage account `stuconnect482913`, private container `learning-materials` | ✅ Created and verified reachable |
| Azure Database for PostgreSQL Flexible Server | 📄 Documented only — not provisioned (see phase-13.md for why) |
| Local development path (`LocalFileStorage` + local Postgres) | ✅ Preserved as the default — no Azure access required |
