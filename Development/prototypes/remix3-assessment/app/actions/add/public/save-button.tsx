import { clientEntry, type Handle } from 'remix/component';

export const SaveButton = clientEntry(import.meta.url, function SaveButton(handle: Handle) {
  let pending = false;
  let set = (value: boolean) => () => {
    pending = value;
    handle.update();
  };
  handle.frame.addEventListener('reloadStart', set(true), { signal: handle.signal });
  handle.frame.addEventListener('reloadComplete', set(false), { signal: handle.signal });

  return () => (
    <button type="submit" disabled={pending}>
      {pending ? 'Saving…' : 'Save'}
    </button>
  );
});
