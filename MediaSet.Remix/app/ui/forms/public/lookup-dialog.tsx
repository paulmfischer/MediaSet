import { clientEntry, on, ref, type Handle } from 'remix/component';

export interface LookupDialogResult {
  title: string;
  /** Short facts that tell editions apart, such as authors, publisher and date. */
  details: string[];
  imageUrl?: string | undefined;
}

export interface LookupDialogProps {
  /** Names the kind of item in the dialog heading, for example "book". */
  itemLabel: string;
  status: 'results' | 'notFound' | 'unavailable' | 'error';
  /** The reason for an `error`. */
  message?: string | undefined;
  results: LookupDialogResult[];
  /** The `name` of the hidden field that carries the results back with an apply submission. */
  resultsField: string;
  /** The results serialized for that hidden field. */
  resultsValue: string;
}

const MESSAGES = {
  notFound: 'No matches were found. Check the title or identifier, or fill the form in by hand.',
  unavailable: 'Metadata lookup is not available for this kind of item.',
} as const;

// The lookup results, shown in a dialog over the form. Every button is a native submit button of
// the surrounding form (`intent` says what it does), so the dialog works without JavaScript as an
// open panel (`apply:<index>` and `dismiss` are the intents the add action reads); hydration makes it modal and lets Close dismiss it without a round trip.
export const LookupDialog = clientEntry(import.meta.url, function LookupDialog(handle: Handle<LookupDialogProps>) {
  let dialog: HTMLDialogElement | undefined;

  return () => {
    const { itemLabel, status, message, results, resultsField, resultsValue } = handle.props;
    // After every render, not just the first: a new lookup re-renders this same island, and it must
    // reopen the dialog the user closed. `open` is only the no-JavaScript fallback, which is not modal.
    handle.queueTask(() => {
      if (dialog?.matches(':modal')) return;
      if (dialog?.open) dialog.close();
      dialog?.showModal();
    });
    return (
      <dialog
        open
        aria-labelledby="lookup-dialog-title"
        class="w-full max-w-2xl rounded-md border border-gray-600 bg-gray-900 p-4 text-white backdrop:bg-black/60"
        mix={ref((node) => {
          dialog = node as HTMLDialogElement;
        })}
      >
        <div class="flex flex-col gap-3">
          <h2 id="lookup-dialog-title" class="text-xl">
            Look up {itemLabel}
          </h2>
          <input type="hidden" name={resultsField} value={resultsValue} />
          {status === 'error' ? (
            <p role="alert" class="rounded-md border border-red-500/50 bg-red-500/10 p-3 text-red-300">
              {message}
            </p>
          ) : null}
          {status === 'notFound' || status === 'unavailable' ? (
            <p role="status" class="rounded-md border border-yellow-500/50 bg-yellow-500/10 p-3 text-yellow-200">
              {MESSAGES[status]}
            </p>
          ) : null}
          {status === 'results' ? (
            <ul class="flex flex-col gap-3" aria-label="Matches">
              {results.map((result, index) => (
                <li class="flex gap-3 rounded-md border border-gray-700 p-3">
                  {result.imageUrl ? (
                    <img src={result.imageUrl} alt="" class="h-24 w-auto rounded-md object-contain" />
                  ) : null}
                  <div class="flex flex-1 flex-col gap-1">
                    <p class="font-medium">{result.title}</p>
                    {result.details.length > 0 ? (
                      <p class="text-sm text-gray-400">{result.details.join(' · ')}</p>
                    ) : null}
                  </div>
                  <button type="submit" name="intent" value={`apply:${index}`} class="self-start">
                    Use this result
                  </button>
                </li>
              ))}
            </ul>
          ) : null}
          <div class="flex justify-end">
            <button
              type="submit"
              name="intent"
              value="dismiss"
              class="secondary"
              mix={on('click', (event) => {
                event.preventDefault();
                dialog?.close();
              })}
            >
              Close
            </button>
          </div>
        </div>
      </dialog>
    );
  };
});
