# Cloud Glossary

Short, beginner-friendly definitions — see [docs/phase-11.md](phase-11.md) for the full
explanations and StudentConnect examples.

**Cloud Computing** — Consuming computing resources (compute, storage, databases,
networking, and more) over a network, instead of an organization owning and operating
all of the physical infrastructure itself.

**Cloud Provider** — A company (e.g. Microsoft, with Azure) that operates physical data
centers and exposes computing capabilities to customers through software, APIs, and
management tools.

**Region** — A geographic area containing one or more Azure datacenters used to host
cloud resources. Choosing a region can affect latency, cost, and available services.

**Availability Zone** — A physically separate location within an Azure region, designed
to help protect applications from certain datacenter-level failures. A region can
contain multiple availability zones.

**Subscription** — An account-level container through which Azure resources are
organized, managed, and billed. More than just a folder — it has management, billing,
and access-control significance.

**Resource Group** — A logical container for the Azure resources that belong to one
solution or lifecycle (e.g. all of StudentConnect's cloud resources together).

**Resource** — An individual Azure-managed object, such as a database, a storage
account, or an application hosting resource.

**IaaS (Infrastructure as a Service)** — The provider gives you infrastructure building
blocks (virtual machines, networking, storage); you manage more of the operating
environment yourself (OS, runtime, application).

**PaaS (Platform as a Service)** — The provider manages more of the underlying
infrastructure and platform; you focus more on the application and its data.

**SaaS (Software as a Service)** — You consume a complete, ready-to-use software
application; the provider manages essentially everything underneath it.

**Serverless** — Servers still exist, but the customer does not directly manage the
underlying server infrastructure, and typically consumes execution based on the
provider's managed model (e.g. code that runs in response to an event).

**Scalability** — The ability of a system to handle more (or less) load, generally by
adding or removing resources.

**Elasticity** — Resources automatically or easily increasing or decreasing as demand
changes, rather than being fixed in advance.

**Availability** — Whether a system is up and able to respond when needed; cloud
providers offer features (like availability zones) intended to help improve this.

**Shared Responsibility** — The idea that securing and operating a cloud solution is
split between the cloud provider (e.g. physical facilities, underlying infrastructure)
and the customer (e.g. application configuration, identities, permissions, data) — the
exact split depends on the service model (IaaS/PaaS/SaaS).

**Consumption** — Using (and typically being metered/billed for) cloud resources based
on actual usage, rather than a fixed upfront purchase.
