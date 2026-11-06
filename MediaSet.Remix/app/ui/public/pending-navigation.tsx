import { clientEntry, type Handle } from 'remix/component';

import { Spinner } from '../spinner.tsx';

// Shows a spinner over the page while the top frame reloads (client navigation).
export const PendingNavigation = clientEntry(import.meta.url, function PendingNavigation(handle: Handle) {
  let pending = false;

  const setPending = (value: boolean) => {
    if (pending === value) return;
    pending = value;
    handle.update();
  };

  handle.queueTask(() => {
    const top = handle.frames.top;
    top.addEventListener('reloadStart', () => setPending(true), { signal: handle.signal });
    top.addEventListener('reloadComplete', () => setPending(false), { signal: handle.signal });
  });

  // The live region stays mounted so assistive tech announces the text when it changes.
  return () => (
    <>
      <div role="status" class="sr-only">
        {pending ? 'Loading' : ''}
      </div>
      {pending ? (
        <>
          <div aria-hidden="true" class="fixed inset-0 2xl:mx-14 z-20 bg-gray-900 bg-opacity-60"></div>
          <div
            aria-hidden="true"
            class="fixed z-50 top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 flex justify-center text-slate-400"
          >
            <Spinner size={84} />
          </div>
        </>
      ) : null}
    </>
  );
});
