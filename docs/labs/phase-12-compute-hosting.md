# Lab — Where Should StudentConnect Run?

**Phase 12: Azure Compute & Application Hosting**

This is a reasoning exercise, not a deployment exercise. No Azure resources are created
in this lab. See [docs/phase-12.md](../phase-12.md) for the concepts this lab uses.

## Step 1 — Identify the application components

List StudentConnect's current pieces:

```
Angular frontend
ASP.NET Core Web API
SQL Server (LocalDB)
Local file storage (learning materials)
```

## Step 2 — Identify which components require compute

Which of the above need something actively *executing* code (as opposed to just storing
data)?

```
Angular frontend   → executes in the browser (client-side compute)
ASP.NET Core API   → needs server-side compute to run
SQL Server         → needs compute to run the database engine
Local file storage → not compute itself, but read/written by the API's compute
```

## Step 3 — Compare VM, App Service, and Container Apps

For the ASP.NET Core API specifically, fill in your own reasoning:

| | Azure VM | Azure App Service | Azure Container Apps |
|---|---|---|---|
| Who manages the OS? | | | |
| Do we need to think about containers? | | | |
| How much infrastructure do we manage? | | | |
| Is this a good fit for a plain ASP.NET Core API? | | | |

(See the comparison tables in `docs/phase-12.md` if you want to check your answers.)

## Step 4 — Choose a hosting option

Pick one: VM, App Service, or Container Apps, for StudentConnect's API **today**
(given it is not yet containerized and doesn't need OS-level control).

## Step 5 — Justify the decision

Write 2–3 sentences explaining *why*, referencing the actual requirement (not "because
it's popular").

## Step 6 — Draw the architecture

Sketch (on paper or in a text diagram) how a browser reaches the Angular frontend, and
how the frontend reaches the API, using your chosen hosting option. Compare it against:

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

## Step 7 — Identify what the cloud provider manages

For your chosen option, list what Azure would take responsibility for (e.g. physical
hardware, hosting infrastructure, platform patching).

## Step 8 — Identify what the application team still manages

List what StudentConnect's own team remains responsible for (e.g. application code,
configuration, identities/permissions, data, application-level security) — regardless
of which hosting option was chosen.

## Optional — Docker demonstration

If Docker Desktop is running on your machine:

```bash
cd backend
docker build -f StudentConnect.Api/Dockerfile -t studentconnect-api:local .
docker run -p 8080:8080 studentconnect-api:local
```

Then try `GET http://localhost:8080/api/...` from another terminal or browser.

**Expected teaching moment:** the container will very likely fail to start cleanly,
because `Program.cs` tries to reach the database (`db.Database.Migrate()`) at startup,
and the connection string points at Windows LocalDB, which a Linux container cannot
reach without extra network configuration. This is intentional — it demonstrates that
**containerizing the application does not, by itself, solve where its database
lives.** (See "Database warning" in `docs/phase-12.md`.)

Stop the container afterward:

```bash
docker ps          # find the container id
docker stop <id>
```
