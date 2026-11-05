import { clientEntry, on, ref, type Handle } from 'remix/component';

// One confirm dialog for a whole list. Each row's delete control is a plain link to the delete page
// (`a[data-delete-href]`, which works without JavaScript); once hydrated, a click on one opens this dialog
// instead, naming the row's item, and the dialog posts to that row's delete URL. Delegating from the
// document keeps a long list to one island instead of one per row.
export const RowDeleteDialog = clientEntry(import.meta.url, function RowDeleteDialog(handle: Handle) {
  let dialog: HTMLDialogElement | undefined;
  let title = '';
  let href = '';
  let pending = false;

  const setPending = (value: boolean) => {
    pending = value;
    handle.update();
  };

  handle.queueTask(() => {
    document.addEventListener(
      'click',
      (event) => {
        if (!dialog || event.defaultPrevented || event.button !== 0) return;
        if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        const link = (event.target as Element | null)?.closest<HTMLAnchorElement>('a[data-delete-href]');
        if (!link) return;
        event.preventDefault();
        title = link.dataset.deleteTitle ?? '';
        href = link.dataset.deleteHref ?? link.href;
        handle.update();
        dialog.showModal();
      },
      { signal: handle.signal }
    );
    const top = handle.frames.top;
    top.addEventListener('reloadStart', () => setPending(true), { signal: handle.signal });
    top.addEventListener('reloadComplete', () => setPending(false), { signal: handle.signal });
  });

  return () => (
    <dialog
      aria-labelledby="row-delete-heading"
      class="m-auto rounded-md border border-gray-600 bg-gray-800 p-4 text-white backdrop:bg-black/60"
      mix={ref((node) => (dialog = node as HTMLDialogElement))}
    >
      <form method="post" action={href} class="flex flex-col gap-4">
        <h2 id="row-delete-heading" class="text-xl">
          Delete {title}?
        </h2>
        <p>This can't be undone.</p>
        <div class="flex gap-2 justify-end">
          <button type="button" mix={on('click', () => dialog?.close())}>
            Cancel
          </button>
          <button type="submit" disabled={pending} aria-disabled={pending ? 'true' : undefined}>
            {pending ? 'Deleting…' : 'Delete'}
          </button>
        </div>
      </form>
    </dialog>
  );
});
