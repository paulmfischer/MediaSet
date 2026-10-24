/**
 * Server-side API URL. Native development calls the API on the host;
 * production deployments supply their container-to-container URL.
 */
export const baseUrl =
  process.env.apiUrl || (process.env.NODE_ENV === 'development' ? 'http://127.0.0.1:5000' : 'http://localhost:7130');

/**
 * Browser API URL. Vite proxies development requests on the UI's origin,
 * including HTTPS mobile requests. Production supplies an external API URL.
 */
export const clientApiUrl =
  process.env.clientApiUrl || (process.env.NODE_ENV === 'development' ? '/dev-api' : 'http://localhost:7130');
