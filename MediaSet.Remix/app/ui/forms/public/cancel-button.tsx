import { clientEntry, on, type Handle } from 'remix/component';

interface CancelButtonProps {
  /** Where Cancel goes when the page can't go back, and without JavaScript. */
  fallbackHref: string;
}

// Cancel for a form: goes back in history, as the old UI did. Without JavaScript it is a native
// submit button that loads `fallbackHref` with a GET, which skips validation and saves nothing.
export const CancelButton = clientEntry(import.meta.url, function CancelButton(handle: Handle<CancelButtonProps>) {
  return () => (
    <button
      type="submit"
      class="secondary"
      formmethod="get"
      formaction={handle.props.fallbackHref}
      formenctype="application/x-www-form-urlencoded"
      formnovalidate
      mix={on('click', (event) => {
        event.preventDefault();
        if (window.history.length > 1) window.history.back();
        else window.location.assign(handle.props.fallbackHref);
      })}
    >
      Cancel
    </button>
  );
});
