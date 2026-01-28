import { initializeRequestContext } from "./requestContext.server";

/**
 * Custom fetch wrapper that automatically adds W3C traceparent header from request context.
 * This enables distributed tracing between the Remix server and the MediaSet API.
 * 
 * The traceparent header format: 00-{traceId}-{spanId}-{traceFlags}
 * - Allows the API to extract and reuse the same trace ID for all operations
 * - Enables end-to-end tracing from UI through backend services
 * 
 * The request context (and traceparent header) is initialized on first fetch call
 * and reused for all subsequent API calls within the same request.
 */
export async function apiFetch(
  url: string | URL,
  init?: RequestInit
): Promise<Response> {
  // Get or initialize request context (returns existing or creates new)
  const { traceparent } = initializeRequestContext();
  
  const headers = new Headers(init?.headers ?? {});
  
  // Add W3C traceparent header (guaranteed to exist at this point)
  if (!headers.has("traceparent")) {
    headers.set("traceparent", traceparent);
  }
  
  return fetch(url, {
    ...init,
    headers,
  });
}
