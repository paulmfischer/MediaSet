import { getTraceId, initializeRequestContext } from "./requestContext.server";

/**
 * Custom fetch wrapper that automatically adds X-Trace-Id header from request context.
 * If no traceId exists in context, initializes one on first use. This ensures all
 * API calls within a request are correlated with the same TraceId.
 */
export async function apiFetch(
  url: string | URL,
  init?: RequestInit
): Promise<Response> {
  let traceId = getTraceId();
  
  // Initialize traceId on first fetch call if not already set
  if (!traceId) {
    traceId = initializeRequestContext();
  }
  
  const headers = new Headers(init?.headers ?? {});
  
  // Add traceId header (guaranteed to exist at this point)
  if (!headers.has("X-Trace-Id")) {
    headers.set("X-Trace-Id", traceId);
  }
  
  return fetch(url, {
    ...init,
    headers,
  });
}
