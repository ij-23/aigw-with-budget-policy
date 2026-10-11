# Azure $50 budget lab

This lab deploys the dashboard, policy engine, APIM gateway, Foundry account and project, one pinned GPT-4.1-mini deployment, Cosmos DB, Azure Managed Redis with private networking and Entra authentication, ACR, Key Vault, Log Analytics, Application Insights and Entra applications. Terraform under [iac](../iac) owns the infrastructure. This is the sole supported deployment path in this repository. See [the solution guide](SOLUTION.md) for architecture and scope.

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

The script validates and applies a saved Terraform plan, builds through ACR Tasks, updates the Container App, stores credentials in Key Vault, then seeds the $50/month allowance. Run the interactive E2E command separately after deployment. Local Docker is not required. Terraform initially provisions a bootstrap image; the real dashboard becomes available after the image build/update.

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
| `create_key_vault` | `existing_key_vault_id`, with ARM `Microsoft.KeyVault/vaults/secrets/write` permission for the deployer |
| `create_network` | `existing_vnet_id`, `existing_apps_subnet_id` (delegated to `Microsoft.App/environments`), `existing_endpoints_subnet_id` |
| `create_cosmos_private_dns_zone` | `existing_cosmos_private_dns_zone_id`; the lab creates its VNet link |
| `create_key_vault_private_dns_zone` | `existing_key_vault_private_dns_zone_id`; the lab creates its VNet link |
| `create_key_vault_private_endpoint` | Existing vault networking; private secret retrieval requires VNet connectivity |
| `create_cosmos_private_endpoint` | Existing Cosmos network connectivity and DNS reachable from the Container Apps VNet |
| `create_monitoring` | `existing_log_analytics_id` and `existing_app_insights_id` |
| `create_identity` | `existing_identity`, with API/gateway/test/admin app IDs, automation secret, scopes, roles and consent |
| `create_test_user` | `existing_test_user_object_id`; supply a delegated gateway token |

The default deployment uses a VNet-connected Consumption workload profile environment and a Cosmos SQL private endpoint with private DNS. Cosmos and Key Vault public access are disabled. Key Vault has a private endpoint and DNS; Terraform stores credentials through the ARM secret-deployment API, so local deployment does not require vault data-plane network access. Set `store_lab_credentials_in_key_vault=false` to retain credentials only in sensitive, ignored Terraform state. Secret retrieval from the vault requires connectivity to its VNet. Set `container_workload_profile_name=null` for an existing legacy Consumption-only environment, or supply its workload profile name. An existing Container Apps environment must have network and DNS access to the supplied Cosmos endpoint. Changing the network type of an existing environment requires Terraform replacement.

Redis is Azure Managed Redis with a private endpoint, TLS and engine managed identity data access. `create_redis=true` creates it by default; `create_redis=false` creates no cache infrastructure and requires `existing_redis_connection_string`. The engine cannot currently run without Redis. Existing caches need engine data access and network/DNS reachability. Budget spending is durable in Cosmos and survives cache replacement.

SKU availability is subscription- and region-dependent. `redis_location` overrides only the cache location; its private endpoint remains beside the engine. If Azure returns a subscription/SKU restriction or capacity error, choose an approved SKU/region or bring an existing cache.

The default `redis_sku_name="Balanced_B0"` and `redis_high_availability_enabled=false` target POC cost. Set high availability to true for replicas. Use `create_redis_private_dns_zone=false` with `existing_redis_private_dns_zone_id` to reuse a DNS zone; avoid duplicate VNet links. See [Architecture and flow](ARCHITECTURE.md) for component and sequence diagrams.

When upgrading a previously deployed container-based lab, Terraform removes the old Redis Container App/password and switches the engine to Managed Redis. Cached counters and recent activity reset; the financial ledger remains in Cosmos. Provision the new cache and its connectivity before the full migration apply to reduce interruption. Inspect the plan before applying it.

Existing Cosmos accounts need `aipolicy` and containers `configuration` (`/partitionKey`), `audit-logs` and `billing-summaries` (`/customerKey`). `create_cosmos_schema` follows `create_cosmos` by default, so existing schemas are reused without import or management. Set `create_cosmos_schema=true` when an existing account needs a new lab schema. An existing account must have one write region. Financial ledger/configuration documents must not expire.

