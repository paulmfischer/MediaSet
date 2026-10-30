import type { Handle } from 'remix/component';

interface UnavailablePageProps {
  heading: string;
  message: string;
  /** Where to send the reader back to, e.g. the collection list. */
  backHref: string;
  backLabel: string;
}

// Shown in place of an item when it is missing or cannot be loaded.
export function UnavailablePage(handle: Handle<UnavailablePageProps>) {
  return () => {
    const { heading, message, backHref, backLabel } = handle.props;
    return (
      <div class="flex flex-col items-center gap-3 text-center py-8">
        <h1 class="text-2xl">{heading}</h1>
        <p>{message}</p>
        <a href={backHref}>{backLabel}</a>
      </div>
    );
  };
}
