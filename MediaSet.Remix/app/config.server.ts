/**
 * Server-side configuration from environment variables
 * These are runtime-configurable and NOT baked into the build
 * Note: appVersion is build-time only and not included here
 */

import { clientApiUrl } from './constants.server.ts';

export interface ClientConfig {
  apiUrl: string;
  showErrorDetails: boolean;
}

export function getClientConfig(): ClientConfig {
  return {
    // Client-side API URL must be accessible from the browser
    apiUrl: clientApiUrl,
    showErrorDetails: (process.env.SHOW_ERROR_DETAILS ?? '').toString().toLowerCase() === 'true',
  };
}
