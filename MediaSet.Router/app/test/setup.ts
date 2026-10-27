import '@testing-library/jest-dom';
import 'vitest-dom/extend-expect';
import { vi } from 'vitest';

// Mock global fetch to prevent any unmocked API calls from reaching the network
// Tests should explicitly mock their fetch calls, but this catches any that slip through
// and prevents ECONNREFUSED errors during test runs
global.fetch = vi.fn(async () => {
  // Return a default empty response to prevent network errors
  return new Response(JSON.stringify({}), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  });
});
