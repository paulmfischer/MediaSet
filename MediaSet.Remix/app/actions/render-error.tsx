import type { RemixNode } from 'remix/component';

import { getShowErrorDetails } from '../config.server.ts';
import { routes } from '../routes.ts';
import { Document } from '../ui/document.tsx';
import { ErrorPage } from '../ui/error-page.tsx';
import { serverLogger } from '../utils/serverLogger.ts';

/** The parts of a request context an error page needs; kept structural so the router can use it. */
export interface ErrorContext {
  url: URL;
  render(node: RemixNode, init?: ResponseInit): Response | Promise<Response>;
}

interface ErrorOptions {
  heading?: string;
  message?: string;
  /** Page title; defaults to the heading. */
  title?: string;
  backHref?: string;
  backLabel?: string;
  headers?: HeadersInit;
}

const DEFAULTS: Record<number, { heading: string; message: string }> = {
  404: { heading: 'Page not found', message: "We couldn't find the page you were looking for." },
  500: { heading: 'Something went wrong', message: 'An unexpected error occurred. Please try again.' },
};

/** The text for the details block, or undefined unless SHOW_ERROR_DETAILS is on and there is an error. */
export function errorDetails(error: unknown): string | undefined {
  if (!getShowErrorDetails() || error === undefined || error === null) return undefined;
  if (error instanceof Error) return error.stack ?? `${error.name}: ${error.message}`;
  if (error instanceof Response) return `${error.status} ${error.statusText}`.trim();
  return String(error);
}

/**
 * Renders the shared error page in the site layout. The request path comes from the context, so
 * the header's active link is right. `error` only shows when SHOW_ERROR_DETAILS is on.
 */
export async function renderError(
  context: ErrorContext,
  status: number,
  error?: unknown,
  options: ErrorOptions = {}
): Promise<Response> {
  const fallback = DEFAULTS[status] ?? DEFAULTS[500];
  const heading = options.heading ?? fallback.heading;
  try {
    return await context.render(
      <Document path={context.url.pathname} title={`${options.title ?? heading} | MediaSet`}>
        <ErrorPage
          heading={heading}
          message={options.message ?? fallback.message}
          backHref={options.backHref ?? routes.home.href()}
          backLabel={options.backLabel ?? 'Back to Home'}
          details={errorDetails(error)}
        />
      </Document>,
      { status, headers: options.headers }
    );
  } catch (renderFailure) {
    // The layout itself failed to render; answer with plain text rather than throw out of the error handler.
    serverLogger.error('Failed to render the error page', renderFailure, { operation: 'renderError', status });
    return new Response(status === 404 ? 'Not Found' : 'Internal Server Error', {
      status,
      headers: { 'Content-Type': 'text/plain; charset=utf-8', ...Object.fromEntries(new Headers(options.headers)) },
    });
  }
}

/**
 * Builds the "item missing or could not be loaded" renderer an entity controller shares: the page
 * with its own heading and message and a link back to the collection.
 */
export function entityErrorPage(backHref: string, backLabel: string) {
  return (context: ErrorContext, status: number, heading: string, message: string, error?: unknown) =>
    renderError(context, status, error, { heading, message, backHref, backLabel });
}
