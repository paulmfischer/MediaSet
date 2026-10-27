import { privateApiUrl } from './config.server';

export async function getApiHealth(signal?: AbortSignal) {
  try {
    const response = await fetch(`${privateApiUrl().replace(/\/$/, '')}/health`, {
      signal: signal ? AbortSignal.any([signal, AbortSignal.timeout(5000)]) : AbortSignal.timeout(5000),
    });
    if (!response.ok) throw new Error('API health request failed');
    const health: unknown = await response.json();
    if (!health || typeof health !== 'object' || !('status' in health) || typeof health.status !== 'string') {
      throw new Error('Invalid API health response');
    }
    // Explicit allowlist: health payload also contains private database details.
    return { status: health.status };
  } catch {
    throw new Response('The API is unavailable. Please try again shortly.', {
      status: 503,
    });
  }
}
