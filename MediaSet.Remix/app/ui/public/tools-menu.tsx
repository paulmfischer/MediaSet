import { clientEntry, on, ref, type Handle, type RemixNode } from 'remix/component';

import { Icon } from '../icon.tsx';
import { Settings } from '../icons.ts';
import { dismissOnOutsideOrEscape } from './dismiss.ts';

// Desktop tools dropdown. The server renders the links (and their active state) as children.
export const ToolsMenu = clientEntry(
  import.meta.url,
  function ToolsMenu(handle: Handle<{ active?: boolean; children?: RemixNode }>) {
    let open = false;

    const close = () => {
      if (!open) return false;
      open = false;
      handle.update();
      return true;
    };

    // Close when client navigation starts. Browser-only, so it runs after the first commit.
    handle.queueTask(() => {
      handle.frames.top.addEventListener('reloadStart', () => close(), { signal: handle.signal });
    });

    return () => (
      <div class="relative" mix={ref((node, signal) => dismissOnOutsideOrEscape(node, signal, close))}>
        <button
          type="button"
          class={`p-3 flex gap-2 items-center rounded-lg tertiary${handle.props.active ? ' active bg-zinc-300 dark:bg-zinc-800' : ''}`}
          aria-label="Tools"
          aria-haspopup="true"
          aria-controls="tools-menu-panel"
          aria-expanded={open ? 'true' : 'false'}
          mix={on('click', () => {
            open = !open;
            handle.update();
          })}
        >
          <Icon icon={Settings} />
        </button>
        {open ? (
          <div
            id="tools-menu-panel"
            class="absolute right-0 top-full mt-1 w-48 rounded-lg border border-zinc-600 bg-white dark:bg-zinc-800 shadow-lg z-50"
          >
            {handle.props.children}
          </div>
        ) : null}
      </div>
    );
  }
);
