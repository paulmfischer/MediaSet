import { clientEntry, on, ref, type Handle } from 'remix/component';

import { Icon } from '../../icon.tsx';
import { Trash2 } from '../../icons.ts';

interface ConfirmDeleteProps {
  /** What is being deleted, named in the dialog, e.g. the book's title. */
  title: string;
  /** The delete page URL: the link target without JavaScript and the form action the dialog posts to. */
  href: string;
}

// Delete control for the detail page. Without JavaScript (or before it loads) the link opens the
// delete page's confirmation form; once hydrated, the click opens a confirmation dialog instead.
export const ConfirmDelete = clientEntry(import.meta.url, function ConfirmDelete(handle: Handle<ConfirmDeleteProps>) {
  let dialog: HTMLDialogElement | undefined;

  return () => {
    const { title, href } = handle.props;
    return (
      <>
        <a
          href={href}
          aria-label="Delete"
          title="Delete"
          mix={on('click', (event) => {
            if (!dialog) return;
            event.preventDefault();
            dialog.showModal();
          })}
        >
          <Icon icon={Trash2} size={22} />
        </a>
        <dialog
          aria-labelledby="confirm-delete-heading"
          class="m-auto rounded-md border border-gray-600 bg-gray-800 p-4 text-white backdrop:bg-black/60"
          mix={ref((node) => (dialog = node as HTMLDialogElement))}
        >
          <form method="post" action={href} class="flex flex-col gap-4">
            <h2 id="confirm-delete-heading" class="text-xl">
              Delete {title}?
            </h2>
            <p>This can't be undone.</p>
            <div class="flex gap-2 justify-end">
              <button type="button" mix={on('click', () => dialog?.close())}>
                Cancel
              </button>
              <button type="submit">Delete</button>
            </div>
          </form>
        </dialog>
      </>
    );
  };
});
