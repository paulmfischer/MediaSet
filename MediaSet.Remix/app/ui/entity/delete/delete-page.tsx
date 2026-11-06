import type { Handle } from 'remix/component';

import { SaveButton } from '../../forms/public/save-button.tsx';

interface DeletePageProps {
  /** Singular entity name, e.g. `Book`. */
  singular: string;
  title: string;
  action: string;
  cancelHref: string;
  /** The delete failed; shown above the form. */
  formError?: string | undefined;
}

// The delete confirmation form. It is the no-JavaScript path; the detail page's dialog posts to the same action.
export function DeletePage(handle: Handle<DeletePageProps>) {
  return () => {
    const { singular, title, action, cancelHref, formError } = handle.props;
    return (
      <div class="flex flex-col gap-4 px-2 max-w-3xl mx-auto">
        <h1 class="text-2xl">Delete {singular}</h1>
        {formError ? (
          <p role="alert" class="rounded-md border border-red-500/50 bg-red-500/10 p-3 text-red-300">
            {formError}
          </p>
        ) : null}
        <p>
          Delete <strong>{title}</strong>? This can't be undone.
        </p>
        <form method="post" action={action} class="flex gap-2 justify-end">
          <a href={cancelHref} class="px-3 py-1">
            Cancel
          </a>
          <SaveButton label="Delete" pendingLabel="Deleting…" />
        </form>
      </div>
    );
  };
}
