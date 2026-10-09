# Internal changelog

Dated implementation history for maintainers.

### 2026-10-09 — Azure APIM budget test environment

- Added modular Terraform under `iac/` with creation flags and existing-resource inputs.
- Added ACR image deployment and an authenticated $50 monthly E2E budget test.
- Added a deployment walkthrough and policy/usage viewing instructions.
- Added explicitly scoped Foundry discovery and canonical backend URL rewriting.

### 2026-10-08 — USD budget enforcement

- Added weekly/monthly USD policies with user, group and application assignments.
- Added Cosmos transactional reservations, price snapshots, idempotent settlement and administrator reconciliation.
- Added a Budgets dashboard and Entra text-chat APIM template.
- Added an explicit switch to disable token allowances while retaining access and rate controls.
- Added service, HTTP, Cosmos transaction and template tests; documented supported requests and activation steps.
