import type { Handle } from 'remix/component';

export interface BarChartItem {
  name: string;
  value: number;
}

interface BarChartProps {
  data: BarChartItem[];
  color: string;
  orientation?: 'horizontal' | 'vertical';
  maxItems?: number;
}

const count = (value: number): string => value.toLocaleString('en-US');

// Server-rendered bars with no script. Hover detail is each bar's title attribute.
export function BarChart(handle: Handle<BarChartProps>) {
  return () => {
    const { data, color, orientation = 'horizontal', maxItems = 10 } = handle.props;
    const items = data.slice(0, maxItems);
    if (items.length === 0) return null;

    const max = Math.max(...items.map((d) => d.value));

    if (orientation === 'vertical') {
      return (
        <div class="relative flex h-full flex-col gap-2">
          <div class="relative min-h-0 flex-1 flex items-end">
            {items.map((item) => {
              const heightPct = max > 0 ? Math.max((item.value / max) * 100, 1) : 1;
              return (
                <div class="relative flex-1 self-stretch px-1.5" title={`${item.name}: ${count(item.value)}`}>
                  <div
                    class="absolute inset-x-1.5 bottom-0 rounded-t transition-opacity hover:opacity-80"
                    style={{ height: `${heightPct}%`, backgroundColor: color }}
                  />
                </div>
              );
            })}
          </div>
          <div class="flex flex-shrink-0">
            {items.map((item) => (
              <span class="flex-1 truncate px-1.5 text-center text-xs text-zinc-400" title={item.name}>
                {item.name}
                <span class="sr-only">: {count(item.value)}</span>
              </span>
            ))}
          </div>
        </div>
      );
    }

    return (
      <div class="relative">
        <ul class="space-y-2">
          {items.map((item, i) => {
            const widthPct = max > 0 ? (item.value / max) * 100 : 0;
            return (
              <li class="flex items-center gap-2" title={`${item.name}: ${count(item.value)}`}>
                <span class="w-4 flex-shrink-0 text-right text-xs text-zinc-500">{i + 1}</span>
                <div class="flex flex-1 flex-col gap-0.5">
                  <div class="flex items-center justify-between">
                    <span class="truncate text-xs font-medium text-zinc-200">{item.name}</span>
                    <span class="ml-2 flex-shrink-0 text-xs text-zinc-400">{item.value}</span>
                  </div>
                  <div class="h-1.5 w-full overflow-hidden rounded-full bg-zinc-700">
                    <div
                      class="h-full rounded-full transition-all"
                      style={{ width: `${widthPct}%`, backgroundColor: color }}
                    />
                  </div>
                </div>
              </li>
            );
          })}
        </ul>
      </div>
    );
  };
}
