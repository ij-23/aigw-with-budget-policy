import assert from 'node:assert/strict';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { acquireToken, jsonRequest, outputs, requireJson, root, sleep } from './common.mjs';

const configuration = outputs();
assert.ok(configuration.test_user_object_id, 'The lab test user has not been provisioned. Complete Entra administrator sign-in and apply Terraform before running the authenticated budget test.');
const adminToken = process.env.LAB_ADMIN_TOKEN || await acquireToken(configuration);
const engine = configuration.dashboard_url;
const budgetBase = `${engine}/api/budgets/${configuration.tenant_id}`;
const plans = await requireJson(`${engine}/api/plans`, adminToken);
let plan = plans.plans.find(candidate => candidate.name === 'USD budget lab (no token allowance)');
if (!plan) plan = await requireJson(`${engine}/api/plans`, adminToken, 'POST', {
  name: 'USD budget lab (no token allowance)', monthlyRate: 0, monthlyTokenQuota: 0,
  enforceTokenQuota: false, tokensPerMinuteLimit: 1000000, requestsPerMinuteLimit: 120,
  allowOverbilling: false, costPerMillionTokens: 0, allowedDeployments: [configuration.deployment_name],
});
await requireJson(`${engine}/api/clients/${configuration.test_client_id}/${configuration.tenant_id}`, adminToken, 'PUT', { planId: plan.id });
const price = process.env.LAB_PRICE_JSON ? JSON.parse(process.env.LAB_PRICE_JSON) : {
  deploymentId: configuration.deployment_name, version: 'gpt-4.1-mini/2025-04-14/GlobalStandard/2026-10-09',
  inputUsdPerMillion: 0.40, outputUsdPerMillion: 1.60, cachedInputUsdPerMillion: 0.10,
  maxInputTokens: 1047576, maxOutputTokens: 32768,
};
const deployments = await requireJson(`${engine}/api/deployments`, adminToken);
const available = Array.isArray(deployments) ? deployments : deployments.deployments;
const deployment = available.find(candidate => candidate.name === configuration.deployment_name && candidate.resourceId.toLowerCase() === configuration.foundry_id.toLowerCase());
assert.ok(deployment, 'The policy engine must discover the deployed model');
if (!process.env.LAB_PRICE_JSON) {
  assert.equal(deployment.model, 'gpt-4.1-mini', 'Provide LAB_PRICE_JSON with verified rates and hard limits for other models');
  assert.equal(deployment.modelVersion, '2025-04-14');
  assert.equal(deployment.skuName, 'GlobalStandard');
}
let config = await requireJson(`${budgetBase}/configuration`, adminToken);
let policy = config.policies.find(candidate => candidate.name === 'Lab test user — $50/month');
if (!policy) {
  policy = { id: crypto.randomUUID(), name: 'Lab test user — $50/month', limitUsd: 50, period: 'Monthly', enabled: true };
  config.policies.push(policy);
}
assert.equal(policy.limitUsd, 50, 'Restore the lab policy to $50 before running');
assert.equal(policy.period, 'Monthly');
if (!config.assignments.some(assignment => assignment.policyId === policy.id && assignment.subjectType === 'User' && assignment.subjectId === configuration.test_user_object_id)) {
  config.assignments.push({ policyId: policy.id, subjectType: 'User', subjectId: configuration.test_user_object_id, groupMode: 'PerMember' });
}
config.models = [...config.models.filter(model => model.deploymentId !== price.deploymentId), price];
await requireJson(`${budgetBase}/configuration`, adminToken, 'PUT', config);
if (process.argv.includes('--setup-only')) {
  console.log(JSON.stringify({ dashboardUrl: `${engine}/budgets`, tenantId: configuration.tenant_id, testUser: configuration.test_user_upn, testUserObjectId: configuration.test_user_object_id, policyId: policy.id, allowanceUsd: 50, period: 'Monthly', inferencePerformed: false }, null, 2));
  process.exit(0);
}
const userToken = process.env.LAB_USER_TOKEN || await acquireToken(configuration, true, process.argv.includes('--device-code'));
const claims = JSON.parse(Buffer.from(userToken.split('.')[1], 'base64url').toString());
assert.equal(claims.oid, configuration.test_user_object_id, 'Delegated token must belong to the configured budget test user');
assert.equal(claims.tid, configuration.tenant_id);
assert.ok(claims.scp?.split(' ').includes('access_as_user'), 'A delegated user token is required');
const actualClientId = claims.azp || claims.appid;
assert.ok(actualClientId, 'Delegated token must identify its client application');
if (actualClientId !== configuration.test_client_id) {
  await requireJson(`${engine}/api/clients/${actualClientId}/${configuration.tenant_id}`, adminToken, 'PUT', { planId: plan.id });
}
const before = await requireJson(`${budgetBase}/balances`, adminToken);
const priorBalance = before.find(balance => balance.policyId === policy.id && balance.subject === `user:${configuration.test_user_object_id}` && new Date(balance.resetsAt) > new Date());
const request = { messages: [{ role: 'user', content: 'Say hello and explain in one short sentence that this request uses my monthly AI budget.' }], max_completion_tokens: 100, stream: false };
const inference = await jsonRequest(configuration.apim_chat_url, userToken, 'POST', request);
assert.equal(inference.response.status, 200, `APIM inference failed: ${JSON.stringify(inference.data)}`);
const requestId = inference.response.headers.get('x-budget-request-id');
assert.ok(requestId, 'APIM must return X-Budget-Request-Id');
let reservation;
for (let attempt = 0; attempt < 10; attempt++) {
  const reservations = await requireJson(`${budgetBase}/reservations`, adminToken);
  reservation = reservations.find(candidate => candidate.id === `reservation:${requestId}` || candidate.id === requestId);
  if (reservation?.settledAt) break;
  await sleep(1000);
}
assert.ok(reservation?.settledAt, `Request ${requestId} did not settle. Retain its reservation and investigate; do not assume a refund.`);
assert.equal(inference.response.headers.get('x-budget-settlement'), 'settled');
const usage = inference.data.usage;
assert.ok(usage && usage.prompt_tokens > 0);
assert.equal(reservation.usage.promptTokens, usage.prompt_tokens);
assert.equal(reservation.usage.completionTokens, usage.completion_tokens);
const nanoCost = BigInt(usage.prompt_tokens - (usage.prompt_tokens_details?.cached_tokens || 0)) * BigInt(Math.round(price.inputUsdPerMillion * 1000000))
  + BigInt(usage.completion_tokens) * BigInt(Math.round(price.outputUsdPerMillion * 1000000))
  + BigInt(usage.prompt_tokens_details?.cached_tokens || 0) * BigInt(Math.round(price.cachedInputUsdPerMillion * 1000000));
