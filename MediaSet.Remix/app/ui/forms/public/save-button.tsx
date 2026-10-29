import { clientEntry, type Handle } from 'remix/component';

interface SaveButtonProps {
  label?: string;
  pendingLabel?: string;
}

// Submit button that shows pending state while the form submission (a top-frame reload) is in flight.
export const SaveButton = clientEntry(import.meta.url, function SaveButton(handle: Handle<SaveButtonProps>) {
  let pending = false;

  const setPending = (value: boolean) => {
    pending = value;
    handle.update();
  };

  handle.queueTask(() => {
    const top = handle.frames.top;
    top.addEventListener('reloadStart', () => setPending(true), { signal: handle.signal });
    top.addEventListener('reloadComplete', () => setPending(false), { signal: handle.signal });
  });

  return () => {
    const { label = 'Save', pendingLabel = 'Saving…' } = handle.props;
    return (
      <button type="submit" disabled={pending} aria-disabled={pending ? 'true' : undefined}>
        {pending ? pendingLabel : label}
      </button>
    );
  };
});
