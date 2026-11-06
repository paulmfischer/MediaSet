import { clientEntry, on, ref, type Handle, type RemixNode } from 'remix/component';

import { Icon } from '../icon.tsx';
import { Menu, X } from '../icons.ts';
import { dismissOnOutsideOrEscape } from './dismiss.ts';

// Mobile toggle and menu. The wrapper uses `display: contents` so the toggle sits in the header
// row and the open menu wraps onto its own full-width row. The server renders the links as children.
export const MobileMenu = clientEntry(import.meta.url, function MobileMenu(handle: Handle<{ children?: RemixNode }>) {
  let open = false;

  const close = () => {
    if (!open) return false;
    open = false;
    handle.update();
    return true;
  };

  handle.queueTask(() => {
    handle.frames.top.addEventListener('reloadStart', () => close(), { signal: handle.signal });
  });

  return () => (
    <div class="contents" mix={ref((node, signal) => dismissOnOutsideOrEscape(node, signal, close))}>
      <button
        type="button"
        class="md:hidden p-2 rounded-md"
        aria-label="Toggle menu"
        aria-haspopup="true"
        aria-controls="mobile-menu-panel"
        aria-expanded={open ? 'true' : 'false'}
        mix={on('click', () => {
          open = !open;
          handle.update();
        })}
      >
        <Icon icon={open ? X : Menu} />
      </button>
      {open ? (
        <nav id="mobile-menu-panel" aria-label="Mobile" class="md:hidden basis-full bg-zinc-200 dark:bg-zinc-700 z-50">
          <div class="flex flex-col gap-2">{handle.props.children}</div>
        </nav>
      ) : null}
    </div>
  );
});
