import test from 'node:test';
import assert from 'node:assert/strict';
import { createMonitoringClient, validateDashboard, validateJobs } from '../src/api/client.mjs';
import { dashboard, jobs, session } from './fixtures.mjs';
for (const status of [200, 401, 403]) test(`late HTTP ${status} cannot contaminate a replacement session`, async () => {
  let release;
  let hold = true;
  const client = createMonitoringClient(async url => {
    if (url.endsWith('sign-in')) return new Response(JSON.stringify(session));
    if (url.endsWith('/monitoring') && hold) {
      hold = false;
      return new Promise(resolve => { release = resolve; });
    }
    return new Response(JSON.stringify(url.endsWith('/jobs') ? jobs : dashboard));
  });
  await client.signIn('synthetic@example.invalid', 'synthetic-password-only');
  const oldLoad = client.load();
  await client.signOut();
  await client.signIn('synthetic@example.invalid', 'synthetic-password-only');
  release(new Response(JSON.stringify(dashboard), { status }));
  await assert.rejects(oldLoad, status === 403 ? /admin_required/ : /sign_in_required/);
  assert.deepEqual(await client.load(), { dashboard, jobs });
});
test('validated contracts accept synthetic observations and reject unsafe display fields', () => {
  assert.equal(validateDashboard(dashboard), dashboard);
  assert.equal(validateJobs(jobs), jobs);
  assert.throws(() => validateDashboard({
    ...dashboard,
    dataClassification: 'raw-financial-data'
  }));
  assert.throws(() => validateDashboard({
    ...dashboard,
    services: [{
      ...dashboard.services[0],
      service: '<script>secret</script>'
    }]
  }));
  assert.throws(() => validateJobs({
    ...jobs,
    jobs: [{
      ...jobs.jobs[0],
      errorCategory: 'raw-provider-message'
    }]
  }));
  assert.throws(() => validateJobs({
    ...jobs,
    jobs: Array(201).fill(jobs.jobs[0])
  }));
});
test('client uses only gateway paths, no-store and bearer authentication; logout clears session', async () => {
  const calls = [];
  const client = createMonitoringClient(async (url, options) => {
    calls.push({
      url,
      options
    });
    return new Response(JSON.stringify(url.endsWith('sign-in') ? session : url.endsWith('/jobs') ? jobs : dashboard));
  });
  await assert.rejects(client.load(), /sign_in_required/);
  await client.signIn('synthetic@example.invalid', 'synthetic-password-only');
  assert.deepEqual(await client.load(), {
    dashboard,
    jobs
  });
  assert.equal(calls[1].options.headers.Authorization, 'Bearer ' + session.accessToken);
  assert.equal(calls[1].options.cache, 'no-store');
  assert.equal(calls[1].options.credentials, 'omit');
  assert.equal(calls[1].options.headers['X-Gateway-Roles'], undefined);
  await client.signOut();
  await assert.rejects(client.load(), /sign_in_required/);
  assert.equal(calls.at(-1).url, '/gateway/auth/v1/logout');
});
for (const status of [401, 403]) test(`HTTP ${status} clears authority without reflecting error payloads`, async () => {
  const client = createMonitoringClient(async url => url.endsWith('sign-in') ? new Response(JSON.stringify(session)) : new Response('private upstream error', {
    status
  }));
  await client.signIn('synthetic@example.invalid', 'synthetic-password-only');
  await assert.rejects(client.load(), status === 401 ? /sign_in_required/ : /admin_required/);
  await assert.rejects(client.load(), /sign_in_required/);
});
test('expired access session cannot load data', async () => {
  const client = createMonitoringClient(async () => new Response(JSON.stringify({
    ...session,
    accessTokenExpiresAtUtc: '2020-01-01T00:00:00Z'
  })));
  await client.signIn('synthetic@example.invalid', 'synthetic-password-only');
  await assert.rejects(client.load(), /sign_in_required/);
});