const expectedUsd = Number((nanoCost + 999n) / 1000n) / 1000000000;
assert.equal(reservation.actualUsd, expectedUsd, 'Ledger cost must match provider usage and pinned price');
const balances = await requireJson(`${budgetBase}/balances`, adminToken);
const balance = balances.find(candidate => reservation.balanceIds.includes(candidate.id));
assert.ok(balance);
assert.equal(balance.limitUsd, 50);
assert.ok(Math.abs(balance.spentUsd - (priorBalance?.spentUsd || 0) - expectedUsd) < 0.000000002);
assert.equal(balance.reservedUsd, priorBalance?.reservedUsd || 0);
let blockResult;
if (process.argv.includes('--verify-block')) {
  async function updateLimit(limitUsd) {
    const latest = await requireJson(`${budgetBase}/configuration`, adminToken);
    latest.policies = latest.policies.map(candidate => candidate.id === policy.id ? { ...candidate, limitUsd } : candidate);
    await requireJson(`${budgetBase}/configuration`, adminToken, 'PUT', latest);
  }
  try {
    await updateLimit(Math.max(balance.spentUsd, 0.000000001));
    const blocked = await jsonRequest(configuration.apim_chat_url, userToken, 'POST', request);
    assert.equal(blocked.response.status, 429, `Expected budget rejection, got ${JSON.stringify(blocked.data)}`);
    assert.equal(blocked.data.code, 'budget_exceeded');
    blockResult = { status: blocked.response.status, code: blocked.data.code, resetsAt: blocked.data.resetsAt };
    const after = await requireJson(`${budgetBase}/balances`, adminToken);
    assert.equal(after.find(candidate => candidate.id === balance.id).spentUsd, balance.spentUsd, 'Rejected inference must not add spending');
  } finally {
    await updateLimit(50);
  }
}
const report = {
  testedAt: new Date().toISOString(), testUser: { upn: configuration.test_user_upn, objectId: configuration.test_user_object_id },
  tenantId: configuration.tenant_id, policyId: policy.id, allowanceUsd: 50, period: 'Monthly',
  dashboardUrl: `${engine}/budgets`, apimResourceId: configuration.apim_resource_id,
  requestId, model: inference.data.model, providerUsage: usage, requestCostUsd: reservation.actualUsd,
  spentUsd: balance.spentUsd, reservedUsd: balance.reservedUsd, remainingUsd: balance.remainingUsd,
  resetsAt: balance.resetsAt, blockTest: blockResult, allowanceRestoredToUsd: 50,
  response: inference.data.choices?.[0]?.message?.content,
};
mkdirSync(join(root, '.lab'), { recursive: true, mode: 0o700 });
writeFileSync(join(root, '.lab', 'e2e-report.json'), JSON.stringify(report, null, 2), { mode: 0o600 });
console.log(JSON.stringify(report, null, 2));
console.log(`Open ${engine}/budgets, sign in as the dashboard administrator, enter tenant ${configuration.tenant_id}, and click Load budgets.`);
