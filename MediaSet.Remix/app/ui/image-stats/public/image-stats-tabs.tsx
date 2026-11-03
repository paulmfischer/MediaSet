import { clientEntry, on, type Handle, type RemixNode } from 'remix/component';

export interface ImageStatsTab {
  id: string;
  /** Sets --entity-color for the tab button. */
  className: string;
  label: RemixNode;
  panel: RemixNode;
}

interface ImageStatsTabsProps {
  tabs: ImageStatsTab[];
  /** The tab to show first; defaults to the first tab. */
  initialTab?: string;
}

// Per-entity tabs. The server renders every panel; the island only picks which one is visible.
export const ImageStatsTabs = clientEntry(
  import.meta.url,
  function ImageStatsTabs(handle: Handle<ImageStatsTabsProps>) {
    const { tabs: initialTabs, initialTab } = handle.props;
    let activeId = initialTabs.find((tab) => tab.id === initialTab)?.id ?? initialTabs[0]?.id ?? '';

    return () => {
      const { tabs } = handle.props;
      if (tabs.length === 0) return null;
      const active = tabs.find((tab) => tab.id === activeId) ?? tabs[0];

      return (
        <div>
          <div class="grid gap-4 mb-1 sm:grid-cols-2 lg:grid-cols-4">
            {tabs.map((tab) => {
              const isActive = tab.id === active.id;
              return (
                <button
                  type="button"
                  id={`image-stats-tab-${tab.id}`}
                  aria-pressed={isActive ? 'true' : 'false'}
                  aria-controls={`image-stats-panel-${tab.id}`}
                  class={`tertiary !p-6 w-full text-left ${tab.className}${
                    isActive
                      ? ' relative -mb-[5px] z-10 !bg-zinc-800 !border-x-zinc-700 !border-b-zinc-900 !rounded-b-none !border-t-entity'
                      : ''
                  }`}
                  mix={on('click', () => {
                    activeId = tab.id;
                    handle.update();
                  })}
                >
                  {tab.label}
                </button>
              );
            })}
          </div>
          {tabs.map((tab) => (
            <div
              id={`image-stats-panel-${tab.id}`}
              role="region"
              aria-labelledby={`image-stats-tab-${tab.id}`}
              hidden={tab.id !== active.id}
              class="relative rounded-lg rounded-tl-none rounded-tr-none border border-zinc-700"
            >
              {tab.panel}
            </div>
          ))}
        </div>
      );
    };
  }
);
