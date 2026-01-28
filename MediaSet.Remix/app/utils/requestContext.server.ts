import { AsyncLocalStorage } from "async_hooks";

export interface RequestContext {
  traceId: string;
  spanId: string;
  traceparent: string;
}

// Store the request context (traceId, spanId, traceparent) for the current request
const requestContextStorage = new AsyncLocalStorage<RequestContext>();

/**
 * Generates a random hex string of the specified length.
 * Used for creating W3C trace IDs and span IDs.
 */
function generateRandomHex(length: number): string {
  return Array.from(crypto.getRandomValues(new Uint8Array(length / 2)))
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("");
}

/**
 * Gets or initializes the request context with W3C traceparent header format.
 * If a context already exists for this request, returns it.
 * Otherwise, creates a new context with W3C traceparent format: 00-{traceId}-{spanId}-{traceFlags}
 * - 00: W3C version (always 0)
 * - traceId: 32 hex characters (128-bit)
 * - spanId: 16 hex characters (64-bit)
 * - traceFlags: 2 hex characters (01 for sampled/recorded, 00 for not sampled)
 */
export function initializeRequestContext(): RequestContext {
  // Return existing context if already initialized
  const existingContext = requestContextStorage.getStore();
  if (existingContext) {
    return existingContext;
  }

  const traceId = generateRandomHex(32); // 128-bit trace ID
  const spanId = generateRandomHex(16); // 64-bit span ID
  const traceparent = `00-${traceId}-${spanId}-01`; // 01 = sampled/recorded

  const context: RequestContext = { traceId, spanId, traceparent };
  requestContextStorage.enterWith(context);
  return context;
}

export function getTraceparent(): string | null {
  const context = requestContextStorage.getStore();
  return context?.traceparent ?? null;
}

export function getRequestContext(): RequestContext | null {
  return requestContextStorage.getStore() ?? null;
}

export function withRequestContext<T>(
  context: RequestContext,
  fn: () => T | Promise<T>
): T | Promise<T> {
  return requestContextStorage.run(context, fn);
}
