# Azure AI Gateway with Dollar Budgets

A deployable proof of concept that gives an AI consumer a weekly or monthly USD allowance and enforces it through Azure API Management. The default demonstration assigns a test user **$50 per calendar month** for GPT-4.1-mini requests.

APIM authenticates callers and requests authorization from a policy engine. PostgreSQL holds durable reservations and spending records; Redis supports existing access checks, rate counters and recent activity. The administrator dashboard shows policies, assignments, balances and pending reservations.

**Supported:** Entra-authenticated, non-streaming, text Chat Completions with one response and an explicit output cap. This is a POC, not a production certification or an Azure invoice spending cap. Read the [architecture and flow](docs/ARCHITECTURE.md) and [solution explanation](docs/SOLUTION.md) for the request flow, resource purposes and limitations.

## Deploy a customer POC

| Prerequisite | Requirement |
|---|---|
| Terminal | Bash on macOS/Linux or WSL on Windows |
| Azure CLI | Signed in to the intended tenant and subscription |
| Terraform | Version 1.9 or newer, below 2.0; providers locked in `iac/.terraform.lock.hcl` |
| Node.js | 22 or newer |
| Azure permissions | Create resources and assign scoped RBAC roles; typically Contributor plus User Access Administrator, or Owner |
| Entra permissions | Create applications, consent/role grants and a test user; Cloud Application Administrator plus User Administrator, or an authorized administrator |
| Model capacity | GPT-4.1-mini `2025-04-14`, GlobalStandard, available in the selected region |

Local Docker and the .NET SDK are not required for Azure deployment: ACR builds the image remotely.

```bash
git clone https://github.com/ij-23/aigw-with-budget-policy.git
cd aigw-with-budget-policy
az login
# Select the intended subscription using your usual Azure CLI workflow.
node scripts/lab/configure-lab.mjs
# Review iac/terraform.tfvars: tenant, subscription, domain, region and unique prefix.
bash scripts/lab/deploy-lab.sh
```

Configuration generation uses the signed-in account and creates a private, ignored variable file without overwriting an existing file. You can instead copy [terraform.tfvars.example](iac/terraform.tfvars.example) to `iac/terraform.tfvars` and fill in the values manually. For guest accounts, specify a verified deployment-tenant domain using `LAB_TEST_USER_DOMAIN`.

Deployment creates infrastructure, builds and deploys the application, checks readiness and assigns the $50 policy. **It does not wait for test-user MFA or claim E2E success.** First-time provisioning can take tens of minutes. Run the interactive test separately:

```bash
terraform -chdir=iac output -raw test_user_password
node scripts/lab/e2e-budget.mjs --device-code --verify-block
```

Use the newly displayed device code and the configured test account; complete MFA if requested. The test makes a small real model call, verifies actual spending, temporarily lowers the lab allowance to verify `429 budget_exceeded`, then restores $50. Keep it running until restoration finishes. Results print to the terminal and are saved privately to `.lab/e2e-report.json` on success.

Open the dashboard URL printed by deployment, sign in as the configured administrator, enter the tenant ID and select **Load budgets**. [Deployment and testing](docs/AZURE_BUDGET_LAB.md) covers permissions, reuse flags, viewing results, troubleshooting and cleanup.

PostgreSQL defaults to the small burstable B1ms size. If the region rejects that allocation, set `postgresql_sku_name` to an available alternative in your private variable file; see [PostgreSQL deployment notes](docs/POSTGRESQL.md).

## What is deployed

APIM, a Foundry account/project/model, the policy engine Container App, Azure Managed Redis, the VNet-connected hosting environment, ACR, PostgreSQL, Key Vault, private endpoints/DNS, managed identities, Entra applications/test user and monitoring. Platforms default to creation; `create_*` flags and `existing_*` inputs support reuse. Redis creation is optional: `create_redis=false` requires a reachable existing Redis connection. Running the engine without Redis is not supported. The default managed cache uses a private endpoint, TLS and managed identity; `redis_sku_name` and `redis_high_availability_enabled` control its size and replicas. Existing services must satisfy the documented identity, schema and network prerequisites; setting a flag does not configure an existing service automatically.

## Repository structure

```text
docs/                         Customer solution, deployment and budget guides
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

The application retains access-plan, routing and analytics services used by the budget flow. Only the USD-budget APIM template is shipped. Alternative deployment stacks, unrelated policy templates, demos and benchmark projects have been removed.

## Development and handling deployment state

```bash
dotnet build src/AIPolicyEngine.slnx --configuration Release
dotnet test src/AIPolicyEngine.Tests/AIPolicyEngine.Tests.csproj --configuration Release
cd src/aipolicyengine-ui
npm ci
npm run build
npm test
```

Backend development requires .NET 10. Terraform state is local and contains credentials. Protect and back it up; do not share state, plans, variable files or tokens with the customer. A clean clone creates a separate environment. Retain the MIT [license](LICENSE) and its attribution when distributing the repository.

Regional resources default to **Sweden Central** (`swedencentral`). PostgreSQL uses a small burstable server with private networking; its POC password is generated by Terraform and injected as a Container Apps secret. The application creates its SQL schema at startup. See [PostgreSQL storage](docs/POSTGRESQL.md) and [interactive testing](docs/INTERACTIVE_TEST.md).

Terraform provisions lab secrets with an ARM PUT action because the ARM secret resource does not support DELETE. Destroying the newly created vault removes its secrets with the vault; when using an existing vault, these written lab secrets remain in that vault and require customer-managed cleanup.
