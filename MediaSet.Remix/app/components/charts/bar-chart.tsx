import { useRef, useState } from 'react';

type BarChartItem = {
  name: string;
  value: number;
};

type BarChartProps = {
  data: BarChartItem[];
  color: string;
  orientation?: 'horizontal' | 'vertical';
  maxItems?: number;
};

type TooltipState = { x: number; y: number; name: string; value: number } | null;

export default function BarChart({ data, color, orientation = 'horizontal', maxItems = 10 }: BarChartProps) {
  const items = data.slice(0, maxItems);
  const [tooltip, setTooltip] = useState<TooltipState>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  if (items.length === 0) return null;

  const max = Math.max(...items.map((d) => d.value));

  const handleMouseMove = (e: React.MouseEvent, name: string, value: number) => {
    if (!containerRef.current) return;
    const rect = containerRef.current.getBoundingClientRect();
    setTooltip({ x: e.clientX - rect.left, y: e.clientY - rect.top, name, value });
  };

  const handleMouseLeave = () => setTooltip(null);

  if (orientation === 'vertical') {
    return (
      <div ref={containerRef} className="relative flex flex-col gap-2" onMouseLeave={handleMouseLeave}>
        {tooltip && (
          <div
            className="pointer-events-none absolute z-20 whitespace-nowrap"
            style={{ left: tooltip.x + 14, top: Math.max(4, tooltip.y - 40) }}
          >
            <div className="rounded-md border border-zinc-700 bg-zinc-900 px-2.5 py-1.5 shadow-xl">
              <p className="text-xs font-semibold text-zinc-200">{tooltip.name}</p>
              <div className="mt-1 flex items-center gap-1.5">
                <span className="inline-block h-2 w-2 flex-shrink-0 rounded-sm" style={{ backgroundColor: color }} />
                <span className="text-xs text-zinc-300">{tooltip.value.toLocaleString()}</span>
              </div>
            </div>
          </div>
        )}
        <div className="relative flex items-end" style={{ aspectRatio: '3 / 2' }}>
          {items.map((item) => {
            const heightPct = max > 0 ? Math.max((item.value / max) * 100, 1) : 1;
            return (
              <div
                key={item.name}
                className="relative flex-1 self-stretch px-1.5"
                onMouseMove={(e) => handleMouseMove(e, item.name, item.value)}
              >
                <div
                  className="absolute inset-x-1.5 bottom-0 rounded-t transition-opacity hover:opacity-80"
                  style={{ height: `${heightPct}%`, backgroundColor: color }}
                />
              </div>
            );
          })}
        </div>
        <div className="flex">
          {items.map((item) => (
            <span
              key={item.name}
              className="flex-1 truncate px-1.5 text-center text-xs text-zinc-400"
              title={item.name}
            >
              {item.name}
            </span>
          ))}
        </div>
      </div>
    );
  }

  return (
    <div ref={containerRef} className="relative" onMouseLeave={handleMouseLeave}>
      {tooltip && (
        <div
          className="pointer-events-none absolute z-20 whitespace-nowrap"
          style={{ left: tooltip.x + 14, top: Math.max(4, tooltip.y - 40) }}
        >
          <div className="rounded-md border border-zinc-700 bg-zinc-900 px-2.5 py-1.5 shadow-xl">
            <p className="text-xs font-semibold text-zinc-200">{tooltip.name}</p>
            <div className="mt-1 flex items-center gap-1.5">
              <span className="inline-block h-2 w-2 flex-shrink-0 rounded-sm" style={{ backgroundColor: color }} />
              <span className="text-xs text-zinc-300">{tooltip.value.toLocaleString()}</span>
            </div>
          </div>
        </div>
      )}
      <ul className="space-y-2">
        {items.map((item, i) => {
          const widthPct = max > 0 ? (item.value / max) * 100 : 0;
          return (
            <li
              key={item.name}
              className="flex items-center gap-2"
              onMouseMove={(e) => handleMouseMove(e, item.name, item.value)}
            >
              <span className="w-4 flex-shrink-0 text-right text-xs text-zinc-500">{i + 1}</span>
              <div className="flex flex-1 flex-col gap-0.5">
                <div className="flex items-center justify-between">
                  <span className="truncate text-xs font-medium text-zinc-200" title={item.name}>
                    {item.name}
                  </span>
                  <span className="ml-2 flex-shrink-0 text-xs text-zinc-400">{item.value}</span>
                </div>
                <div className="h-1.5 w-full overflow-hidden rounded-full bg-zinc-700">
                  <div
                    className="h-full rounded-full transition-all"
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
}
