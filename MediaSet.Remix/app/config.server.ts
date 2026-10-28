/**
 * Server-side configuration from environment variables
 * These are runtime-configurable and NOT baked into the build
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

/**
 * Version shown in the footer. Read from the environment per request, so a container can set it at
 * run time. VITE_APP_VERSION is the legacy name the current image build still sets.
 */
export function getAppVersion(): string {
  return process.env.APP_VERSION || process.env.VITE_APP_VERSION || '0.0.0-local';
}
