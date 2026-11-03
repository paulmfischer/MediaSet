import type { Handle } from 'remix/component';

import { getShowErrorDetails } from '../config.server.ts';

interface ErrorPageProps {
  heading: string;
  message: string;
  /** Where to send the reader back to, e.g. the collection list. */
  backHref: string;
  backLabel: string;
  /** Message and stack of the error; only passed when the server allows showing them. */
  details?: string;
}

/** The text for the details block, or undefined unless SHOW_ERROR_DETAILS is on and there is an error. */
export function errorDetails(error: unknown): string | undefined {
  if (!getShowErrorDetails() || error === undefined || error === null) return undefined;
  if (error instanceof Error) return error.stack ?? `${error.name}: ${error.message}`;
  if (error instanceof Response) return `${error.status} ${error.statusText}`.trim();
  return String(error);
}

// Shown in place of a page that is missing or cannot be loaded.
export function ErrorPage(handle: Handle<ErrorPageProps>) {
  return () => {
    const { heading, message, backHref, backLabel, details } = handle.props;
    return (
      <div class="flex flex-col items-center gap-3 text-center py-8">
        <h1 class="text-2xl">{heading}</h1>
        <p>{message}</p>
        <a href={backHref}>{backLabel}</a>
        {details ? (
          <pre
            data-testid="error-details"
            class="max-w-full overflow-x-auto text-left text-sm p-3 rounded-lg bg-zinc-200 dark:bg-zinc-800"
          >
            {details}
          </pre>
        ) : null}
      </div>
    );
  };
}
