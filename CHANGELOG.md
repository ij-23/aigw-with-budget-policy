# Internal changelog


### 2026-10-11 — Fix Key Vault teardown lifecycle
- Provision ARM secrets using a PUT action so destroying an owned vault does not attempt unsupported secret DELETE operations.
- Document that secrets written into an existing vault remain under customer ownership after teardown.

### 2026-10-11 — PostgreSQL budget POC and Sweden Central defaults
- Replaced Cosmos SDK, repositories, audit store and budget batches with Npgsql and PostgreSQL transactions.
- Added SQL schema, tenant advisory locking and real database tests for concurrency, rollback, isolation and audit retry deduplication.
- Replaced Cosmos Terraform platform with optional create/use-existing PostgreSQL Flexible Server, private endpoint and diagnostics.
- Set regional infrastructure defaults to Sweden Central and documented rebuild and interactive budget testing.

### 2026-10-11 — Managed Redis and architecture

- Replaced the Redis Container App with optional creation of Azure Managed Redis, private networking, Entra data access, diagnostics and configurable SKU/high availability.
- Reject missing existing Redis connections when cache creation is disabled; document that the engine still requires Redis.
- Restart active engine revisions during deployment to load updated Redis secrets, including when reusing an image.
- Added component and numbered request-flow diagrams, $50 accounting example and dashboard guidance in docs/ARCHITECTURE.md.

### 2026-10-11 — Customer POC repository

- Removed alternative deployment stacks, obsolete scripts/docs, unrelated policy templates, demo and benchmark/load-test projects, and root agent-tooling dependencies.
- Added customer solution guide and a focused README with one Terraform deployment path.
- Added private configuration generation and separated POC deployment/setup from interactive MFA verification.
- Updated template tests and CI to validate the budget-only catalog and frontend.

## 2026-10-09 — Interactive budget lab verification

- Added device-code sign-in for the delegated test user to support MFA enrollment.
- Added setup-only mode to configure the $50/month policy and model prices before user authentication.

Dated implementation history for maintainers.

### 2026-10-09 — Azure APIM budget test environment

- Added modular Terraform under `iac/` with creation flags and existing-resource inputs.
- Added ACR image deployment and an authenticated $50 monthly E2E budget test.
- Added a deployment walkthrough and policy/usage viewing instructions.
- Added explicitly scoped Foundry discovery and canonical backend URL rewriting.
- Validated policy expressions against live APIM and fixed raw C# expression rendering.
- Added private Cosmos and Key Vault networking for tenants enforcing disabled public access; moved credential storage to Terraform ARM secret resources.
- Protected separately managed Entra identifier/redirect URIs from parent-resource updates and explicitly set the Consumption workload profile.
- Fixed internal Redis TCP discovery and configured the API audience for Entra v2 client-ID tokens.
- Reduced ACR upload context to application sources and added build-only/image reuse options.

### 2026-10-08 — USD budget enforcement

- Added weekly/monthly USD policies with user, group and application assignments.
- Added Cosmos transactional reservations, price snapshots, idempotent settlement and administrator reconciliation.
- Added a Budgets dashboard and Entra text-chat APIM template.
- Added an explicit switch to disable token allowances while retaining access and rate controls.
- Added service, HTTP, Cosmos transaction and template tests; documented supported requests and activation steps.
