import { clientEntry, on, ref, type Handle } from 'remix/component';

interface PlexLinkDialogProps {
  /** The link page URL: the no-JavaScript target of the button and the flow the dialog loads. */
  href: string;
  /** "Link to Plex" or "Relink". */
  label: string;
}

const fragmentUrl = (url: URL): URL => {
  const next = new URL(url);
  next.searchParams.set('fragment', '1');
  return next;
};

// Link control for the Plex card. Without JavaScript (or before it loads) the button opens the server-rendered
// link page. Once hydrated it opens a dialog and loads the same flow into it from the same route (the
// `fragment=1` form of the page), so the search, the scope step and the failure page are the ones the page
// uses. Plex is contacted only after the button is pressed, never on page load.
export const PlexLinkDialog = clientEntry(
  import.meta.url,
  function PlexLinkDialog(handle: Handle<PlexLinkDialogProps>) {
    let dialog: HTMLDialogElement | undefined;
    let content: HTMLElement | undefined;
    let loading = false;
    let controller: AbortController | undefined;

    // Loads one step of the flow into the dialog. A failure answer (Plex unreachable, ...) is an error page, so
    // the browser goes to the full page for it instead.
    async function load(url: URL) {
      if (!dialog || !content) return;
      controller?.abort();
      controller = new AbortController();
      loading = true;
      handle.update();
      try {
        const response = await fetch(fragmentUrl(url), { signal: controller.signal, headers: { Accept: 'text/html' } });
        if (!response.ok) {
          window.location.assign(url);
          return;
        }
        content.innerHTML = await response.text();
        content.querySelector<HTMLElement>('input[type="search"], input:not([type="hidden"])')?.focus();
      } catch (error) {
        if (!(error instanceof DOMException && error.name === 'AbortError')) window.location.assign(url);
      } finally {
        loading = false;
        handle.update();
      }
    }

    // The flow's own links and forms keep working as pages; inside the dialog the GET ones load a step instead.
    function onDialogClick(event: Event) {
      const target = event.target as Element | null;
      if (target?.closest('[data-plex-close]')) {
        dialog?.close();
        return;
      }
      const link = target?.closest<HTMLAnchorElement>('a[data-plex-step]');
      if (link) {
        event.preventDefault();
        void load(new URL(link.href));
      }
    }

    function onDialogSubmit(event: Event) {
      const form = event.target as HTMLFormElement;
      if (form.method.toLowerCase() !== 'get') return;
      event.preventDefault();
      const url = new URL(form.action);
      for (const [name, value] of new FormData(form)) {
        if (typeof value === 'string') url.searchParams.set(name, value);
      }
      void load(url);
    }

    // A link posts as a top-frame reload (the page is patched in place); close the dialog when it completes.
    handle.queueTask(() => {
      const top = handle.frames.top;
      top.addEventListener('reloadComplete', () => dialog?.close(), { signal: handle.signal });
    });

    return () => {
      const { href, label } = handle.props;
      return (
        <>
          <form
            method="get"
            action={href}
            mix={on('submit', (event) => {
              if (!dialog) return;
              event.preventDefault();
              dialog.showModal();
              void load(new URL(href, window.location.href));
            })}
          >
            <button type="submit" class="secondary">
              {label}
            </button>
          </form>
          <dialog
            aria-label={label}
            class="m-auto w-full max-w-2xl rounded-md border border-gray-600 bg-gray-800 p-4 text-white backdrop:bg-black/60"
            mix={[
              ref((node) => (dialog = node as HTMLDialogElement)),
              on('click', (event) => onDialogClick(event)),
              on('submit', (event) => onDialogSubmit(event)),
            ]}
          >
            <p role="status" class={loading ? 'text-sm' : 'sr-only'}>
              {loading ? 'Loading…' : ''}
            </p>
            <div mix={ref((node) => (content = node as HTMLElement))} aria-busy={loading ? 'true' : 'false'} />
          </dialog>
        </>
      );
    };
  }
);
