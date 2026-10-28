import { clientEntry, on, ref, type Handle } from 'remix/component';

// Replaces useState + useEffect outside-click handling from root.tsx's Header.
export const MobileMenu = clientEntry(
  import.meta.url,
  function MobileMenu(handle: Handle<{ links: [string, string][] }>) {
    let open = false;

    handle.queueTask(() => {
      document.addEventListener(
        'mousedown',
        (event) => {
          if (open && !(event.target as Element).closest('[data-mobile-menu]')) {
            open = false;
            handle.update();
          }
        },
        { signal: handle.signal }
      );
    });

    return () => (
      <div data-mobile-menu class="md:hidden">
        <button
          type="button"
          aria-label="Toggle menu"
          aria-expanded={open}
          mix={on('click', () => {
            open = !open;
            handle.update();
          })}
        >
          {open ? 'Close' : 'Menu'}
        </button>
        {open && (
          <nav class="absolute right-0 top-full bg-zinc-700 flex flex-col gap-2 p-2">
            {handle.props.links.map(([label, href]) => (
              <a key={href} href={href} class="p-3 rounded-lg">
                {label}
              </a>
            ))}
          </nav>
        )}
      </div>
    );
  }
);
