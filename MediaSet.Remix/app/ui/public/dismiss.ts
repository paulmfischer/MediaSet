/**
 * Dismisses a popup owned by `element` on a pointer press outside it or on Escape. `close`
 * returns whether it actually closed; after an Escape close, focus returns to the element's
 * toggle button. Listeners are tied to `signal`, which a `ref` callback aborts on removal.
 */
export function dismissOnOutsideOrEscape(element: Element, signal: AbortSignal, close: () => boolean): void {
  document.addEventListener(
    'mousedown',
    (event) => {
      if (!element.contains(event.target as Node)) close();
    },
    { signal }
  );
  document.addEventListener(
    'keydown',
    (event) => {
      if (event.key !== 'Escape') return;
      if (close()) element.querySelector<HTMLElement>('button[aria-expanded]')?.focus();
    },
    { signal }
  );
}
