/**
 * Server-side configuration from environment variables
 * These are runtime-configurable and NOT baked into the build
 * Note: appVersion is build-time only and not included here
 */

export interface ClientConfig {
  apiUrl: string;
  showErrorDetails: boolean;
}

export function getClientConfig(): ClientConfig {
  return {
    // Client-side API URL must be accessible from the browser
    apiUrl: process.env.clientApiUrl || 'http://localhost:7130',
    showErrorDetails: (process.env.SHOW_ERROR_DETAILS ?? '').toString().toLowerCase() === 'true',
  };
}
