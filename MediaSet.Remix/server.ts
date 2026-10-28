import * as fs from 'node:fs';
import * as http from 'node:http';
import * as https from 'node:https';
import { createRequestListener } from 'remix/node-fetch-server';

import { withDevApiProxy } from './app/dev-api-proxy.ts';
import { router } from './app/router.ts';

const port = process.env.PORT ? Number.parseInt(process.env.PORT, 10) : 3000;
const isProduction = process.env.NODE_ENV === 'production';

const handler = isProduction
  ? router.fetch
  : withDevApiProxy(router.fetch, process.env.apiUrl || 'http://127.0.0.1:5000');

// Mobile HTTPS mode (camera scanning needs a secure context), enabled by `npm run dev:mobile`.
function readHttpsOptions(): https.ServerOptions | null {
  if (isProduction || process.env.MEDIASET_DEV_HTTPS !== '1') return null;

  const { DEV_HTTPS_CERT, DEV_HTTPS_KEY } = process.env;
  if (!DEV_HTTPS_CERT || !DEV_HTTPS_KEY) {
    throw new Error(
      'Mobile HTTPS requires DEV_HTTPS_CERT and DEV_HTTPS_KEY in .env.local. See Development/DEVELOPMENT.md#mobile-https-and-barcode-scanning.'
    );
  }
  return { cert: fs.readFileSync(DEV_HTTPS_CERT), key: fs.readFileSync(DEV_HTTPS_KEY) };
}

const httpsOptions = readHttpsOptions();
const listener = createRequestListener(handler);
const server = httpsOptions ? https.createServer(httpsOptions, listener) : http.createServer(listener);

server.on('error', (error) => {
  console.error(error);
  process.exit(1);
});

server.listen(port, () => {
  console.log(`Local: ${httpsOptions ? 'https' : 'http'}://localhost:${port}/`);
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
