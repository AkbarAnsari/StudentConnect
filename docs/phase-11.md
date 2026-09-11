# Phase 11 — Introduction to Cloud Computing & Azure

This phase is conceptual. **No application code changes were made.** StudentConnect
still runs exactly as it did at the end of Phase 10 — locally, with SQL Server LocalDB
and local file storage. The goal here is to understand *why* cloud computing exists and
*what* Azure provides, so that later phases can move StudentConnect there deliberately
rather than by copying service names.

## 1. Why cloud computing exists

StudentConnect today runs conceptually as:

```
                         Browser
                            │
                            ▼
                       Angular App
                            │
                            ▼
                  ASP.NET Core Web API
                            │
                 ┌──────────┴──────────┐
                 ▼                     ▼
             SQL Server          Local File Storage
```

Ask the questions a real school would eventually ask:

- What happens if this application needs to serve 10,000 students instead of 10?
- Where does the server run? Where does the database run? Where are uploaded files stored?
- How do we keep the system available if a machine fails?
- How do we handle more users without buying hardware in advance?
- Who manages the physical servers, power, cooling, and network equipment?

Answering these questions honestly is *why* cloud computing exists — not because it's a
trend, but because owning and operating all of that physical infrastructure is a real,
ongoing burden that most organizations would rather not carry themselves.

## 2. Traditional IT

A school running StudentConnect entirely on its own hardware would need to own and
operate:

```
School
  │
  ├── Server
  ├── Storage
  ├── Database Server
  ├── Network Equipment
  ├── Backup
  ├── Power
  ├── Cooling
  └── IT Staff
```

That means purchasing hardware, installing and patching operating systems, configuring
networks, replacing failed equipment, planning capacity in advance, managing backups,
and monitoring all of it — before a single student has logged in.

## 3. Cloud computing

```
School
   │
   ▼
Cloud Provider
   │
   ├── Compute
   ├── Storage
   ├── Databases
   ├── Networking
   ├── Security
   └── Monitoring
```

> Cloud computing is a way of consuming computing resources over a network instead of
> an organization having to own and operate all of the physical infrastructure itself.

**Important:** cloud does not mean "someone else's computer" and nothing more. Cloud
providers operate real, physical data centers — the computers still exist. The
difference is that the provider operates and manages that infrastructure, and exposes
it to customers through software, APIs, management tools, and automation, organized
into consumable service models (see IaaS/PaaS/SaaS below).

## 4. Cloud characteristics

Using StudentConnect as the running example:

- **On-demand** — if StudentConnect needs a database, provisioning it doesn't require
  ordering, shipping, and racking a physical server.
- **Elasticity** — resources can grow (more students enrolling) or shrink (summer
  break) as demand changes.
- **Broad network access** — the API and database are reachable over standard network
  protocols, not a private cable running through the school building.
- **Resource pooling** — the cloud provider's physical infrastructure is shared across
  many customers, using virtualization/isolation so each customer's resources stay
  separate.
- **Measured consumption** — many cloud resources are metered, so an organization can
  see and reason about what it's actually using.

These are learning concepts, not a checklist to memorize — the point is that each one
solves a specific problem a school running its own hardware would otherwise face.

## The StudentConnect cloud question

> StudentConnect has 10 students today and might have 10,000 next year. Should the
> school buy enough hardware today for 10,000 students?

Traditional approach: buy infrastructure in advance → possibly underutilized for years.
Cloud approach: start with the capacity actually needed → increase resources as demand
grows. **This still requires capacity planning and cost management** — cloud does not
mean unlimited or free resources.

## 5. CapEx and OpEx

- **Traditional infrastructure** is commonly associated with large upfront investments
  (servers, storage, networking, data center equipment) — **CapEx** (capital expense).
- **Cloud** is commonly associated with consuming services and paying according to the
  provider's pricing model — **OpEx** (operating expense).

This is a simplification, not a rule: the exact financial treatment depends on an
organization's accounting and purchasing arrangements (e.g. reserved capacity
commitments can behave differently). The conceptual difference that matters for
students is:

