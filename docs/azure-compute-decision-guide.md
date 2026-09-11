# Azure Compute Decision Guide (Learning Tool)

> This is a **simplified learning decision guide** for StudentConnect-style workloads —
> not a universal Azure architecture decision tree. Real decisions also weigh cost,
> team skills, compliance, existing investments, and more. See
> [docs/phase-12.md](phase-12.md) for the full reasoning behind each option.

## The guide

```
Need: run an application
        │
        ▼
Do we need OS-level control?
        │
     Yes│        No
        ▼         ▼
       VM      Managed hosting
                  │
                  ▼
          Is it containerized?
             │           │
            No          Yes
             │           │
             ▼           ▼
        App Service   Container Apps
```

## Walking through it for StudentConnect's ASP.NET Core API

1. **Do we need OS-level control?** No — StudentConnect doesn't need custom OS software,
   drivers, or low-level configuration. → Skip the VM branch.
2. **Managed hosting** — the team wants the cloud provider to manage more of the
   underlying infrastructure (patching, OS, scaling mechanics).
3. **Is it containerized?** Not yet — StudentConnect's API is a plain ASP.NET Core
   project today (a Dockerfile now exists, see Phase 12, but the team isn't
   container-first yet). → **Azure App Service** is the current recommended direction.

If the team later standardizes on containers for consistency across environments (or
needs finer-grained scaling/orchestration for containerized workloads), **Azure
Container Apps** becomes the stronger fit — same "managed hosting" branch, different
answer to "is it containerized?"

## For the Angular frontend

Angular's production output is static files (HTML/CSS/JS/images) — a different
question from "where does server-side code run." The equivalent guide for a static
frontend:

```
Need: host static frontend files
        │
        ▼
Azure Static Web Apps
```

## Quick reference

| Requirement | Suggested direction |
|---|---|
| ASP.NET Core API, no OS-level control needed, not containerized | Azure App Service |
| ASP.NET Core API, already packaged as a container | Azure Container Apps |
| Workload needs OS-level control / custom software | Azure Virtual Machines |
| Angular static frontend | Azure Static Web Apps |

None of these have been deployed for StudentConnect — this guide documents the
*reasoning process*, not an implementation.
