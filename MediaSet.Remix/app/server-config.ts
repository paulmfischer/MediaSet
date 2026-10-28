import * as fs from 'node:fs';
import type { ServerOptions } from 'node:https';

import { withDevApiProxy } from './dev-api-proxy.ts';

type Env = Record<string, string | undefined>;
type FetchHandler = (request: Request) => Promise<Response> | Response;

const DEFAULT_PORT = 3000;

/** Port from `PORT`, defaulting to 3000; anything but a valid port number is a startup error. */
export function readPort(env: Env): number {
  if (env.PORT === undefined || env.PORT === '') return DEFAULT_PORT;

  const port = Number(env.PORT);
  if (!/^\d+$/.test(env.PORT) || port < 1 || port > 65535) {
    throw new Error(`PORT must be an integer between 1 and 65535, got "${env.PORT}".`);
  }
  return port;
}

/** The app handler, wrapped with the /dev-api proxy outside production. */
export function createRequestHandler(
  appFetch: FetchHandler,
  env: Env,
  options?: { fetch?: typeof globalThis.fetch }
): FetchHandler {
  if (env.NODE_ENV === 'production') return appFetch;
  return withDevApiProxy(appFetch, env.apiUrl || 'http://127.0.0.1:5000', options);
}

/**
 * Mobile HTTPS mode (camera scanning needs a secure context), enabled by
 * `npm run dev:mobile`. Returns null for plain HTTP, and never serves HTTPS in production.
 */
export function readHttpsOptions(env: Env): ServerOptions | null {
  if (env.NODE_ENV === 'production' || env.MEDIASET_DEV_HTTPS !== '1') return null;

  const { DEV_HTTPS_CERT, DEV_HTTPS_KEY } = env;
  if (!DEV_HTTPS_CERT || !DEV_HTTPS_KEY) {
    throw new Error(
      'Mobile HTTPS requires DEV_HTTPS_CERT and DEV_HTTPS_KEY in .env.local. See Development/DEVELOPMENT.md#mobile-https-and-barcode-scanning.'
    );
  }
  return { cert: fs.readFileSync(DEV_HTTPS_CERT), key: fs.readFileSync(DEV_HTTPS_KEY) };
}