```
Own and operate infrastructure   vs   Consume computing capability as a service
```

## 6. IaaS — Infrastructure as a Service

The provider gives you infrastructure building blocks — virtual machines, networking,
storage — and you manage more of the operating environment yourself.

```
StudentConnect
      ↓
Virtual Machine
      ↓
Install OS
      ↓
Install .NET
      ↓
Deploy API
```

## 7. PaaS — Platform as a Service

The cloud provider manages more of the underlying infrastructure and platform; the
customer focuses more on the application itself.

```
StudentConnect API
       ↓
Managed application platform
       ↓
Cloud provider manages much of the platform
```

This model is highly relevant to StudentConnect's ASP.NET Core API — it's an ordinary
web application, not something that needs custom OS-level configuration.

## 8. SaaS — Software as a Service

The user consumes a complete, ready-to-use software application (webmail, online
document/collaboration tools). The customer generally does not manage servers or
operating systems at all — they just use the software.

## 9. Serverless

> Serverless does not mean there are no servers.

Servers still exist — the customer simply does not directly manage the underlying
server infrastructure, and typically consumes execution based on the provider's managed
model (e.g. code that runs in response to an event, without provisioning a server
ahead of time). This is introduced here only as a concept; **StudentConnect does not
use serverless yet**.

## Compare the models

| Model | Provider manages more? | Customer focuses more on |
|---|---|---|
| IaaS | Less | VM, OS, application |
| PaaS | More | Application and data |
| SaaS | Much more | Using/configuring software |
| Serverless | Provider manages underlying infrastructure | Functions/application logic |

These are simplified learning models — real services can blur these boundaries.

## 10. Shared responsibility

Moving to cloud does not mean the provider does everything.

```
Cloud Provider
        +
Customer
        ↓
Secure Cloud Solution
```

A simplified split, which shifts depending on the service model (IaaS vs PaaS vs SaaS):

- **Customer** typically remains responsible for: application configuration,
  identities, permissions, data, application-level security.
- **Provider** typically takes on: physical facilities, physical hardware, underlying
  infrastructure.

## 11. Microsoft Azure

> Microsoft Azure is Microsoft's cloud computing platform.

Azure provides capabilities across areas such as compute, storage, databases,
networking, identity, security, monitoring, data, AI, integration, and governance.
Students are **not** expected to memorize service names — the important skill is
recognizing which *capability* a problem needs first.

## 12–15. Azure core concepts

```
Azure
  ↓
Subscription
  ↓
Resource Group
  ↓
Resources
```

- **Subscription** — a boundary for Azure resources, management, billing, and access.
  Think of it as an account-level container through which Azure resources are
  organized, managed, and billed — not merely "a folder."
- **Resource Group** — a logical container for the resources that belong to one
  solution or lifecycle. For a deployed StudentConnect:

  ```
  StudentConnect Resource Group
          │
          ├── Application hosting resource
          ├── Database resource
          ├── Storage resource
          ├── Monitoring resources
          └── Other supporting resources
  ```

  Resource groups help with organization, lifecycle management (delete the group,
  everything in it goes with it), access control, and grouping related resources.
- **Resource** — an individual Azure-managed object: a database, a storage account, an
  application hosting resource, a virtual machine, a virtual network, etc.

## 16. Region

> An Azure region is a geographic area containing one or more Azure datacenters used to
> host cloud resources.

```
StudentConnect
      ↓
Choose an appropriate Azure region
      ↓
Application resources deployed there
```

Region selection can affect latency, available options, regulatory/data-residency
considerations, service availability, and cost. Choosing a region does **not**
automatically solve disaster recovery on its own.

## 17. Availability Zone

> Availability Zones are physically separate locations within an Azure region designed
> to help protect applications from certain datacenter-level failures.

```
Azure Region
 ├── Zone 1
 ├── Zone 2
 └── Zone 3
```

