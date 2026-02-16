/**
 * Server-side API URL - used for server-to-server communication within Docker
 * This uses Docker container names (e.g., http://mediaset-api:8080)
 */
export const baseUrl = process.env.apiUrl || 'http://localhost:7130';

/**
 * Client-side API URL - used for browser requests from the host machine
 * This must be accessible from the user's browser (e.g., http://localhost:8080)
 */
export const clientApiUrl = process.env.clientApiUrl || 'http://localhost:7130';
