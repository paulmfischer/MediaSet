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
    showErrorDetails: getShowErrorDetails(),
  };
}

/** Whether error pages may show the message and stack. Read per request so a container can set it at run time. */
export function getShowErrorDetails(): boolean {
  return (process.env.SHOW_ERROR_DETAILS ?? '').toString().toLowerCase() === 'true';
}

/**
 * Version shown in the footer. Read from the environment per request; the container image sets it
 * from the APP_VERSION build argument and a deployment can override it at run time.
 */
export function getAppVersion(): string {
  return process.env.APP_VERSION || '0.0.0-local';
}
