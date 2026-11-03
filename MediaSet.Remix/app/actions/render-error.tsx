import type { RemixNode } from 'remix/component';

import { routes } from '../routes.ts';
import { Document } from '../ui/document.tsx';
import { ErrorPage, errorDetails } from '../ui/error-page.tsx';

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

/**
 * Renders the shared error page in the site layout. The request path comes from the context, so
 * the header's active link is right. `error` only shows when SHOW_ERROR_DETAILS is on.
 */
export function renderError(context: ErrorContext, status: number, error?: unknown, options: ErrorOptions = {}) {
  const fallback = DEFAULTS[status] ?? DEFAULTS[500];
  const heading = options.heading ?? fallback.heading;
  return context.render(
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
}
