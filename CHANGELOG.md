# Internal changelog

Dated implementation history for maintainers.

### 2026-10-09 — Azure APIM budget test environment

- Added modular Terraform under `iac/` with creation flags and existing-resource inputs.
- Added ACR image deployment and an authenticated $50 monthly E2E budget test.
- Added a deployment walkthrough and policy/usage viewing instructions.
- Added explicitly scoped Foundry discovery and canonical backend URL rewriting.
- Validated policy expressions against live APIM and fixed raw C# expression rendering.
- Added private Cosmos networking for tenants enforcing disabled public access.
- Reduced ACR upload context to application sources and added build-only/image reuse options.

### 2026-10-08 — USD budget enforcement

- Added weekly/monthly USD policies with user, group and application assignments.
- Added Cosmos transactional reservations, price snapshots, idempotent settlement and administrator reconciliation.
- Added a Budgets dashboard and Entra text-chat APIM template.
- Added an explicit switch to disable token allowances while retaining access and rate controls.
- Added service, HTTP, Cosmos transaction and template tests; documented supported requests and activation steps.
