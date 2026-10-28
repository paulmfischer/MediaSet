import * as fs from 'node:fs';
import * as http from 'node:http';
import * as https from 'node:https';
import { createFetchProxy } from 'remix/fetch-proxy';
import { createRequestListener } from 'remix/node-fetch-server';

import { router } from './app/router.ts';

const port = process.env.PORT ? Number.parseInt(process.env.PORT, 10) : 44100;
const isProduction = process.env.NODE_ENV === 'production';

// Dev-only: forward /dev-api/* to the .NET API (Vite's server.proxy equivalent on main).
const devApi = isProduction ? null : createFetchProxy(process.env.apiUrl || 'http://127.0.0.1:5000');

function handler(request: Request): Promise<Response> | Response {
  let url = new URL(request.url);
  if (devApi && /^\/dev-api(\/|$)/.test(url.pathname)) {
    url.pathname = url.pathname.replace(/^\/dev-api/, '') || '/';
    return devApi(new Request(url, request));
  }
  return router.fetch(request);
}

// Mobile HTTPS dev mode (camera access requires a secure context): same cert/key env vars as main.
const { DEV_HTTPS_CERT, DEV_HTTPS_KEY } = process.env;
const listener = createRequestListener(handler);
const server =
  DEV_HTTPS_CERT && DEV_HTTPS_KEY
    ? https.createServer({ cert: fs.readFileSync(DEV_HTTPS_CERT), key: fs.readFileSync(DEV_HTTPS_KEY) }, listener)
    : http.createServer(listener);

server.listen(port, () => {
  console.log(`Server listening on ${DEV_HTTPS_CERT ? 'https' : 'http'}://localhost:${port}`);
});

let shuttingDown = false;

function shutdown() {
  if (shuttingDown) return;
  shuttingDown = true;
  server.close(() => process.exit(0));
  server.closeAllConnections();
}

process.on('SIGINT', shutdown);
process.on('SIGTERM', shutdown);