For another model or SKU, provide `LAB_PRICE_JSON` with verified prices and hard model input/output limits. The script checks the model, version and SKU before using its default GPT-4.1-mini price book. Reference retail rates: $0.40 input, $0.10 cached input and $1.60 output per million tokens; use contract rates if different. Check [Azure OpenAI pricing](https://azure.microsoft.com/pricing/details/cognitive-services/openai-service/) and [model limits](https://learn.microsoft.com/azure/foundry/openai/concepts/models) before changing this configuration.

## E2E verification

```bash
node scripts/lab/e2e-budget.mjs --device-code --verify-block
```

The script authenticates a real delegated test user, creates an access plan with token allowances disabled, and assigns a **$50/month USD allowance to the user's object ID**. It calls APIM with a small non-streaming text request and verifies provider usage, the settled Cosmos reservation, the calculated actual cost, and the remaining balance.

`--verify-block` temporarily lowers only the lab policy to its recorded spending, verifies `429 budget_exceeded` without further spending, then restores $50 in a `finally` block. It does not consume $50 or fabricate usage. If killed during that phase, restore $50 in the dashboard before the next run.

The gateway app requests security group claims for group allowances. When group assignments exist, missing or overage group claims cause a denial; Graph-based group-overage resolution is not implemented. The E2E test uses a direct user assignment.

You can configure the allowance before user sign-in with `node scripts/lab/e2e-budget.mjs --setup-only`. This creates the access plan, verified price book and $50/month user assignment without invoking a model.

For MFA tenants, run `node scripts/lab/e2e-budget.mjs --device-code --verify-block`. Follow the displayed Microsoft device sign-in instructions using the configured test user, and complete MFA enrollment if required. Retrieve its password locally with `terraform -chdir=iac output -raw test_user_password`; do not share the password or tokens. The script keeps the delegated token in memory and proceeds with inference after sign-in. An expired device session requires rerunning the command.

The dedicated account's password flow requires tenant policies to permit it. The script never disables MFA or Conditional Access. For an existing user or interactive login, set `LAB_USER_TOKEN` to a current delegated gateway token; do not commit it. `LAB_ADMIN_TOKEN` can supply an existing admin token.

The report is `.lab/e2e-report.json`: request ID, provider tokens, actual USD cost, spending, remaining balance, reset time and block-test result. It excludes passwords and tokens.

## Where to see policy and usage

- **Dashboard:** obtain `dashboard_url` with `terraform -chdir=iac output -raw dashboard_url` and open `/budgets`. Sign in as the configured administrator, enter `tenant_id` and click **Load budgets**. **Allowance policies** and **Assignments** show $50/month for the user. **Current balances** shows spending, reserved funds, remaining allowance and UTC reset. Refresh after calls using Load budgets.
- **APIM:** portal → API Management → lab service → APIs → **AI budget lab — $50 per user per month** → **All operations** → policy editor. API ID: `ai-budget-lab`; suffix: `budget/openai`. The policy validates JWTs, checks access, reserves USD, calls the model using managed identity and settles usage.
- **Rendered policy:** `.lab/apim-policy.xml`, or `terraform -chdir=iac output -raw rendered_policy`.
- **Cosmos:** with Cosmos data-plane read permission and VNet connectivity, open Data Explorer → `aipolicy` → `configuration`; find `partitionKey = 'budget:YOUR-TENANT-ID'`. `kind` identifies configuration, balances and reservations. These records are authoritative for budget usage.
- **Monitoring:** Application Insights and Log Analytics show engine diagnostics. Azure Cost Management reports provider/infrastructure charges separately; it is not the real-time allowance authority.

## Costs, limits and cleanup

The allowance covers model consumption according to the price book. APIM, Container Apps, Cosmos, ACR and monitoring have separate charges while running.

Image builds upload a temporary source-only context, excluding credentials, Terraform state, dependencies and local build outputs. `node scripts/lab/build-lab.mjs --build-only` builds without deployment; `LAB_IMAGE` reuses an existing image from the configured registry.

Initial support is non-streaming text Chat Completions, one response and an explicit output cap. Model account keys are disabled; APIM uses managed identity. Missing usage retains a reservation for verified reconciliation.

For cleanup, review and apply a Terraform destroy plan with the same state and inputs after approving the specific resources. Existing parent platforms are not destroyed, but the lab APIs, schema and permission assignments owned by this state are removed. Do not delete the resource group as a shortcut.

## Troubleshooting

| Symptom | Next step |
|---|---|
| Directory setup returns 403 | Entra app consent, app-role grants and user creation need directory privileges separately from Azure subscription RBAC. Activate the appropriate roles and refresh sign-in. |
| Device sign-in selects an administrator | Use a private browser window and select the exact test UPN printed by the script. |
| MFA enrollment is required | Enroll the test user during interactive sign-in; do not disable tenant controls. |
| Device code expired | Rerun the interactive test for a new code. |
| Model creation fails for capacity or availability | Select an approved region/model and verify its rates and bounds before deployment. |
| Setup cannot discover a model | Verify the configured Foundry account, deployment name and engine Reader permission. |
| Engine readiness fails | Inspect Container Apps console/system logs; verify private Cosmos DNS/networking and Redis reachability. |
| Allowance remains after a 429 | The conservative maximum request reservation may exceed the remaining funds. |
| Test interrupted during the block phase | Restore the lab policy to $50 in the dashboard before retrying. |

When a deployment fails partway through, keep its local state and variable file, fix the cause and rerun the deployment. Do not start a second environment with a new state to repair the first one. For an application-only update, run `node scripts/lab/build-lab.mjs`; for budget setup only, run `node scripts/lab/e2e-budget.mjs --setup-only`.

## Remove the POC

From the same checkout with its original state and configuration:

```bash
terraform -chdir=iac plan -destroy -out=destroy.tfplan
# Review every deletion in the displayed plan before applying.
terraform -chdir=iac apply destroy.tfplan
```

This deletes Terraform-managed resources and financial data; export any required evidence first. Existing parent services referenced with reuse flags remain, while child resources and grants owned by this state are removed. Verify that the plan matches the intended environment. Do not delete a resource group as a shortcut.
