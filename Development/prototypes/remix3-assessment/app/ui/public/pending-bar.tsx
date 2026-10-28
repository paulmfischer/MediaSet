import { clientEntry, type Handle } from 'remix/component';

// Equivalent of PendingNavigation (useNavigation().state !== 'idle') driven by frame reload events.
export const PendingBar = clientEntry(import.meta.url, function PendingBar(handle: Handle) {
  let pending = false;
  let set = (value: boolean) => () => {
    pending = value;
    handle.update();
  };
  handle.frame.addEventListener('reloadStart', set(true), {
    signal: handle.signal,
  });
  handle.frame.addEventListener('reloadComplete', set(false), {
    signal: handle.signal,
  });

  return () => (
    <div
      role="progressbar"
      aria-label="Loading"
      aria-hidden={pending ? undefined : true}
      class={`h-1 bg-blue-500 transition-opacity ${pending ? 'opacity-100' : 'opacity-0'}`}
    />
  );
});
