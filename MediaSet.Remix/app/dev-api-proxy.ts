import { createFetchProxy } from 'remix/fetch-proxy';

type FetchHandler = (request: Request) => Promise<Response> | Response;

const devApiPrefix = /^\/dev-api(?=\/|$)/;

/**
 * Development only: browser API and image requests use the same-origin /dev-api
 * prefix (also over mobile HTTPS), which is stripped and forwarded to the local API.
 * Every other request goes to the app handler.
 */
export function withDevApiProxy(
  handler: FetchHandler,
  apiUrl: string,
  options?: { fetch?: typeof globalThis.fetch }
): FetchHandler {
  const proxy = createFetchProxy(apiUrl, options);

  return (request) => {
    const url = new URL(request.url);
    if (!devApiPrefix.test(url.pathname)) return handler(request);

    url.pathname = url.pathname.replace(devApiPrefix, '') || '/';
    return proxy(new Request(url, request));
  };
}
