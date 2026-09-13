# StudentConnect — Azure Infrastructure (prepared, not deployed)

This folder contains Bicep templates for a possible future Azure deployment of
StudentConnect. **Nothing here has been deployed.** Per the project's phase-15 roadmap,
this is Phase 24 scoped to IaC preparation only — no real Azure resources are provisioned
by writing or reviewing these files.

## What this deploys (when you're ready)

| Resource | Purpose |
|---|---|
| Azure Database for PostgreSQL Flexible Server (Burstable B1ms) | Replaces the local Postgres dev database |
| Storage Account + `learning-materials` blob container | Backs `AzureBlobStorage` (already implemented, see `Infrastructure/Storage/AzureBlobStorage.cs`) |
| App Service Plan (Linux, B1) + App Service | Hosts the ASP.NET Core API |
| Static Web App (Free tier) | Hosts the built Angular frontend |
| Key Vault (RBAC-authorized) | Holds the Postgres admin password and JWT signing secret |
| User-assigned managed identity | Lets the API read Key Vault secrets and access Blob Storage without any connection string or account key |
| Log Analytics workspace + Application Insights | Observability — pairs with the `/health` endpoint and `/api/ops/info` from Phase 21 |

**Deliberately not included**: Microsoft Entra ID. StudentConnect keeps its existing
local JWT/database-driven login and authorization (an explicit decision — see the
phase-15 roadmap's Phase 22 entry) — the deployed API still validates against its own
`Users` table, just like local dev.

## Deploying (when actually ready)

```bash
az login
az group create -n <resource-group-name> -l eastus

az deployment group create \
  -g <resource-group-name> \
  -f main.bicep \
  -p main.bicepparam \
  -p postgresAdminPassword='<a-real-secret>' \
  -p jwtSecret='<a-real-secret>' \
  -p frontendOrigin='https://<your-static-web-app-hostname>'
```

After the infrastructure exists, apply `backend/database/schema.sql` and
`backend/database/seed.sql` against the new Postgres server (the same scripts
`Program.cs` already applies automatically on startup locally), and deploy the API
container/build output to the App Service and the Angular build output to the Static
Web App through your normal CI/CD process.

## What was validated locally (no Azure connection required)

- `az bicep build --file main.bicep` — compiles clean, no errors or warnings
- `az bicep build-params --file main.bicepparam` — compiles clean against `main.bicep`

Both are static, offline checks — neither talks to Azure or provisions anything, and
no deployment (`az deployment group create` / `what-if`) has been run against a real
subscription.