Analogy: multiple separate buildings in the same city, each with independent power and
cooling. Not every Azure service or region supports availability-zone capabilities in
exactly the same way — **this is not implemented for StudentConnect yet.**

## Azure management hierarchy (broader context)

```
Microsoft Entra tenant
        │
        ▼
Management Groups
        │
        ▼
Subscriptions
        │
        ▼
Resource Groups
        │
        ▼
Resources
```

Students are not expected to create management groups — this is included only so the
organizational picture is complete.

## 18. Azure Portal

> Azure Portal is a web-based management interface for working with Azure resources.

```
Browser
   ↓
Azure Portal
   ↓
Azure resources
```

### First Azure Portal activity (read-only, if access is available)

Ask students to identify, without creating anything:

1. Their subscription
2. Existing resource groups
3. Azure regions in use
4. Existing resources
5. Resource types
6. Tags, if present

If Azure access isn't available in the learning environment, use screenshots or a
conceptual walk-through instead. **No paid resources are required for this activity.**

## 19. Cost awareness

> Cloud resources cost money. Cloud ≠ free.

If StudentConnect creates a resource for a 30-minute classroom exercise and it's left
running for a month, the resource keeps consuming billable capacity — cost can continue
to accrue even though nobody is using it anymore. Students should learn to:

- know what they create,
- know whether it incurs cost,
- clean up resources they no longer need,
- understand a resource's lifecycle.

No prices are memorized or hardcoded here — Azure pricing changes over time, and none
of it is embedded in this documentation or the application.

### Tags

```
Environment = Learning
Application = StudentConnect
Owner = Training
```

Tags are simple key/value labels that help organize and manage resources — not an
elaborate governance system.

## 20. StudentConnect cloud architecture (conceptual, not implemented)

```
                         Internet
                            │
                            ▼
                         Browser
                            │
                            ▼
                         Angular
                            │
                            ▼
                Managed Application Hosting
                            │
                            ▼
                    ASP.NET Core API
                      │          │
                      │          │
                      ▼          ▼
             Managed SQL     Object Storage
              Database        for Files
                      │
                      │
                      ▼
              Monitoring
```

Identity:

```
User
  │
  ▼
Microsoft Entra ID
  │
  ▼
Application
```

**This diagram is conceptual only and has not been implemented.**

## 21. Capability-first architecture

> Choose the capability first, then choose the service.

Wrong learning approach: *"We need Blob Storage because Azure has Blob Storage."*

Better:

```
"We need to store PDFs and images."
        ↓
"We need object/file storage."
        ↓
"Azure provides Blob Storage for this capability."
```

Likewise: *"We need relational structured data"* → *"managed relational database"* →
*"Azure SQL Database is one Azure option."* This principle is reinforced throughout the
rest of the program.

## 22. Future Azure mapping for StudentConnect

| StudentConnect need | General capability | Future Azure direction |
|---|---|---|
| Angular application | Web hosting | Azure web hosting capability |
| ASP.NET Core API | Application hosting | Azure managed application hosting |
| Student data | Relational database | Azure SQL Database |
| Learning materials | Object storage | Azure Blob Storage |
| Identity | Identity provider | Microsoft Entra ID |
| Monitoring | Observability | Azure Monitor / Application Insights |
| Secrets/configuration | Secret/configuration management | Azure Key Vault / app configuration |

**None of these are implemented yet** — this is a map of *direction*, not a deployment
plan.

## What Azure manages vs. what we manage (simplified)

**Traditional VM approach** — we manage: VM, OS, runtime, application, data, configuration.

**Managed application platform** — we focus more on: application, configuration, data,
identity, application security. The provider manages more of the underlying
infrastructure. The exact split depends on the specific Azure service chosen.

## Hands-on activities

### Activity 1 — Cloud architecture mapping

Given StudentConnect's needs (run Angular, run the API, store student records, store
PDFs/images, authenticate users, monitor the application), students identify:

```
Requirement → Capability → Possible Azure service
```

Grade the reasoning, not memorized service names.

### Activity 2 — Traditional vs. cloud

