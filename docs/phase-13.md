# Phase 13 — Azure Storage & Data

## Learning goal

StudentConnect already has two different kinds of data:

```
Structured data                     File / object data
────────────────                    ──────────────────
Students, Grades, Subjects          PDFs, DOCX files, images
Attendance, Marks                   (Learning materials)
Homework, Activities
Registrations
```

These are different *capabilities*, not just different tables:

- Structured data has fields, relationships, constraints, and is queried with SQL —
  it belongs in a **relational database**.
- File data is binary content (potentially large), identified by a name/key, with
  metadata describing it — it belongs in **object storage**.

The progression this phase teaches:

```
Structured data → Relational database → Managed relational database → Azure Database for PostgreSQL

Documents/Images → File/Object storage → Managed object storage → Azure Blob Storage
```

**Adaptation note:** the original Phase 13 brief (and most Azure Fundamentals material)
frames the managed database step as *Azure SQL Database*. StudentConnect's database
engine was switched to **PostgreSQL** in an earlier ad-hoc change, so this phase
targets **Azure Database for PostgreSQL — Flexible Server** instead. The learning
progression, architecture, and every concept below are identical — only the specific
Azure service name changes. Nothing here is SQL-Server-specific.

## 1–3. Structured data vs. file data (recap of Phase 8)

| | Structured data | File/object data |
|---|---|---|
| Examples | Student, Grade, Attendance, Mark, Activity, Registration | PDF, DOCX, JPG, PNG learning materials |
| Characteristics | fields, relationships, constraints, transactions, SQL queries | binary content, filenames, content types, upload/download |
| Good fit | Relational database | Object storage |
| StudentConnect today | PostgreSQL (local, via EF Core) | Local disk (`LocalFileStorage`) |
| StudentConnect target | Azure Database for PostgreSQL | Azure Blob Storage |

## 4–5. Why not store PDFs in the database?

Technically a relational database *can* store binary data (e.g. Postgres `bytea`).
The lesson isn't "never do that" — it's **choose storage based on the data's
characteristics and the application's requirements**. For StudentConnect: files are
large relative to rows, don't need relational queries against their bytes, and are
served/downloaded as whole objects — object storage is the natural fit. The
`LearningMaterial` *metadata* (title, subject, uploader, filename, size, date) stays in
the relational database; the *bytes* move to object storage.

## 6–9. Azure Database for PostgreSQL

> Azure Database for PostgreSQL Flexible Server is Microsoft's managed relational
> database service built on the open-source PostgreSQL engine.

Capability-first framing:

```
Problem:    store StudentConnect's structured relational data
Capability: managed relational database
Azure:      Azure Database for PostgreSQL — Flexible Server
```

Current architecture (unchanged this phase):

```
Angular → ASP.NET Core API → PostgreSQL (local, on this machine)
```

Target architecture (documented, not yet provisioned — see "What was actually done"
below):

```
Angular → ASP.NET Core API → Azure Database for PostgreSQL
```

What changes is **where** the database runs and **who** manages the underlying
infrastructure — the application still talks to a relational Postgres database via the
same EF Core model and the same `Npgsql.EntityFrameworkCore.PostgreSQL` provider. The
browser never talks to the database directly; the API remains the only thing with a
connection string, and the only thing that enforces authorization (Phase 10).

Azure manages the physical infrastructure, OS/platform patching, and base
availability configuration. The application team still owns schema design, queries,
access configuration, appropriate security settings, and performance tuning — a
managed database does not automatically solve every database problem.

## 10–17. Azure Blob Storage

> Azure Blob Storage is Azure's object storage service for unstructured data —
> documents, images, videos, backups, and other files.

```
Problem:    store learning materials (PDFs, images, DOCX)
Capability: object storage
Azure:      Azure Blob Storage
```

Structure:

```
Storage Account
      ↓
Container ("learning-materials")
      ↓
Blob (e.g. f2e9773e-...-6525.pdf)
```

A container is a **logical grouping of blobs**, not exactly a filesystem folder.
StudentConnect uses one container, `learning-materials` — there's no need for more.

`LearningMaterial` metadata (Title, Description, SubjectId, GradeId, TeacherId,
FileName, ContentType, FileSize, UploadedDate) stays in Postgres, unchanged from
Phase 8. The blob only holds the file bytes:

```
Postgres
   └── LearningMaterial row (metadata, incl. StoredFileName = blob name)
              │
              └── points to →  Azure Blob Storage
                                    └── the actual PDF/DOCX/image bytes
```

## 18–20. Public vs. private storage, direct vs. API-mediated access

A student's learning material should **not** be publicly accessible on the open
internet by default. The container was created with **private access**
(`--public-access off`). All file access goes through the API's existing
`[Authorize]`-protected `/api/learning-materials/{id}/download` endpoint, which enforces
the same Phase 10 role/ownership rules before touching storage — Angular never talks to
Blob Storage directly:

```
Browser → Angular → ASP.NET Core API → Authorization → Blob Storage
```

## What was actually done in this environment

This phase was implemented against a **real Azure subscription** (not just
documentation) — see below for exactly what was created, tested, and what remains
conceptual-only.

### ✅ Implemented and live-tested against real Azure Blob Storage

- **Resource group**: `studentconnect-rg` (region: `eastus`)
- **Storage account**: `stuconnect482913` (Standard_LRS, StorageV2, TLS 1.2 minimum,
  `allowBlobPublicAccess: false`)
- **Container**: `learning-materials` (private, `publicAccess: off`)
- **`Storage/AzureBlobStorage.cs`** — new `IFileStorage` implementation using the
  official `Azure.Storage.Blobs` SDK (v12.29.2) + `Azure.Identity` (v1.21.0).
