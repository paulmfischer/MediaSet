import { useState, type ReactNode } from 'react';

export interface TabConfig {
  id: string;
  /** Content rendered inside the tab button. Can be a plain string or a full card layout. */
  label: ReactNode;
  /** Content rendered in the panel when this tab is active. */
  panel: ReactNode;
  /**
   * Tailwind class applied to the top border of the active tab button, used to give
   * each tab its own accent color (e.g. '!border-t-green-500').
   * Defaults to '!border-t-cyan-500'.
   */
  activeTopBorderClass?: string;
}

interface TabsProps {
  tabs: TabConfig[];
  /** Id of the tab that should be selected on first render. Defaults to the first tab. */
  defaultTabId?: string;
  /**
   * Tailwind grid-cols classes for the tab button row.
   * Defaults to 'sm:grid-cols-2 lg:grid-cols-4'.
   */
  tabGridClassName?: string;
}

export default function Tabs({ tabs, defaultTabId, tabGridClassName = 'sm:grid-cols-2 lg:grid-cols-4' }: TabsProps) {
  const [activeTabId, setActiveTabId] = useState<string>(defaultTabId ?? tabs[0]?.id ?? '');

  if (tabs.length === 0) return null;

  const activeTab = tabs.find((t) => t.id === activeTabId);

  return (
    <div>
      {/* Tab buttons */}
      <div className={`grid gap-4 ${tabGridClassName}`}>
        {tabs.map((tab) => {
          const isActive = activeTabId === tab.id;
          return (
            <button
              key={tab.id}
              onClick={() => setActiveTabId(tab.id)}
              className={`tertiary !p-6 w-full text-left ${
                isActive
                  ? `relative -mb-px z-10 !bg-zinc-800 !border-x-zinc-700 !border-b-zinc-900 !rounded-b-none ${tab.activeTopBorderClass ?? '!border-t-cyan-500'}`
                  : ''
              }`}
            >
              {tab.label}
            </button>
          );
        })}
      </div>

      {/* Active tab panel */}
      {activeTab && (
        <div className="relative rounded-lg rounded-tl-none rounded-tr-none border border-zinc-700">
          {activeTab.panel}
        </div>
      )}
    </div>
  );
}
