import { clientEntry, type Handle } from 'remix/component';

// Shows a timestamp in the viewer's locale and time zone. The server renders the ISO value, and the
// browser swaps in the localized text after hydration, so the server's locale never leaks into the page.
export const LocalDateTime = clientEntry(import.meta.url, function LocalDateTime(handle: Handle<{ value: string }>) {
  let localized: string | null = null;

  handle.queueTask(() => {
    localized = new Date(handle.props.value).toLocaleString();
    handle.update();
  });

  return () => <time datetime={handle.props.value}>{localized ?? handle.props.value}</time>;
});
