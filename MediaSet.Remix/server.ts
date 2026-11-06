import * as http from 'node:http';
import * as https from 'node:https';
import { createRequestListener } from 'remix/node-fetch-server';

import { router } from './app/router.ts';
import { createRequestHandler, readHttpsOptions, readPort } from './app/server-config.ts';

const port = readPort(process.env);
const handler = createRequestHandler(router.fetch, process.env);
const httpsOptions = readHttpsOptions(process.env);
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
