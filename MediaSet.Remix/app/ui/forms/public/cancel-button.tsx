import { clientEntry, navigate, on, type Handle } from 'remix/component';

interface CancelButtonProps {
  /** Where Cancel goes: the collection page for an add form, the item's page for an edit form. */
  fallbackHref: string;
}

// Cancel for a form: leaves for `fallbackHref`. It does not use history.back(): every form submission
// (a failed save, a lookup, applying a result) adds a history entry for the same URL, so stepping back
// would land on an earlier copy of the form. Without JavaScript it is a native submit button that loads
// the same page with a GET, which skips validation and saves nothing.
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
        void navigate(handle.props.fallbackHref);
      })}
    >
      Cancel
    </button>
  );
});
