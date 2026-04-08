type ChartTooltipProps = {
  x: number;
  y: number;
  name: string;
  color: string;
  label: string;
};

export default function ChartTooltip({ x, y, name, color, label }: ChartTooltipProps) {
  return (
    <div
      className="pointer-events-none absolute z-20 whitespace-nowrap"
      style={{ left: x + 14, top: Math.max(4, y - 40) }}
    >
      <div className="rounded-md border border-zinc-700 bg-zinc-900 px-2.5 py-1.5 shadow-xl">
        <p className="text-xs font-semibold text-zinc-200">{name}</p>
        <div className="mt-1 flex items-center gap-1.5">
          <span className="inline-block h-2 w-2 flex-shrink-0 rounded-sm" style={{ backgroundColor: color }} />
          <span className="text-xs text-zinc-300">{label}</span>
        </div>
      </div>
    </div>
  );
}
