# AI Policy Engine — internal project tracker

ASP.NET policy engine with a React dashboard, APIM enforcement and Cosmos/Redis storage.

## Azure budget lab

- Networking: VNet-connected Consumption workload profiles; Cosmos SQL and Key Vault private endpoints and DNS with public access disabled. Terraform deploys vault secrets through ARM. Bring-existing flags cover VNet, subnets, private DNS and Cosmos connectivity.
- Repository: `ij-23/aigw-with-budget-policy`; `iac/` is the authoritative modular test infrastructure.
- Ten infrastructure modules provide create/use-existing flags; local state and credentials are ignored.
- Azure Managed Redis replaces the internal cache Container App; single-write-region Cosmos is the ledger authority. Redis creation is optional but the runtime requires an existing cache when creation is disabled.
- Managed Redis uses private networking, Entra data access granted before engine startup, TLS, NoCluster and NoEviction. Balanced_B0 without HA is the POC default; SKU/HA, Redis-specific region and private DNS reuse are configurable.
- The model-discovery ResourceIds option supports account-scoped Reader permissions.
- The budget policy rewrites to the canonical OpenAI route to remove arbitrary APIM API suffixes.
- Terraform stores credentials in Key Vault; lab scripts build through ACR, seed $50/month and verify real inference/settlement/blocking.
- Entra user creation, consent and app-role grants require tenant privileges separately from Azure subscription permissions.
- Lab setup can seed the $50 assignment before user authentication with `--setup-only`; `--device-code` supports interactive MFA without weakening tenant controls.

## Project structure

```text
docs/
iac/
  modules/
policies/templates/
scripts/lab/
src/
  AIPolicyEngine.Api/
  AIPolicyEngine.ServiceDefaults/
  AIPolicyEngine.AppHost/
  AIPolicyEngine.Tests/
  aipolicyengine-ui/
```

## Customer POC scope

- Only Terraform under iac is supported for cloud provisioning; legacy stacks/scripts and unrelated demos are removed.
- Only the Entra USD-budget policy is shipped. Access, routing and analytics services remain dependencies of its precheck/log flow.
- Configure-lab generates private inputs without overwriting state/configuration. Deploy-lab finishes after readiness and setup; device-code E2E is separate.
- Customer architecture and numbered flow are in docs/ARCHITECTURE.md; solution detail is in docs/SOLUTION.md. No live tenant/account details belong in public guides.

## Budget decisions

- The budget ledger uses Cosmos transactional batches, not cache counters or expiring locks, for monetary authority. A tenant is one partition so all matching budgets can commit together. This favors correctness over high throughput; see the scaling limits in the user guide.
- Existing plans retain token enforcement by default. `EnforceTokenQuota=false` is explicit and does not disable access/rate controls.
- Budget policy assignment is independent of application-plan assignment. Per-member buckets deduplicate by user + policy across applications/groups; shared buckets use group + policy.
- Input reservations use the full configured backend input bound, not an estimated token count. Pricing snapshots remain attached to each reservation. A detected bound overrun quarantines its price configuration.
- Reservation replay cannot authorize another inference. Settlement is idempotent. Unknown outcomes retain funds until verified reconciliation.
- Initial request support is Entra, non-streaming text Chat Completions. Never silently accept new modalities or API schemas without extending pricing, bounds, parsing and tests together.
- Cosmos must have a single write region. Financial records must not expire via TTL. Provider-log recovery, group overage resolution, archival and multi-region authority are not implemented.

User setup: [USD budgets](docs/USD_BUDGETS.md). Dated history: [changelog](CHANGELOG.md).
