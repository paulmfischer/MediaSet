import { clientEntry, ref } from 'remix/component';

// Behaviour for the detail page's Plex gear menu, a native <details> that works without JavaScript. Once hydrated it
// also closes on Escape, on a press outside it, and after a dialog it holds (Relink, Unlink) closes. It renders
// nothing; it only attaches to the <details> it sits in.
export const PlexMenuBehavior = clientEntry(import.meta.url, function PlexMenuBehavior() {
  return () => (
    <span
      hidden
      mix={ref((node, signal) => {
        const menu = node.closest('details');
        if (!menu) return;
        const close = () => {
          menu.open = false;
        };
        document.addEventListener('mousedown', (event) => (menu.contains(event.target as Node) ? null : close()), {
          signal,
        });
        document.addEventListener(
          'keydown',
          (event) => {
            // While a dialog is open Escape belongs to the dialog
            if (event.key !== 'Escape' || !menu.open || menu.querySelector('dialog[open]')) return;
            close();
            menu.querySelector<HTMLElement>('summary')?.focus();
          },
          { signal }
        );
        // Tabbing out of the menu closes it; focus leaving the window (no related target) does not
        menu.addEventListener(
          'focusout',
          (event) => {
            const next = event.relatedTarget;
            if (next instanceof Node && !menu.contains(next) && !menu.querySelector('dialog[open]')) close();
          },
          { signal }
        );
        // `close` does not bubble, so listen in the capture phase
        menu.addEventListener('close', (event) => (event.target instanceof HTMLDialogElement ? close() : null), {
          capture: true,
          signal,
        });
      })}
    />
  );
});
