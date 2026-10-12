# AI Policy Engine — internal project tracker

ASP.NET policy engine with a React dashboard, APIM enforcement and PostgreSQL/Redis storage.

## Azure budget lab

- Networking: VNet-connected Consumption workload profiles; PostgreSQL and Key Vault private endpoints and DNS with public access disabled. Terraform deploys vault secrets through ARM. Bring-existing flags cover VNet, subnets, private DNS and PostgreSQL connectivity.
- Repository: `ij-23/aigw-with-budget-policy`; `iac/` is the authoritative modular test infrastructure.
- Ten infrastructure modules provide create/use-existing flags; local state and credentials are ignored.
- Azure Managed Redis replaces the internal cache Container App; PostgreSQL is the ledger authority. Redis creation is optional but the runtime requires an existing cache when creation is disabled.
- Managed Redis uses private networking, Entra data access granted before engine startup, TLS, NoCluster and NoEviction. Balanced_B0 without HA is the POC default; SKU/HA, Redis-specific region and private DNS reuse are configurable.
- The model-discovery ResourceIds option supports account-scoped Reader permissions.
- The budget policy rewrites to the canonical OpenAI route to remove arbitrary APIM API suffixes.
- Terraform stores credentials in Key Vault; lab scripts build through ACR, seed $50/month and verify real inference/settlement/blocking.
- Entra user creation, consent and app-role grants require tenant privileges separately from Azure subscription permissions.
- Lab setup can seed the $50 assignment before user authentication with `--setup-only`; `--device-code` supports interactive MFA without weakening tenant controls.

## Project structure

```text
docs/                         Customer solution, deployment and budget guides
  diagrams/                   Editable draw.io sources and PNG previews
iac/                          Authoritative Terraform and example configuration
  modules/                    Ten platform modules
policies/templates/           Entra USD-budget policy and its manifest
scripts/lab/                  Configure, deploy, build and test commands
src/
  AIPolicyEngine.Api/          Policy engine API and services
  AIPolicyEngine.ServiceDefaults/ Shared hosting and telemetry
  AIPolicyEngine.AppHost/      Optional local Aspire development host
  AIPolicyEngine.Tests/        Backend and policy tests
  aipolicyengine-ui/           React administrator dashboard
```

## Customer POC scope

- Only Terraform under `iac/` is supported for cloud provisioning.
- Only the Entra USD-budget policy is shipped. Access, routing and analytics services remain dependencies of its precheck/log flow.
- Configure-lab generates private inputs without overwriting state/configuration. Deploy-lab finishes after readiness and setup; device-code E2E is separate.
- Customer architecture and numbered flow are in [Architecture](docs/ARCHITECTURE.md); editable sources and embedded PNG previews live in `docs/diagrams/`. [Solution](docs/SOLUTION.md) explains resource purposes; [Interactive test](docs/INTERACTIVE_TEST.md) is the test runbook. Keep diagrams and these guides aligned with the APIM template and Terraform. No live tenant/account details belong in public guides.

## Budget decisions

- The budget ledger uses PostgreSQL transactions and a transaction-scoped advisory lock per tenant. ReadCommitted reads occur after acquiring the lock; all matching allowances commit together. JSONB preserves the public contracts and decimal money.
- Existing plans retain token enforcement by default. `EnforceTokenQuota=false` is explicit and does not disable access/rate controls.
- Budget policy assignment is independent of application-plan assignment. Per-member buckets deduplicate by user + policy across applications/groups; shared buckets use group + policy.
- Input reservations use the full configured backend input bound, not an estimated token count. Pricing snapshots remain attached to each reservation. A detected bound overrun quarantines its price configuration.
- Reservation replay cannot authorize another inference. Settlement is idempotent. Unknown outcomes retain funds until verified reconciliation.
- Initial request support is Entra, non-streaming text Chat Completions. Never silently accept new modalities or API schemas without extending pricing, bounds, parsing and tests together.
- All financial operations target the PostgreSQL primary; no read replicas for admission. No automatic financial-record deletion. Provider-log recovery, group overage resolution and archival are not implemented.

User setup: [USD budgets](docs/USD_BUDGETS.md). Dated history: [changelog](CHANGELOG.md).

- All new regional services default to swedencentral. PostgreSQL defaults to B_Standard_B1ms, 32 GiB storage and seven-day backups, with private endpoint networking and public access disabled. POC database password auth is explicit; least-privilege managed identity DB auth is future hardening. Application startup applies Database/001_initial.sql under a schema advisory lock.

Terraform provisions lab secrets with an ARM PUT action because the ARM secret resource does not support DELETE. Destroying the newly created vault removes its secrets with the vault; when using an existing vault, these written lab secrets remain in that vault and require customer-managed cleanup.

PostgreSQL preserves the Azure-selected availability zone with lifecycle ignore_changes to prevent perpetual zone-removal drift. RegionalAllocationFailed is handled through a customer-selected SKU override; repository defaults stay B1ms.
