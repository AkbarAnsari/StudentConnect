# Azure Storage & Data Glossary

Beginner-friendly definitions for Phase 13 terms. See `docs/phase-13.md` for how these
apply to StudentConnect specifically.

**Azure Database for PostgreSQL (Flexible Server)** — Microsoft's managed relational
database service built on the open-source PostgreSQL engine. You get a Postgres
database without managing the underlying server OS, patching, or physical hardware
yourself.

**Storage Account** — the top-level Azure resource that provides a unique namespace
and configuration boundary (security, redundancy, networking) for Azure Storage
services such as Blob Storage. Creating a storage account doesn't create any data by
itself — it's the container *for* containers.

**Blob** — an individual object stored in Azure Blob Storage (e.g. one PDF, one image).
"Blob" stands for Binary Large OBject.

**Blob Container** — a logical grouping of blobs inside a storage account (similar in
spirit to a folder, but flatter — it's a namespace, not a nested filesystem).
StudentConnect uses one container: `learning-materials`.

**Object Storage** — a storage model for unstructured data (files/objects), each
identified by a key/name, typically accessed by uploading/downloading the whole object
rather than querying inside it. Azure Blob Storage is Azure's object storage service.

**Relational Database** — a database that organizes data into tables with defined
columns, relationships (foreign keys), and constraints, queried with SQL. PostgreSQL
(local or Azure-hosted) is StudentConnect's relational database.

**Structured Data** — data with a consistent, well-defined shape (fields, types,
relationships) — e.g. a `Student` row. Fits naturally into a relational database.

**Unstructured Data** — data without a fixed relational shape — e.g. the bytes of a
PDF or image. Fits naturally into object storage.

**Metadata** — data *about* data. StudentConnect's `LearningMaterial` table is
metadata (title, subject, uploader, filename, size, upload date) describing a file
whose actual bytes live in Blob Storage.

**Connection String** — the configuration value an application uses to locate and
authenticate to a database (or, historically, a storage account). Never committed to
source control or placed in frontend code.

**Storage Redundancy** — how many copies of your data Azure Storage keeps and where
(e.g. LRS = multiple copies in one datacenter; ZRS/GRS/RA-GRS offer wider geographic
protection at higher cost). A trade-off between cost, durability, and availability —
not something to memorize in depth for this phase.

**Backup** — a separate, recoverable copy of data taken at a point in time, used to
**restore** data after accidental loss or corruption. Distinct from redundancy (which
protects against infrastructure failure, not human error like an accidental delete).

**Restore** — the act of recovering data from a backup.

**Durability** — the likelihood that stored data is not lost over time (an
infrastructure/replication property).

**Availability** — whether a service can be reached and used when needed (uptime). A
system can be highly durable (data isn't lost) while briefly unavailable (a temporary
outage), and vice versa — the two properties are related but not identical.
