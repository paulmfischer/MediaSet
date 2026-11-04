import { expect } from 'remix/assert';
import type { TestContext } from 'remix/test';

type FakeResponse = Partial<Response> | { ok: boolean; status?: number; json?: () => Promise<unknown> };

export interface FetchFakes {
  /** The mocked global fetch, which `apiFetch` calls with the trace headers added. */
  apiFetch: ReturnType<TestContext['mock']['fn']>;
  /** Queue the response the next unanswered fetch call resolves with. */
  respondOnce(response: FakeResponse): void;
  /** Queue an error the next unanswered fetch call rejects with. */
  rejectOnce(error: unknown): void;
  /** Forget recorded fetch calls, like a mid-test reset. */
  clearCalls(): void;
}

/**
 * Replaces the global fetch for one test (restored when the test ends) and silences the server
 * logger's console output. Queued responses are used in order; once the queue is empty fetch
 * answers with an ok, empty JSON response, so nothing reaches the network. The logger only posts
 * to Seq when ExternalLogging__Enabled is set, which tests never do.
 */
export function mockFetch(t: TestContext): FetchFakes {
  const queue: Array<{ response?: FakeResponse; error?: unknown }> = [];
  const apiFetch = t.mock.method(globalThis, 'fetch', async () => {
    const next = queue.shift();
    if (next?.error !== undefined) throw next.error;
    return (next?.response ?? { ok: true, status: 200, json: async () => ({}) }) as Response;
  });
  for (const method of ['log', 'warn', 'error', 'debug'] as const) {
    t.mock.method(console, method, () => undefined);
  }
  return {
    apiFetch,
    respondOnce: (response) => void queue.push({ response }),
    rejectOnce: (error) => void queue.push({ error }),
    clearCalls: () => void (apiFetch.mock.calls.length = 0),
  };
}

/** apiFetch passes a Headers object that includes the traceparent; compare plain headers without it. */
function withPlainHeaders(options: unknown): unknown {
  if (typeof options !== 'object' || options === null || !('headers' in options)) return options;
  const headers = Object.fromEntries(new Headers(options.headers as HeadersInit));
  delete headers['traceparent'];
  return { ...options, headers };
}

function isPartialMatcher(value: unknown): boolean {
  return typeof value === 'object' && value !== null && Object.getOwnPropertySymbols(value).length > 0;
}

/**
 * Asserts a fetch call whose URL contains `urlPart`. When `init` is given, the call's options must
 * contain it (extra properties are allowed; the traceparent header is ignored and header names are lowercase).
 */
export function expectFetched(apiFetch: FetchFakes['apiFetch'], urlPart: string, init?: unknown): void {
  const matched = apiFetch.mock.calls.some((call) => {
    const [url, rawOptions] = call.arguments;
    const options = withPlainHeaders(rawOptions);
    if (typeof url !== 'string' || !url.includes(urlPart)) return false;
    if (init === undefined) return true;
    try {
      if (isPartialMatcher(init)) expect(options).toEqual(init);
      else expect(options).toMatchObject(init as object);
      return true;
    } catch {
      return false;
    }
  });
  if (!matched) {
    throw new Error(
      `Expected a fetch of ${urlPart}; calls: ${JSON.stringify(apiFetch.mock.calls.map((c) => c.arguments))}`
    );
  }
}

export function mockApiResponse<T>(data: T, init?: ResponseInit): Response {
  return new Response(JSON.stringify(data), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
    ...init,
  });
}
