import type { Handle } from 'remix/component';

import { SaveButton } from '../forms/public/save-button.tsx';

interface UnlinkPageProps {
  title: string;
  action: string;
  cancelHref: string;
  /** The unlink failed; shown above the form. */
  formError?: string | undefined;
}

// The unlink confirmation form. It is the no-JavaScript path; the Plex card's dialog posts to the same action.
export function UnlinkPage(handle: Handle<UnlinkPageProps>) {
  return () => {
    const { title, action, cancelHref, formError } = handle.props;
    return (
      <div class="flex flex-col gap-4 px-2 max-w-3xl mx-auto">
        <h1 class="text-2xl">Unlink from Plex</h1>
        {formError ? (
          <p role="alert" class="rounded-md border border-red-500/50 bg-red-500/10 p-3 text-red-300">
            {formError}
          </p>
        ) : null}
        <p>
          Unlink <strong>{title}</strong> from Plex? This deletes the Plex data stored for it. The movie itself is kept,
          and you can link it again later.
        </p>
        <form method="post" action={action} class="flex gap-2 justify-end">
          <a href={cancelHref} class="px-3 py-1">
            Cancel
          </a>
          <SaveButton label="Unlink" pendingLabel="Unlinking…" />
        </form>
      </div>
    );
  };
}
