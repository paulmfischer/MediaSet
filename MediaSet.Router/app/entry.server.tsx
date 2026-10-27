import { PassThrough } from 'node:stream';
import { createReadableStreamFromReadable } from '@react-router/node';
import { isbot } from 'isbot';
import { renderToPipeableStream } from 'react-dom/server';
import { ServerRouter, type EntryContext } from 'react-router';

export const streamTimeout = 5000;

export default function handleRequest(
  request: Request,
  responseStatusCode: number,
  responseHeaders: Headers,
  routerContext: EntryContext
) {
  return new Promise<Response>((resolve, reject) => {
    let shellRendered = false;
    const ready =
      isbot(request.headers.get('user-agent') || '') || routerContext.isSpaMode ? 'onAllReady' : 'onShellReady';
    const { pipe, abort } = renderToPipeableStream(<ServerRouter context={routerContext} url={request.url} />, {
      [ready]() {
        shellRendered = true;
        const body = new PassThrough();
        const stream = createReadableStreamFromReadable(body);
        responseHeaders.set('Content-Type', 'text/html');
        resolve(
          new Response(stream, {
            status: responseStatusCode,
            headers: responseHeaders,
          })
        );
        pipe(body);
      },
      onShellError: reject,
      onError(error: unknown) {
        responseStatusCode = 500;
        if (shellRendered) console.error(error);
      },
    });
    // Leave time to send rejected loader boundaries before aborting the stream.
    const timer = setTimeout(abort, streamTimeout + 1000);
    timer.unref();
  });
}
