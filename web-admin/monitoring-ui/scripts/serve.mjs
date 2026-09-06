import http from 'node:http';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
const root = fileURLToPath(new URL('../dist/', import.meta.url));
const gateway = new URL(process.env.MONITORING_GATEWAY_URL || 'http://127.0.0.1:5000');
if (!['http:', 'https:'].includes(gateway.protocol) || gateway.username || gateway.password || gateway.pathname !== '/' || gateway.search || gateway.hash || gateway.protocol === 'http:' && !['localhost', '127.0.0.1', '[::1]'].includes(gateway.hostname)) {
  throw new Error('Gateway must be a credential-free HTTPS origin, or loopback HTTP for development.');
}
const port = Number(process.env.PORT || 5184);
if (!Number.isInteger(port) || port < 1024 || port > 65535) throw new Error('Invalid local port.');
const routes = new Map([['/gateway/auth/v1/sign-in', 'POST'], ['/gateway/auth/v1/logout', 'POST'], ['/gateway/admin/monitoring', 'GET'], ['/gateway/admin/monitoring/jobs', 'GET']]);
const mime = {
  '.html': 'text/html',
  '.js': 'text/javascript',
  '.css': 'text/css',
  '.svg': 'image/svg+xml',
  '.txt': 'text/plain'
};
const assets = new Set(['/', '/index.html', '/app.js', '/styles.css', '/THIRD-PARTY-NOTICES.txt', ...['Activity', 'Server', 'ListChecks', 'TriangleAlert', 'ChartNoAxesCombined', 'LifeBuoy', 'RefreshCw', 'LogOut', 'Search', 'ShieldCheck'].map(name => '/icons/' + name + '.svg')]);
const server = http.createServer(async (req, res) => {
  res.setHeader('Cache-Control', 'no-store');
  res.setHeader('X-Content-Type-Options', 'nosniff');
  res.setHeader('Referrer-Policy', 'no-referrer');
  res.setHeader('Content-Security-Policy', "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'");
  const safeError = status => {
    res.writeHead(status, {
      'Content-Type': 'application/json'
    });
    res.end('{"code":"monitoring_unavailable"}');
  };
  try {
    if (!['127.0.0.1:' + port, 'localhost:' + port].includes(req.headers.host)) return safeError(403);
    const url = new URL(req.url, 'http://127.0.0.1:' + port);
    if (url.search) return safeError(400);
    if (routes.has(url.pathname)) {
      if (req.method !== routes.get(url.pathname)) return safeError(405);
      if (req.headers.origin && !['http://127.0.0.1:' + port, 'http://localhost:' + port].includes(req.headers.origin)) return safeError(403);
      if (req.headers['sec-fetch-site'] === 'cross-site') return safeError(403);
      const chunks = [];
      let size = 0;
      for await (const chunk of req) {
        size += chunk.length;
        if (size > 16384) return safeError(413);
        chunks.push(chunk);
      }
      const upstream = await fetch(new URL(url.pathname.slice('/gateway'.length), gateway), {
        method: req.method,
        redirect: 'error',
        signal: AbortSignal.timeout(15000),
        headers: {
          'Content-Type': 'application/json',
          ...(req.headers.authorization ? {
            Authorization: req.headers.authorization
          } : {})
        },
        body: req.method === 'POST' ? Buffer.concat(chunks) : undefined
      });
      if (!upstream.ok) return safeError(upstream.status);
      const reader = upstream.body?.getReader();
      const output = [];
      let length = 0;
      if (reader) {
        for (;;) {
          const {
            done,
            value
          } = await reader.read();
          if (done) break;
          length += value.length;
          if (length > 1048576) {
            await reader.cancel();
            return safeError(502);
          }
          output.push(value);
        }
      }
      res.writeHead(upstream.status, {
        'Content-Type': 'application/json'
      });
      res.end(Buffer.concat(output));
      return;
    }
    if (req.method !== 'GET' || !assets.has(url.pathname)) return safeError(404);
    const file = path.join(root, url.pathname === '/' ? 'index.html' : url.pathname.slice(1));
    const data = await readFile(file);
    res.writeHead(200, {
      'Content-Type': mime[path.extname(file)]
    });
    res.end(data);
  } catch {
    safeError(502);
  }
});
server.requestTimeout = 20000;
server.headersTimeout = 10000;
server.listen(port, '127.0.0.1', () => console.log(`Monitoring UI: http://127.0.0.1:${port}`));
