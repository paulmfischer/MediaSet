import { clientEntry, on, ref, type Handle } from 'remix/component';

interface PlexUnlinkDialogProps {
  /** What is being unlinked, named in the dialog, e.g. the movie's title. */
  title: string;
  /** The unlink page URL: the no-JavaScript target of the button and the form action the dialog posts to. */
  href: string;
}

// Unlink control for the Plex card. Without JavaScript (or before it loads) the button opens the unlink page's
// confirmation form; once hydrated, it opens a confirmation dialog instead, like the delete control.
export const PlexUnlinkDialog = clientEntry(
  import.meta.url,
  function PlexUnlinkDialog(handle: Handle<PlexUnlinkDialogProps>) {
    let dialog: HTMLDialogElement | undefined;
    let pending = false;

    const setPending = (value: boolean) => {
      pending = value;
      handle.update();
    };

    // The dialog's form submits as a top-frame reload; block a second submit while it is in flight.
    handle.queueTask(() => {
      const top = handle.frames.top;
      top.addEventListener('reloadStart', () => setPending(true), { signal: handle.signal });
      top.addEventListener(
        'reloadComplete',
        () => {
          dialog?.close();
          setPending(false);
        },
        { signal: handle.signal }
      );
    });

    return () => {
      const { title, href } = handle.props;
      return (
        <>
          <form
            method="get"
            action={href}
            mix={on('submit', (event) => {
              if (!dialog) return;
              event.preventDefault();
              dialog.showModal();
            })}
          >
            <button type="submit" class="secondary">
              Unlink
            </button>
          </form>
          <dialog
            aria-labelledby="plex-unlink-heading"
            class="m-auto rounded-md border border-gray-600 bg-gray-800 p-4 text-white backdrop:bg-black/60"
            mix={ref((node) => (dialog = node as HTMLDialogElement))}
          >
            <form method="post" action={href} class="flex flex-col gap-4">
              <h2 id="plex-unlink-heading" class="text-xl">
                Unlink {title} from Plex?
              </h2>
              <p>This deletes the Plex data stored for it. The movie is kept, and you can link it again later.</p>
              <div class="flex gap-2 justify-end">
                <button type="button" mix={on('click', () => dialog?.close())}>
                  Cancel
                </button>
                <button type="submit" disabled={pending} aria-disabled={pending ? 'true' : undefined}>
                  {pending ? 'Unlinking…' : 'Unlink'}
                </button>
              </div>
            </form>
          </dialog>
        </>
      );
    };
  }
);
