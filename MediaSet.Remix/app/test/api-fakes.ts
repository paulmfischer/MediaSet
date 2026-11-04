import { expect } from 'remix/assert';
import type { TestContext } from 'remix/test';

import type { ApiDeps } from '../api/api-deps.ts';

type FakeResponse = Partial<Response> | { ok: boolean; status?: number; json?: () => Promise<unknown> };

export interface ApiFakes {
  deps: ApiDeps;
  apiFetch: ApiDeps['apiFetch'] & ReturnType<TestContext['mock']['fn']>;
  logger: Record<'info' | 'warn' | 'error' | 'debug', ReturnType<TestContext['mock']['fn']>>;
  /** Queue the response the next unanswered apiFetch call resolves with. */
  respondOnce(response: FakeResponse): void;
  /** Queue an error the next unanswered apiFetch call rejects with. */
  rejectOnce(error: unknown): void;
  /** Forget recorded apiFetch calls, like a mid-test reset. */
  clearCalls(): void;
}

/**
 * Fake `apiFetch` and `serverLogger` for the API data functions. Queued responses are used in
 * order; once the queue is empty apiFetch answers with an ok, empty JSON response, so nothing
 * reaches the network.
 */
export function createApiFakes(t: TestContext): ApiFakes {
  const queue: Array<{ response?: FakeResponse; error?: unknown }> = [];
  const apiFetch = t.mock.fn(async () => {
    const next = queue.shift();
    if (next?.error !== undefined) throw next.error;
    return (next?.response ?? { ok: true, status: 200, json: async () => ({}) }) as Response;
  }) as unknown as ApiFakes['apiFetch'];
  const logger = {
    info: t.mock.fn(),
    warn: t.mock.fn(),
    error: t.mock.fn(),
    debug: t.mock.fn(),
  };
  return {
    deps: { apiFetch, serverLogger: logger },
    apiFetch,
    logger,
    respondOnce: (response) => void queue.push({ response }),
    rejectOnce: (error) => void queue.push({ error }),
    clearCalls: () => void (apiFetch.mock.calls.length = 0),
  };
}

/** Asserts a call whose URL contains `urlPart` and whose remaining arguments equal `rest`. */
export function expectFetched(apiFetch: ApiFakes['apiFetch'], urlPart: string, ...rest: unknown[]): void {
  const matched = apiFetch.mock.calls.some((call) => {
    const [url, ...others] = call.arguments;
    if (typeof url !== 'string' || !url.includes(urlPart) || others.length !== rest.length) return false;
    try {
      others.forEach((arg, index) => expect(arg).toEqual(rest[index]));
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
