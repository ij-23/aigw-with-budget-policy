# Azure $50 budget lab

This lab deploys the dashboard, policy engine, APIM gateway, Foundry account and project, one pinned GPT-4.1-mini deployment, Cosmos DB, password-protected internal Redis, ACR, Key Vault, Log Analytics, Application Insights and Entra applications. Terraform under [iac](../iac) owns the infrastructure. The original [infra/terraform](../infra/terraform) folder is upstream reference material; use `iac` for this lab.

## Prerequisites

- Azure CLI, Terraform 1.9 or later, Node.js 22 or later, and an Azure subscription with resource creation and role assignment permissions.
- Entra permissions to create a test user, grant delegated consent and assign app roles. Azure subscription Owner does **not** provide these directory permissions. Global Reader cannot complete identity setup. Use a suitably authorized administrator; separate administrators must use the same local Terraform state, transferred privately.
- Model capacity in the selected region. Default: `gpt-4.1-mini`, version `2025-04-14`, `GlobalStandard`, capacity 1.

## Deploy and test

Copy [iac/terraform.tfvars.example](../iac/terraform.tfvars.example) to `iac/terraform.tfvars`; set subscription, tenant, administrator object ID, verified test-user domain, contact email and a unique prefix.

```bash
az login --tenant YOUR-TENANT-ID
bash scripts/lab/deploy-lab.sh
```

The script validates and applies a saved Terraform plan, builds through ACR Tasks, updates the Container App, stores credentials in Key Vault, then runs the E2E test. Local Docker is not required. Terraform initially provisions a bootstrap image; the real dashboard becomes available after the image build/update.

State is **local** and excluded from Git along with variable files, plans, tokens, reports and credentials. Protect and back up the state: it contains passwords even when Terraform marks them sensitive. A public clone does not contain your environment state.

## Create or use existing resources

All create flags default to `true`. Set a flag to `false` and supply its existing input. Existing platforms remain data references; Terraform owns the new lab API and permission assignments, not their parent platforms.

| Platform flag | Existing input |
|---|---|
| `create_resource_group` | `resource_group_name` |
| `create_apim` | `existing_apim_id` (system assigned identity required) |
| `create_foundry` | `existing_foundry_id` |
| `create_foundry_project` | `false` skips the project; creation requires a project-capable AI Services account |
| `create_model_deployment` | `deployment_name` of an existing deployment |
| `create_cosmos` | `existing_cosmos_id`, single write region required |
| `create_redis` | `existing_redis_connection_string` with password or Entra-compatible managed identity configuration |
| `create_registry` | `existing_registry_id` |
| `create_container_environment` | `existing_container_environment_id` |
| `create_policy_engine` | `existing_policy_engine_id`, running updated code with matching configuration |
| `create_key_vault` | `existing_key_vault_id`, with secret write permission for the deployer |
| `create_monitoring` | `existing_log_analytics_id` and `existing_app_insights_id` |
| `create_identity` | `existing_identity`, with API/gateway/test/admin app IDs, automation secret, scopes, roles and consent |
| `create_test_user` | `existing_test_user_object_id`; supply a delegated gateway token |

Redis is a small internal Container App used only as a cache, not a managed production Redis service. Budget spending is durable in Cosmos and survives Redis restarts. Use `create_redis=false` for an existing managed Redis service.

Existing Cosmos accounts need `aipolicy` and containers `configuration` (`/partitionKey`), `audit-logs` and `billing-summaries` (`/customerKey`). `create_cosmos_schema` follows `create_cosmos` by default, so existing schemas are reused without import or management. Set `create_cosmos_schema=true` when an existing account needs a new lab schema. An existing account must have one write region. Financial ledger/configuration documents must not expire.

For another model or SKU, provide `LAB_PRICE_JSON` with verified prices and hard model input/output limits. The script checks the model, version and SKU before using its default GPT-4.1-mini price book. Reference retail rates: $0.40 input, $0.10 cached input and $1.60 output per million tokens; use contract rates if different. Check [Azure OpenAI pricing](https://azure.microsoft.com/pricing/details/cognitive-services/openai-service/) and [model limits](https://learn.microsoft.com/azure/foundry/openai/concepts/models) before changing this configuration.

## E2E verification

```bash
node scripts/lab/e2e-budget.mjs --verify-block
```

The script authenticates a real delegated test user, creates an access plan with token allowances disabled, and assigns a **$50/month USD allowance to the user's object ID**. It calls APIM with a small non-streaming text request and verifies provider usage, the settled Cosmos reservation, the calculated actual cost, and the remaining balance.

`--verify-block` temporarily lowers only the lab policy to its recorded spending, verifies `429 budget_exceeded` without further spending, then restores $50 in a `finally` block. It does not consume $50 or fabricate usage. If killed during that phase, restore $50 in the dashboard before the next run.

The dedicated account's password flow requires tenant policies to permit it. The script never disables MFA or Conditional Access. For an existing user or interactive login, set `LAB_USER_TOKEN` to a current delegated gateway token; do not commit it. `LAB_ADMIN_TOKEN` can supply an existing admin token.

The report is `.lab/e2e-report.json`: request ID, provider tokens, actual USD cost, spending, remaining balance, reset time and block-test result. It excludes passwords and tokens.

## Where to see policy and usage

- **Dashboard:** obtain `dashboard_url` with `terraform -chdir=iac output -raw dashboard_url` and open `/budgets`. Sign in as the configured administrator, enter `tenant_id` and click **Load budgets**. **Allowance policies** and **Assignments** show $50/month for the user. **Current balances** shows spending, reserved funds, remaining allowance and UTC reset. Refresh after calls using Load budgets.
- **APIM:** portal → API Management → lab service → APIs → **AI budget lab — $50 per user per month** → **All operations** → policy editor. API ID: `ai-budget-lab`; suffix: `budget/openai`. The policy validates JWTs, checks access, reserves USD, calls the model using managed identity and settles usage.
- **Rendered policy:** `.lab/apim-policy.xml`, or `terraform -chdir=iac output -raw rendered_policy`.
- **Cosmos:** Data Explorer → `aipolicy` → `configuration`; find `partitionKey = 'budget:YOUR-TENANT-ID'`. `kind` identifies configuration, balances and reservations. These records are authoritative for budget usage.
- **Monitoring:** Application Insights and Log Analytics show engine diagnostics. Azure Cost Management reports provider/infrastructure charges separately; it is not the real-time allowance authority.

## Costs, limits and cleanup

The allowance covers model consumption according to the price book. APIM, Container Apps, Cosmos, ACR and monitoring have separate charges while running.

Initial support is non-streaming text Chat Completions, one response and an explicit output cap. Model account keys are disabled; APIM uses managed identity. Missing usage retains a reservation for verified reconciliation.

For cleanup, review and apply a Terraform destroy plan with the same state and inputs after approving the specific resources. Existing parent platforms are not destroyed, but the lab APIs, schema and permission assignments owned by this state are removed. Do not delete the resource group as a shortcut.
