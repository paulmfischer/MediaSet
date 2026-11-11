import { clientEntry, on, ref, type Handle } from 'remix/component';

import { PLEX_LINK_FRAGMENT } from './plex-link-fragment.ts';

interface PlexLinkDialogProps {
  /** The link page URL: the no-JavaScript target of the button and the flow the dialog loads. */
  href: string;
  /** "Link to Plex" or "Relink". */
  label: string;
  /** The movie's detail page, where a finished link lands. */
  detailHref: string;
  /**
   * The first link of an unlinked movie: the button POSTs to the link action, which links a lone title match at once
   * and otherwise shows the results. Relink never sets it, so its search is always shown.
   */
  auto?: boolean;
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
    async function load(url: URL, form?: HTMLFormElement) {
      if (!dialog || !content) return;
      controller?.abort();
      controller = new AbortController();
      loading = true;
      handle.update();
      try {
        const response = await fetch(fragmentUrl(url), {
          method: form ? 'POST' : 'GET',
          body: form ? new URLSearchParams({ intent: 'auto' }) : null,
          signal: controller.signal,
          headers: { Accept: 'text/html' },
        });
        // A search with exactly one match is linked by the server, which redirects to the detail page. The redirect is
        // followed so its target can be told apart: only the detail page means "linked"; any other redirect is just
        // navigated to.
        if (response.redirected) {
          const target = new URL(response.url);
          if (target.pathname === new URL(handle.props.detailHref, window.location.href).pathname) {
            await finishLinked();
          } else {
            window.location.assign(target);
          }
          return;
        }
        if (!response.ok) {
          // The server refused (Plex unreachable, a failed link, ...). Sending the form for real shows the standard
          // error page; that repeats the search and, if it found one match, the link attempt. Nothing was linked by
          // the refused attempt, so repeating it is safe.
          if (form) {
            form.setAttribute('data-rmx-document', '');
            form.submit();
          } else {
            window.location.assign(url);
          }
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

    // The link was applied (the server redirected): show the detail page's new state and close. If the reload
    // fails the outcome is unknown, so go to the detail page rather than repeating anything.
    async function finishLinked() {
      try {
        await handle.frames.top.reload();
        dialog?.close();
      } catch {
        window.location.assign(handle.props.detailHref);
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

    // Posts the link from the dialog. A validation error comes back as the scope step (fragment=1) and replaces the
    // dialog's content, so the dialog stays open with the error; success is a redirect, so the page reloads in
    // place. Only an answer that clearly refused the link (an error page) is shown by submitting the form for real.
    // When the outcome is unknown (the request or the reload failed after the server may have linked) the form is
    // never sent again: the browser goes to the detail page, which shows whatever state resulted.
    async function post(form: HTMLFormElement) {
      if (!dialog || !content) return;
      const url = new URL(form.action);
      url.searchParams.set('fragment', '1');
      const { detailHref } = handle.props;
      loading = true;
      handle.update();
      let refused = false;
      try {
        const body = new URLSearchParams();
        for (const [name, value] of new FormData(form)) if (typeof value === 'string') body.append(name, value);
        const response = await fetch(url, {
          method: 'POST',
          body,
          redirect: 'manual',
          headers: { Accept: 'text/html' },
        });
        if (response.type === 'opaqueredirect') {
          await finishLinked();
          return;
        }
        const html = await response.text();
        if (response.status === 400 && html.includes(`data-plex-link="${PLEX_LINK_FRAGMENT}"`)) {
          content.innerHTML = html;
          content.querySelector<HTMLElement>('[aria-invalid="true"], input:not([type="hidden"])')?.focus();
          return;
        }
        refused = response.status >= 400;
      } catch {
        // Unknown outcome: handled below
      } finally {
        loading = false;
        handle.update();
      }
      if (!refused) {
        window.location.assign(detailHref);
        return;
      }
      // The server refused the link, so nothing was linked and sending it again cannot link twice: this replay
      // exists only so the browser renders the standard error page for the refusal. The frame runtime would keep
      // the current page for a server error, so `data-rmx-document` opts this form out and the browser navigates
      // itself. (`submit()` fires no submit event, so the dialog's own handler is not involved.)
      form.setAttribute('data-rmx-document', '');
      form.submit();
    }

    function onDialogSubmit(event: Event) {
      const form = event.target as HTMLFormElement;
      event.preventDefault();
      if (form.method.toLowerCase() === 'get') {
        const url = new URL(form.action);
        for (const [name, value] of new FormData(form)) {
          if (typeof value === 'string') url.searchParams.set(name, value);
        }
        void load(url);
      } else {
        void post(form);
      }
    }

    // Closing (Cancel, Escape or a finished link) drops the loaded step, so the next open starts fresh
    function onDialogClose() {
      controller?.abort();
      if (content) content.innerHTML = '';
    }

    // A link posts as a top-frame reload (the page is patched in place); close the dialog when it completes.
    handle.queueTask(() => {
      const top = handle.frames.top;
      top.addEventListener('reloadComplete', () => dialog?.close(), { signal: handle.signal });
    });

    return () => {
      const { href, label, auto } = handle.props;
      return (
        <>
          <form
            method={auto ? 'post' : 'get'}
            action={href}
            mix={on('submit', (event) => {
              if (!dialog) return;
              event.preventDefault();
              dialog.showModal();
              if (auto) {
                void load(new URL(href, window.location.href), event.currentTarget as HTMLFormElement);
              } else {
                void load(new URL(href, window.location.href));
              }
            })}
          >
            {auto ? <input type="hidden" name="intent" value="auto" /> : null}
            <button type="submit" class="secondary">
              {label}
            </button>
          </form>
          <dialog
            aria-label={label}
            aria-labelledby="plex-link-heading"
            class="m-auto w-full max-w-2xl rounded-md border border-gray-600 bg-gray-800 p-4 text-white backdrop:bg-black/60"
            mix={[
              ref((node) => (dialog = node as HTMLDialogElement)),
              on('click', (event) => onDialogClick(event)),
              on('submit', (event) => onDialogSubmit(event)),
              on('close', onDialogClose),
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
