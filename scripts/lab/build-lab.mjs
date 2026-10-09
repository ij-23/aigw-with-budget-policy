import { spawnSync } from 'node:child_process';
import { cpSync, mkdirSync, mkdtempSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { outputs, root, sleep } from './common.mjs';

function run(args) {
  const result = spawnSync('az', args, { cwd: root, stdio: 'inherit' });
  if (result.status !== 0) throw new Error(`Azure command failed: az ${args[0]} ${args[1]}`);
}
const configuration = outputs();
const image = process.env.LAB_IMAGE || `${configuration.registry_server}/aigw-budget:lab-${Date.now()}`;
if (!process.env.LAB_IMAGE) {
  // Upload only application sources: no local binaries, state, credentials or dependencies.
  const context = mkdtempSync(join(tmpdir(), 'aigw-build-'));
  const excluded = new Set(['bin', 'obj', 'node_modules', 'wwwroot', 'dist', '.git', '.env', '.env.local']);
  try {
    for (const path of ['src/Dockerfile', 'src/Directory.Packages.props', 'src/AIPolicyEngine.Api', 'src/AIPolicyEngine.ServiceDefaults', 'src/aipolicyengine-ui', 'policies/templates']) {
      cpSync(join(root, path), join(context, path), { recursive: true, filter: source => !excluded.has(source.split('/').at(-1)) && !source.split('/').at(-1).startsWith('.env.') });
    }
    run(['acr', 'build', '--subscription', configuration.subscription_id, '--registry', configuration.registry_name, '--image', image, '--file', 'src/Dockerfile', context]);
  } finally {
    rmSync(context, { recursive: true, force: true });
  }
}
mkdirSync(join(root, '.lab'), { recursive: true, mode: 0o700 });
writeFileSync(join(root, '.lab', 'image.json'), JSON.stringify({ image, builtAt: new Date().toISOString() }), { mode: 0o600 });
if (process.argv.includes('--build-only')) {
  console.log(`Built image: ${image}`);
  process.exit(0);
}
run(['containerapp', 'update', '--subscription', configuration.subscription_id, '--ids', configuration.policy_engine_id, '--image', image, '--output', 'none']);
mkdirSync(join(root, '.lab'), { recursive: true, mode: 0o700 });
writeFileSync(join(root, '.lab', 'apim-policy.xml'), configuration.rendered_policy, { mode: 0o600 });
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
