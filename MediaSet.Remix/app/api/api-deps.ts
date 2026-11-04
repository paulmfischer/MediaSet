import { apiFetch } from '../utils/apiFetch.server.ts';
import { serverLogger } from '../utils/serverLogger.ts';

/**
 * The collaborators the API data functions use. Every data function takes them as a trailing
 * argument that defaults to the real implementations, so callers never pass them and tests can
 * supply fakes without module mocking.
 */
export interface ApiDeps {
  apiFetch: typeof apiFetch;
  serverLogger: Pick<typeof serverLogger, 'info' | 'warn' | 'error' | 'debug'>;
}

export const defaultApiDeps: ApiDeps = { apiFetch, serverLogger };
