import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';

export const root = resolve(fileURLToPath(new URL('../..', import.meta.url)));
export const sleep = milliseconds => new Promise(done => setTimeout(done, milliseconds));
export function outputs() {
  const data = JSON.parse(execFileSync('terraform', ['-chdir=iac', 'output', '-json'], { cwd: root, encoding: 'utf8' }));
  return Object.fromEntries(Object.entries(data).map(([name, output]) => [name, output.value]));
}
export async function jsonRequest(url, token, method = 'GET', body) {
  const response = await fetch(url, {
    method,
    headers: { Authorization: `Bearer ${token}`, ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) },
    body: body === undefined ? undefined : JSON.stringify(body),
    signal: AbortSignal.timeout(180000),
  });
  const text = await response.text();
  let data;
  try { data = JSON.parse(text); } catch { data = { error: text.slice(0, 500) }; }
  return { response, data };
}
export async function requireJson(url, token, method = 'GET', body) {
  const result = await jsonRequest(url, token, method, body);
  if (!result.response.ok) throw new Error(`${method} ${new URL(url).pathname}: HTTP ${result.response.status}: ${JSON.stringify(result.data)}`);
  return result.data;
}
export async function acquireToken(configuration, user = false, interactive = false) {
  if (user && interactive) return acquireDeviceToken(configuration);
  const fields = user
    ? { client_id: configuration.test_client_id, scope: `api://${configuration.gateway_client_id}/access_as_user`, grant_type: 'password', username: configuration.test_user_upn, password: configuration.test_user_password }
    : { client_id: configuration.admin_client_id, client_secret: configuration.admin_client_secret, scope: `api://${configuration.api_client_id}/.default`, grant_type: 'client_credentials' };
  const response = await fetch(`https://login.microsoftonline.com/${configuration.tenant_id}/oauth2/v2.0/token`, {
    method: 'POST', body: new URLSearchParams(fields), signal: AbortSignal.timeout(30000),
  });
  const data = await response.json();
  if (!response.ok) throw new Error(`Entra sign-in failed: ${data.error}: ${data.error_description}. For MFA/existing users, supply LAB_USER_TOKEN from an interactive delegated login.`);
  return data.access_token;
}

async function acquireDeviceToken(configuration) {
  const authority = `https://login.microsoftonline.com/${configuration.tenant_id}/oauth2/v2.0`;
  const response = await fetch(`${authority}/devicecode`, {
    method: 'POST', body: new URLSearchParams({ client_id: configuration.test_client_id, scope: `api://${configuration.gateway_client_id}/access_as_user` }),
    signal: AbortSignal.timeout(30000),
  });
  const device = await response.json();
  if (!response.ok) throw new Error(`Device sign-in failed: ${device.error}: ${device.error_description}`);
  console.log(`Sign in as ${configuration.test_user_upn}. ${device.message}`);
  console.log('Complete any required MFA enrollment. Retrieve the test password locally with: terraform -chdir=iac output -raw test_user_password');
  const expiresAt = Date.now() + device.expires_in * 1000;
  let interval = Math.max(device.interval || 5, 5) * 1000;
  while (Date.now() < expiresAt) {
    await sleep(interval);
    const tokenResponse = await fetch(`${authority}/token`, {
      method: 'POST', body: new URLSearchParams({ grant_type: 'urn:ietf:params:oauth:grant-type:device_code', client_id: configuration.test_client_id, device_code: device.device_code }),
      signal: AbortSignal.timeout(30000),
    });
    const token = await tokenResponse.json();
    if (tokenResponse.ok) return token.access_token;
    if (token.error === 'authorization_pending') continue;
    if (token.error === 'slow_down') { interval += 5000; continue; }
    throw new Error(`Device sign-in failed: ${token.error}: ${token.error_description}`);
  }
  throw new Error('Device sign-in expired. Run again with --device-code when ready.');
}
