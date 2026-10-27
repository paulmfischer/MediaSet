import { clientEntry, on, type Handle } from 'remix/component';

export function HydrationCounter(handle: Handle) {
  let count: number = 0;
  return () => (
    <button
      type="button"
      id="hydration-probe"
      class="block mt-4 !bg-blue-600 !text-white px-3 py-1 rounded"
      mix={on('click', () => {
        count += 1;
        void handle.update();
      })}
    >
      Hydration probe: {count}
    </button>
  );
}

export const Counter = clientEntry(import.meta.url, HydrationCounter);