A school buys 3 physical servers, a storage appliance, networking equipment, and backup
hardware. Students answer:

1. What happens if demand increases?
2. What happens if a server fails?
3. Who replaces hardware?
4. Who manages operating systems?
5. What happens if demand falls?
6. What could cloud change?
7. What new responsibilities remain with the school even in the cloud?

### Activity 3 — Architecture decisions

Students design a simple future StudentConnect architecture, answering:

- **Compute** — where should the API run?
- **Data** — where should structured data go?
- **Files** — where should PDFs and images go?
- **Identity** — how does the application know who the user is?
- **Monitoring** — how do we know if the application is working?
- **Cost** — how do we avoid unnecessary spending?

No advanced Azure knowledge is required — the goal is reasoning from requirement to
capability.

## Knowledge check

**Recall:** What is cloud computing? What is Azure? What is a region? What is a resource
group?

**Explain:** Why might an organization use cloud computing? Explain IaaS/PaaS in your
own words. What is the difference between authentication and authorization (Phase 10)?
What is the difference between a region and an availability zone?

**Scenario:**
- StudentConnect needs to store millions of PDFs → object/file storage.
- StudentConnect needs structured student records → relational database.
- StudentConnect needs to know who is logging in → identity/authentication.
- StudentConnect needs to know whether the API is healthy → monitoring/observability.

**Architecture:** Why shouldn't we start architecture design by choosing an Azure
service? What problems could happen if a school leaves unnecessary cloud resources
running?

## Challenge / homework

Design the future StudentConnect cloud solution using this template, for at least five
decisions (compute, database, file storage, identity, monitoring):

```
Requirement: _________________________
Capability needed: _________________________
Possible Azure service: _________________________
Why: _________________________
What would the application depend on? _________________________
```

Explain *why*, not just service names.

## Important misconceptions

- **"The cloud is somewhere in the sky."** Incorrect — cloud services run on physical
  infrastructure in real data centers.
- **"Cloud means no servers."** Incorrect — servers still exist; the customer just
  doesn't necessarily manage them directly.
- **"Cloud is always cheaper."** Incorrect — poor sizing, unnecessary resources, and
  inefficient architecture can create significant costs.
- **"Cloud means unlimited resources."** Incorrect — resources have limits,
  availability constraints, quotas, and costs.
- **"Azure is just virtual machines."** Incorrect — Azure spans compute, storage,
  databases, networking, identity, security, monitoring, data, AI, and more.
- **"PaaS means there are no servers."** Incorrect — the provider manages much of the
  underlying infrastructure, but servers still run the application.
- **"A resource group is just a folder."** Too simplistic — it's a logical management
  boundary with lifecycle and access-control implications.
- **"A region is the same as a data center."** Not exactly — a region is a geographic
  area containing one or more datacenters.
- **"Availability zones are separate regions."** Incorrect — they are physically
  separate locations *within* a region.

## Student learning outcome

> Cloud computing allows organizations to consume computing capabilities without owning
> and managing all of the physical infrastructure themselves. Azure is Microsoft's cloud
> platform. Before selecting an Azure service, I should understand the problem and
> capability I need.

Students should be able to distinguish Traditional IT vs. Cloud Computing, and IaaS vs.
PaaS vs. SaaS vs. Serverless. They should understand `Azure → Subscription → Resource
Group → Resources`, and `Region` vs. `Availability Zone`. Most importantly, they should
begin thinking:

```
Application requirement
        ↓
Capability
        ↓
Architecture decision
        ↓
Azure service
```

## Known limitations (intentionally deferred)

- No Azure deployment of any kind.
- No Azure resources created as part of the application.
- No Microsoft Entra ID implementation.
- No Azure SQL implementation.
- No Blob Storage implementation.
- No production cloud architecture yet — this phase is conceptual only.

## Next phase

**Azure Compute & Application Hosting** — take the first StudentConnect workload and
understand where an application/API should run in Azure, beginning with compute and
managed application hosting. Not implemented yet.
