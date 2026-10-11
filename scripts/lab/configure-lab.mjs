import { execFileSync } from 'node:child_process';
import { existsSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { root } from './common.mjs';

const destination = join(root, 'iac', 'terraform.tfvars');
if (existsSync(destination)) throw new Error('iac/terraform.tfvars already exists. Edit it to reuse this environment; configuration was not overwritten.');
function azure(args) {
  return JSON.parse(execFileSync('az', [...args, '--output', 'json'], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'inherit'] }));
}
const account = azure(['account', 'show']);
if (account.user?.type !== 'user') throw new Error('Sign in as the intended POC administrator, or configure terraform.tfvars manually for service-principal deployment.');
const user = azure(['ad', 'signed-in-user', 'show']);
const domain = process.env.LAB_TEST_USER_DOMAIN || user.userPrincipalName.split('@')[1];
if (!domain || (user.userPrincipalName.includes('#EXT#') && !process.env.LAB_TEST_USER_DOMAIN)) throw new Error('Set test_user_domain manually to a verified domain of the deployment tenant.');
const prefix = process.env.LAB_NAME_PREFIX || `aigw${Date.now().toString(36)}`;
if (!/^[a-z][a-z0-9]{2,17}$/.test(prefix)) throw new Error('LAB_NAME_PREFIX must contain 3–18 lowercase letters/digits and start with a letter.');
const settings = {
  subscription_id: account.id, tenant_id: account.tenantId, admin_user_object_id: user.id,
  publisher_email: process.env.LAB_PUBLISHER_EMAIL || user.mail || user.userPrincipalName,
  test_user_domain: process.env.LAB_TEST_USER_DOMAIN || domain,
  name_prefix: prefix, resource_group_name: `rg-${prefix}`, location: process.env.LAB_LOCATION || 'swedencentral',
};
writeFileSync(destination, Object.entries(settings).map(([key, value]) => `${key} = ${JSON.stringify(value)}`).join('\n') + '\n', { flag: 'wx', mode: 0o600 });
console.log(`Created private configuration for subscription ${account.id}, tenant ${account.tenantId}, resource group ${settings.resource_group_name}.`);
console.log('Review iac/terraform.tfvars before deployment. Confirm the test-user domain belongs to this tenant and the selected region has model capacity.');
