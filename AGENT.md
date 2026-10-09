# AI Policy Engine — internal project tracker

ASP.NET policy engine with a React dashboard, APIM enforcement and Cosmos/Redis storage.

## Azure budget lab

- Networking: VNet-connected Consumption workload profiles; Cosmos SQL and Key Vault private endpoints and DNS with public access disabled. Terraform deploys vault secrets through ARM. Bring-existing flags cover VNet, subnets, private DNS and Cosmos connectivity.
- Repository: `ij-23/aigw-with-budget-policy`; `iac/` is the authoritative modular test infrastructure.
- Ten infrastructure modules provide create/use-existing flags; local state and credentials are ignored.
- New Redis is internal Container Apps cache; single-write-region Cosmos is the ledger authority.
- The model-discovery ResourceIds option supports account-scoped Reader permissions.
- The budget policy rewrites to the canonical OpenAI route to remove arbitrary APIM API suffixes.
- Terraform stores credentials in Key Vault; lab scripts build through ACR, seed $50/month and verify real inference/settlement/blocking.
- Entra user creation, consent and app-role grants require tenant privileges separately from Azure subscription permissions.
- Lab setup can seed the $50 assignment before user authentication with `--setup-only`; `--device-code` supports interactive MFA without weakening tenant controls.

## Project structure

Key maintained paths (not an exhaustive inventory):

```text
src/
  AIPolicyEngine.Api/
    Endpoints/
    Models/
    Services/Budgets/
    Program.cs
  AIPolicyEngine.AppHost/
  AIPolicyEngine.ServiceDefaults/
  AIPolicyEngine.Tests/Budgets/
  AIPolicyEngine.Benchmarks/
  AIPolicyEngine.LoadTest/
  aipolicyengine-ui/src/
    api/
    pages/
    components/
policies/templates/entra-jwt-ai-budget/
docs/
infra/terraform/
scripts/
demo/
```

## Budget decisions

- The budget ledger uses Cosmos transactional batches, not cache counters or expiring locks, for monetary authority. A tenant is one partition so all matching budgets can commit together. This favors correctness over high throughput; see the scaling limits in the user guide.
- Existing plans retain token enforcement by default. `EnforceTokenQuota=false` is explicit and does not disable access/rate controls.
- Budget policy assignment is independent of application-plan assignment. Per-member buckets deduplicate by user + policy across applications/groups; shared buckets use group + policy.
- Input reservations use the full configured backend input bound, not an estimated token count. Pricing snapshots remain attached to each reservation. A detected bound overrun quarantines its price configuration.
- Reservation replay cannot authorize another inference. Settlement is idempotent. Unknown outcomes retain funds until verified reconciliation.
- Initial request support is Entra, non-streaming text Chat Completions. Never silently accept new modalities or API schemas without extending pricing, bounds, parsing and tests together.
- Cosmos must have a single write region. Financial records must not expire via TTL. Provider-log recovery, group overage resolution, archival and multi-region authority are not implemented.

User setup: [USD budgets](docs/USD_BUDGETS.md). Dated history: [changelog](CHANGELOG.md).
