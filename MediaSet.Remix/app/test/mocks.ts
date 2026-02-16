/**
 * Mock utilities for API calls and browser APIs
 */

/**
 * Mock API response helper
 */
export function mockApiResponse<T>(data: T, init?: ResponseInit): Response {
  return new Response(JSON.stringify(data), {
    status: 200,
    headers: {
      "Content-Type": "application/json",
    },
    ...init,
  });
}

/**
 * Mock API error response
 */
export function mockApiError(message: string, status: number = 500): Response {
  return new Response(
    JSON.stringify({
      error: message,
    }),
    {
      status,
      headers: {
        "Content-Type": "application/json",
      },
    }
  );
}

/**
 * Create a mock fetch for testing
 * Usage: global.fetch = mockFetch(...)
 */
export function createMockFetch(
  responses: Record<string, Response | ((url: string, init?: RequestInit) => Response)>
): typeof fetch {
  return async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = typeof input === "string" ? input : input.toString();

    // Find matching response
    for (const [pattern, response] of Object.entries(responses)) {
      // Simple pattern matching - can be improved with URL pattern matching
      if (url.includes(pattern)) {
        if (typeof response === "function") {
          return response(url, init);
        }
        return response;
      }
    }

    // Default: 404 if no match found
    return mockApiError("Not found", 404);
  };
}

/**
 * Mock localStorage
 */
export function mockLocalStorage() {
  const store: Record<string, string> = {};

  return {
    getItem: (key: string) => store[key] || null,
    setItem: (key: string, value: string) => {
      store[key] = value;
    },
    removeItem: (key: string) => {
      delete store[key];
    },
    clear: () => {
      Object.keys(store).forEach((key) => {
        delete store[key];
      });
    },
  };
}
