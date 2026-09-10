import test from 'node:test';
import assert from 'node:assert/strict';
import http from 'node:http';
import { spawn } from 'node:child_process';
import { once } from 'node:events';
test('local proxy restricts routes, origins and forwarded trust headers', async () => {
  const calls = [];
  const upstream = http.createServer((request, response) => {
    calls.push({
      url: request.url,
      headers: request.headers
    });
    if (request.url.endsWith('/jobs')) {
      response.writeHead(302, {
        Location: 'http://example.invalid/private'
      });
      response.end();
      return;
    }
    response.writeHead(200, {
      'Content-Type': 'application/json'
    });
    response.end('{"safe":true}');
  });
  upstream.listen(0, '127.0.0.1');
  await once(upstream, 'listening');
  const reservation = http.createServer();
  reservation.listen(0, '127.0.0.1');
  await once(reservation, 'listening');
  const port = reservation.address().port;
  await new Promise(resolve => reservation.close(resolve));
  const child = spawn(process.execPath, ['scripts/serve.mjs'], {
    cwd: new URL('..', import.meta.url),
    env: {
      ...process.env,
      PORT: String(port),
      MONITORING_GATEWAY_URL: 'http://127.0.0.1:' + upstream.address().port
    },
    stdio: ['ignore', 'pipe', 'pipe']
  });
  const base = 'http://127.0.0.1:' + port;
  try {
    await Promise.race([once(child.stdout, 'data'), once(child, 'exit').then(() => {
      throw new Error('Proxy failed to start');
    }), new Promise((_, reject) => {
      const timer = setTimeout(() => reject(new Error('Proxy start timeout')), 10000);
      timer.unref();
    })]);
    assert.equal((await fetch(base + '/gateway/internal/monitoring/signals/jobs')).status, 404);
    assert.equal((await fetch(base + '/gateway/admin/monitoring?target=private')).status, 400);
    assert.equal((await fetch(base + '/gateway/admin/monitoring', {
      headers: {
        Origin: 'https://example.invalid'
      }
    })).status, 403);
    assert.equal(calls.length, 0);
    const response = await fetch(base + '/gateway/admin/monitoring', {
      headers: {
        Authorization: 'Bearer synthetic-only',
        'X-Gateway-Roles': 'admin',
        'X-Gateway-Authentication': 'must-not-forward'
      }
    });
    assert.equal(response.status, 200);
    assert.equal(response.headers.get('cache-control'), 'no-store');
    assert.equal(calls[0].headers.authorization, 'Bearer synthetic-only');
    assert.equal(calls[0].headers['x-gateway-roles'], undefined);
    assert.equal(calls[0].headers['x-gateway-authentication'], undefined);
    assert.equal((await fetch(base + '/gateway/admin/monitoring/jobs')).status, 502);
    assert.equal(calls.length, 2);
    assert.equal((await fetch(base + '/gateway/auth/v1/sign-in', {
      method: 'POST',
      body: 'x'.repeat(17000)
    })).status, 413);
    assert.equal(calls.length, 2);
  } finally {
    const exited = once(child, 'exit');
    child.kill();
    await exited;
    await new Promise(resolve => upstream.close(resolve));
  }
});