- **Config-driven provider switch** in `Program.cs`:

  ```csharp
  var storageProvider = builder.Configuration["Storage:Provider"] ?? "Local";
  if (string.Equals(storageProvider, "AzureBlob", StringComparison.OrdinalIgnoreCase))
      builder.Services.AddSingleton<IFileStorage, AzureBlobStorage>();
  else
      builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
  ```

- Verified live, end-to-end, through the actual running API (not just the Azure CLI):
  upload → real blob created in `stuconnect482913`/`learning-materials` → metadata row
  in Postgres → download → byte-for-byte content match → delete → blob and metadata
  both removed. Then re-verified `LocalFileStorage` still works identically with the
  `Storage:Provider` setting removed (the default).

### 📄 Documented / conceptual only (not provisioned)

- **Azure Database for PostgreSQL Flexible Server** — deliberately **not created** in
  this session (explicit decision: avoid the continuous per-hour cost of a database
  server while the local Postgres database already satisfies the "managed relational
  database" learning goal for day-to-day development). The database remains **local
  PostgreSQL** for now. See "Configuration" below for exactly how the app would point
  at an Azure Postgres server if/when one is provisioned — no code changes would be
  required, only a connection string.
- A **container-hosted Postgres** (e.g. Postgres running inside Azure Container Apps)
  was raised as an alternative to Flexible Server worth considering later — noted here
  as a discussion point, not implemented. It would still need someone to manage
  backups/patching/HA, so it is a different trade-off than a fully managed PaaS
  database, not simply "the same thing in a container."

### 🧭 A real-world lesson this session surfaced

While creating the storage account, its data plane was initially **unreachable from
this machine** even though the storage account itself provisioned successfully and its
network firewall rules were fully open (`defaultAction: Allow`, no IP restrictions).
The actual cause was `publicNetworkAccess: Disabled` at the storage-account level —
an organizational governance setting on this particular Azure subscription, separate
from firewall/IP rules entirely. It was resolved by the subscription owner adjusting
network policy; access was then verified end-to-end. **This is a genuine, valuable
lesson**: in real enterprise Azure environments, "the firewall looks open" and "the
resource is reachable" are two different questions — organizational policies can
enforce private-endpoint-only access regardless of a resource's own firewall settings.

The storage account also has **`allowSharedKeyAccess: false`** (shared/account-key
authentication disabled by policy) — so `AzureBlobStorage.cs` authenticates with
**Azure AD via `DefaultAzureCredential`** (the developer's `az login` session locally;
a managed identity once actually deployed), not a connection string or account key.
There is no storage secret anywhere in this codebase or its configuration.

## Configuration

```jsonc
// appsettings.json (committed — contains no secrets)
"Storage": { "Provider": "Local" },          // "Local" or "AzureBlob"
"AzureBlobStorage": {
  "AccountUrl": "",                           // e.g. https://<account>.blob.core.windows.net/
  "ContainerName": "learning-materials"
}
```

To run against the real Azure Blob Storage account created for this phase (requires
`az login` locally so `DefaultAzureCredential` can authenticate):

```powershell
$env:Storage__Provider = "AzureBlob"
$env:AzureBlobStorage__AccountUrl = "https://stuconnect482913.blob.core.windows.net/"
dotnet run --urls http://localhost:5080
```

Unset those two environment variables (or just don't set them) to fall back to
`LocalFileStorage` — the default, and what a fresh clone of the repo uses with zero
configuration.

If/when an Azure Database for PostgreSQL server is provisioned, `ConnectionStrings:DefaultConnection`
would simply be overridden with its connection string (via an environment variable or
user-secret, never committed) — `Program.cs`'s `options.UseNpgsql(...)` call does not
change at all, because Azure Database for PostgreSQL speaks the same Postgres wire
protocol as the local server.

## Consistency note

SQL and Blob Storage are **separate services** — a Postgres transaction does not
automatically cover a blob operation. `LearningMaterialsController` already handles
this with simple compensating cleanup (Phase 8): if the blob upload succeeds but the
database save fails, the orphaned blob is deleted; if a delete's blob-removal succeeds
but the metadata delete fails, the failure is logged rather than silently ignored. This
phase did not add distributed transactions — that would be over-engineering for this
architecture.

## Security summary

- Learning-material blobs are **private** — never publicly accessible by default.
- Blob access is authenticated with **Azure AD**, not a shared key.
- No storage credentials, connection strings, or account keys exist in Angular,
  source control, or documentation.
- The API remains the only component with database/storage access — the browser never
  talks to Postgres or Blob Storage directly.
- Phase 10's authentication/authorization continues to gate every learning-material
  endpoint exactly as before, regardless of which `IFileStorage` implementation is
  active.

## Backup / redundancy (concept only, not implemented)

Cloud storage and managed databases can replicate data for durability/availability
(e.g. Blob Storage's LRS/ZRS/GRS options — the storage account here uses the simplest,
LRS) and managed databases can offer configurable backup/restore. These are **choices
with cost/durability/availability trade-offs**, not automatic guarantees of "zero data
loss" or "zero downtime." No backup policy or lifecycle management was configured in
this phase — out of scope.

## What's next

See `docs/labs/phase-13-storage-data.md` for a hands-on lab, `docs/azure-storage-glossary.md`
for term definitions, and `docs/data-storage-decision-guide.md` for the
structured-vs-file-data decision guide.

**Next phase**: Azure Identity & Secure Access — connect Phase 10's identity concepts to
Microsoft Entra ID and introduce managed identities / secure application-to-Azure-service
access (not implemented yet).
