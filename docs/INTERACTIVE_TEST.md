# Interactive $50 budget test

Run commands from the repository root after [deployment](AZURE_BUDGET_LAB.md). All required URLs and identities come from Terraform outputs, so this procedure works after an environment rebuild.

## 1. Open the administrator dashboard

```sh
terraform -chdir=iac output -raw dashboard_url
terraform -chdir=iac output -raw tenant_id
terraform -chdir=iac output -raw test_user_upn
```

Open the dashboard URL with `/budgets` appended. Sign in with the administrator configured during deployment. Enter the tenant ID and select **Load budgets**. Confirm the enabled **Lab test user — $50/month** allowance and its direct user assignment. The spending table may be empty until the user's first authorized request.

The budget dashboard requires the administrator role. The test user authenticates to APIM to consume their allowance; use separate browser profiles or a private window to avoid signing in as the wrong account.

## 2. Sign in as the test user and send a real request

```sh
node scripts/lab/e2e-budget.mjs --device-code
```

The terminal displays Microsoft's device-login address and a short-lived code. Open it in a private window, enter the code, and sign in as the exact user printed by the script. Retrieve the new test password locally if needed:

```sh
terraform -chdir=iac output -raw test_user_password
```

Complete any required MFA enrollment. Do not paste passwords or access tokens into chat, commit them, or use your administrator account for this login. The script keeps the delegated token in memory and sends one real text Chat Completions request through APIM. It verifies the model's usage and settled PostgreSQL balance, then writes a local report.

## 3. See actual consumption

Refresh **Load budgets** in the administrator dashboard. **Current balances** shows spent USD, outstanding reserved USD, remaining USD, and the period reset time. For a settled request, reserved funds return to zero and the actual charge is deducted from $50. Prices come from the configured model price book; this budget does not include Azure infrastructure charges.

Inspect the report in `.lab/e2e-report.json`. It includes the request ID, provider token usage, actual cost and resulting balance without credentials. **Pending reservations** shows unsettled requests when a callback fails; reconcile only after verifying actual backend usage.

## 4. Demonstrate blocking without spending $50

```sh
node scripts/lab/e2e-budget.mjs --device-code --verify-block
```

After a real inference, this option temporarily lowers the dedicated lab allowance to recorded spending. The next APIM call must return **HTTP 429** with `budget_exceeded` before reaching the model. The script restores $50 in a `finally` block. If the process is forcibly terminated during the block phase, restore $50 in **Allowance policies** before repeating the test.

## 5. Inspect the APIM policy

In Azure Portal, open the APIM resource identified by `apim_resource_id`, select **APIs → AI budget lab — $50 per user per month → All operations → Policies**. Inspect JWT validation, access precheck, dollar reservation and settlement. The source policy is [policy.xml](../policies/templates/entra-jwt-ai-budget/policy.xml); the rendered local policy is `.lab/apim-policy.xml`.

## After a rebuild

The old test credentials and tokens no longer apply. Terraform outputs contain new application IDs and the recreated test-user password. Run `node scripts/lab/e2e-budget.mjs --setup-only` to confirm the fresh allowance, then use device-code sign-in. Previous usage has been removed by the teardown.
