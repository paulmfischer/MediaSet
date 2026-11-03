import type { IconNode } from 'lucide';
import type { Handle } from 'remix/component';

import { Icon } from '../icon.tsx';

interface StatCardProps {
  title: string;
  value: string | number;
  icon: IconNode;
  /** Classes colouring the icon badge. */
  colorClass: string;
}

export function StatCard(handle: Handle<StatCardProps>) {
  return () => {
    const { title, value, icon, colorClass } = handle.props;
    return (
      <div class="rounded-lg border border-zinc-700 bg-zinc-800/50 p-6 transition-all">
        <div class="flex items-center justify-between">
          <div class="flex-1">
            <p class="text-sm font-medium text-zinc-400">{title}</p>
            <p class="mt-2 text-3xl font-bold text-white">{value}</p>
          </div>
          <div class={`rounded-lg border p-3 ${colorClass}`}>
            <Icon icon={icon} />
          </div>
        </div>
      </div>
    );
  };
}
