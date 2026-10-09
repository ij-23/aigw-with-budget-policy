import { spawnSync, execFileSync } from 'node:child_process';
import { mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { outputs, root, sleep } from './common.mjs';

function run(args) {
  const result = spawnSync('az', args, { cwd: root, stdio: 'inherit' });
  if (result.status !== 0) throw new Error(`Azure command failed: az ${args[0]} ${args[1]}`);
}
const configuration = outputs();
const image = `${configuration.registry_server}/aigw-budget:lab-${Date.now()}`;
run(['acr', 'build', '--subscription', configuration.subscription_id, '--registry', configuration.registry_name, '--image', image, '--file', 'src/Dockerfile', '.']);
run(['containerapp', 'update', '--subscription', configuration.subscription_id, '--ids', configuration.policy_engine_id, '--image', image, '--output', 'none']);
mkdirSync(join(root, '.lab'), { recursive: true, mode: 0o700 });
writeFileSync(join(root, '.lab', 'apim-policy.xml'), configuration.rendered_policy, { mode: 0o600 });
for (const [name, value] of Object.entries({ 'test-user-password': configuration.test_user_password, 'bootstrap-client-secret': configuration.admin_client_secret })) {
  if (!value) continue;
  const path = join(root, '.lab', `${name}.txt`);
  writeFileSync(path, value, { mode: 0o600 });
  try {
    execFileSync('az', ['keyvault', 'secret', 'set', '--subscription', configuration.subscription_id, '--vault-name', configuration.key_vault_name, '--name', name, '--file', path, '--output', 'none'], { cwd: root, stdio: ['ignore', 'pipe', 'pipe'] });
  } finally {
    rmSync(path, { force: true });
  }
}
let healthy = false;
for (let attempt = 0; attempt < 60; attempt++) {
  try {
    const response = await fetch(`${configuration.dashboard_url}/api/auth-config`, { signal: AbortSignal.timeout(10000) });
    if (response.ok && (await response.json()).clientId === configuration.api_client_id) { healthy = true; break; }
  } catch {}
  await sleep(5000);
}
if (!healthy) throw new Error('Policy engine did not become ready. Inspect Container Apps system and console logs.');
console.log(`Dashboard ready: ${configuration.dashboard_url}/budgets`);
console.log(`APIM request URL: ${configuration.apim_chat_url}`);
console.log('Rendered APIM policy: .lab/apim-policy.xml');
