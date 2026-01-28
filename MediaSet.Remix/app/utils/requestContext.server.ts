import { AsyncLocalStorage } from "async_hooks";

// Store the traceId for the current request context
const requestContextStorage = new AsyncLocalStorage<{ traceId: string }>();

export function initializeRequestContext(): string {
  const traceId = crypto.randomUUID();
  requestContextStorage.enterWith({ traceId });
  return traceId;
}

export function getTraceId(): string | null {
  const context = requestContextStorage.getStore();
  return context?.traceId ?? null;
}

export function withRequestContext<T>(
  traceId: string,
  fn: () => T | Promise<T>
): T | Promise<T> {
  return requestContextStorage.run({ traceId }, fn);
}
