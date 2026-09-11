# Lab: Move StudentConnect Data & Files to Azure

## Track A — Conceptual (no Azure access required)

Design, on paper, how StudentConnect's data maps onto:

```
StudentConnect
    ↓
Azure Database for PostgreSQL  +  Azure Blob Storage
```

For each of the following, identify (a) whether it's structured or file data, (b)
which capability it needs, and (c) which StudentConnect component is responsible for
touching it:

1. A student's attendance record for one day.
2. A teacher-uploaded homework PDF.
3. The homework PDF's title, subject, and upload date.
4. A student's activity registration.
5. A profile photo (hypothetical — StudentConnect doesn't have these yet).

Then answer:

- Which component is StudentConnect's **security boundary** — i.e., what's the only
  thing allowed to talk to the database and to blob storage?
- Why shouldn't Angular hold a storage account key or a database connection string?

## Track B — Azure (requires an Azure subscription)

This track was actually carried out for StudentConnect during Phase 13 development,
against a real Azure subscription. Steps 1–5 and 8–11 below reflect exactly what was
done; step 6/7 (Azure Postgres) was intentionally **not** executed (see `docs/phase-13.md`
for why) — treat those two as instructions for later, not a "already done" claim.

1. **Identify your subscription**: `az account show`
2. **Create/use a resource group**: `az group create -n studentconnect-rg -l eastus`
3. **Create a storage account** (name must be globally unique, lowercase alphanumeric,
   ≤24 chars):
   ```
   az storage account create -n <name> -g studentconnect-rg -l eastus \
     --sku Standard_LRS --kind StorageV2 --min-tls-version TLS1_2 \
     --allow-blob-public-access false
   ```
4. **Create a private container**:
   ```
   az storage container create --name learning-materials \
     --account-name <name> --auth-mode login --public-access off
   ```
5. **Grant yourself data-plane access** (needed because shared-key auth may be
   disabled by policy — as it is on the account used for this phase):
   ```
   az role assignment create --assignee-object-id <your-object-id> \
     --assignee-principal-type User --role "Storage Blob Data Contributor" \
     --scope <storage-account-resource-id>
   ```
   RBAC role assignments can take a minute or two to propagate — if you get an
   authorization error immediately after this step, wait and retry.
6. *(Not done in this phase — for later)* **Create an Azure Database for PostgreSQL
   Flexible Server** and note its cost is continuous (per-hour) unlike the
   pay-per-use storage account.
7. *(Not done in this phase — for later)* **Apply the existing EF Core migrations**
   against it: `dotnet ef database update` with `ConnectionStrings:DefaultConnection`
   pointed at the Azure server via an environment variable (never committed).
8. **Configure StudentConnect** to use the storage account:
   ```powershell
   $env:Storage__Provider = "AzureBlob"
   $env:AzureBlobStorage__AccountUrl = "https://<name>.blob.core.windows.net/"
   dotnet run --urls http://localhost:5080
   ```
9. **Upload a sample learning material** through the running app (login as a
   Teacher/Admin, `POST /api/learning-materials`, or through the Angular UI once it's
   running against this API instance).
10. **Verify** — check both halves independently:
    - Metadata in Postgres: `SELECT * FROM "LearningMaterials";`
    - The blob itself: `az storage blob list --account-name <name> --container-name
      learning-materials --auth-mode login -o table`
11. **Download the file back through StudentConnect** (`GET
    /api/learning-materials/{id}/download`) and confirm the bytes match what you
    uploaded.
12. **Clean up** — unset the two environment variables to fall back to local storage;
    if you're done experimenting entirely, `az group delete -n studentconnect-rg`
    removes everything created in this lab in one step.

## Knowledge check

**Recall**

1. What is structured data? What is object storage?
2. What is Azure Database for PostgreSQL? What is Azure Blob Storage?
3. What is a storage account? What is a blob container?

**Explain**

1. Why shouldn't every file automatically be stored in a relational database?
2. Why does StudentConnect need both a relational database and object storage?
3. Why shouldn't Angular connect directly to the database?
4. Why shouldn't storage credentials be placed in Angular code?
5. What is metadata, and where does StudentConnect's learning-material metadata live?
6. Why is storing files only on local disk problematic once an app runs on multiple
   server instances?

**Scenario**

1. StudentConnect needs to store a student's attendance. → *Relational database.*
2. StudentConnect needs to store a PDF worksheet. → *Object storage.*
3. StudentConnect needs to store information about that PDF. → *Relational database
   (metadata).*
4. StudentConnect runs on 10 API instances. Where should shared learning materials
   live? → *Shared durable object storage — not any one instance's local disk.*
5. Should a learning-material blob normally be publicly accessible? → *Not by default
   — access should be controlled through the API.*

## Architecture challenge

> StudentConnect has grown to 50,000 students. The API runs on multiple application
> instances. Students upload thousands of PDFs and images each month.

Design (on paper): frontend, backend, database, file storage, identity. Then ask:
**"What happens if one API instance disappears?"** Expected reasoning: the application
keeps working because shared state (the database, the learning-material files) doesn't
live on that one instance's local disk — this is exactly why StudentConnect's
`IFileStorage` abstraction and its `AzureBlobStorage` implementation matter.

## Troubleshooting exercises

**Scenario A** — A student uploads a PDF. The database record exists, but the file is
missing. *What went wrong?* → The metadata write succeeded but the blob
upload/storage failed or the blob was later deleted independently of the app.

**Scenario B** — A file exists in Blob Storage, but there's no matching database
metadata row. *What could have happened?* → The blob upload succeeded but the
subsequent metadata save failed (see `docs/phase-13.md`'s consistency note — this is
exactly the partial-failure case `LearningMaterialsController` already guards against
with compensating cleanup).

**Scenario C** — A request to Blob Storage returns an authorization/unauthorized
error. *What should you check?* → The caller's identity/credential, the RBAC role
assignment on the storage account (e.g. `Storage Blob Data Contributor`), the
storage account's network access settings (note: a fully open firewall does **not**
guarantee reachability — see the `publicNetworkAccess` lesson in `docs/phase-13.md`),
and the app's configuration (`Storage:Provider`, `AzureBlobStorage:AccountUrl`).
