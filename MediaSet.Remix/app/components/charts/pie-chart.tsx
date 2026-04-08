import { useRef, useState } from 'react';
import ChartTooltip from './chart-tooltip';

type PieSlice = {
  name: string;
  value: number;
};

type PieChartProps = {
  data: PieSlice[];
  colors: string[];
  size?: number;
  onSliceClick?: (name: string) => void;
  activeSlice?: string;
  compact?: boolean;
};

type TooltipState = { x: number; y: number; name: string; value: number; pct: string; color: string } | null;

// Minimum visible/clickable slice — 8 degrees
const MIN_ANGLE = (8 * Math.PI) / 180;

function polarToCartesian(cx: number, cy: number, r: number, angleRad: number) {
  return {
    x: cx + r * Math.cos(angleRad),
    y: cy + r * Math.sin(angleRad),
  };
}

function slicePath(cx: number, cy: number, r: number, startAngle: number, endAngle: number): string {
  const start = polarToCartesian(cx, cy, r, startAngle);
  const end = polarToCartesian(cx, cy, r, endAngle);
  const largeArc = endAngle - startAngle > Math.PI ? 1 : 0;
  return `M ${cx} ${cy} L ${start.x} ${start.y} A ${r} ${r} 0 ${largeArc} 1 ${end.x} ${end.y} Z`;
}

function computeAngles(data: PieSlice[], total: number): number[] {
  const natural = data.map((d) => (d.value / total) * 2 * Math.PI);
  const needsMin = natural.map((a) => a < MIN_ANGLE);
  const smallCount = needsMin.filter(Boolean).length;
  const remainingAngle = 2 * Math.PI - smallCount * MIN_ANGLE;
  const largeTotal = data.reduce((sum, d, i) => (needsMin[i] ? sum : sum + d.value), 0);

  return natural.map((a, i) => {
    if (needsMin[i]) return MIN_ANGLE;
    return largeTotal > 0 ? (data[i].value / largeTotal) * remainingAngle : a;
  });
}

export default function PieChart({
  data,
  colors,
  size = 220,
  onSliceClick,
  activeSlice,
  compact = false,
}: PieChartProps) {
  const [tooltip, setTooltip] = useState<TooltipState>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  const total = data.reduce((sum, d) => sum + d.value, 0);
  if (total === 0) return null;

  const cx = size / 2;
  const cy = size / 2;
  const r = size / 2 - 4;

  const angles = computeAngles(data, total);
  const slices: {
    path: string;
    color: string;
    name: string;
    value: number;
    pct: string;
  }[] = [];
  let angle = -Math.PI / 2;

  for (let i = 0; i < data.length; i++) {
    const endAngle = angle + angles[i];
    slices.push({
      path: slicePath(cx, cy, r, angle, endAngle),
      color: colors[i % colors.length],
      name: data[i].name,
      value: data[i].value,
      pct: ((data[i].value / total) * 100).toFixed(1),
    });
    angle = endAngle;
  }

  const handleMouseMove = (e: React.MouseEvent, slice: (typeof slices)[number]) => {
    if (!containerRef.current) return;
    const rect = containerRef.current.getBoundingClientRect();
    setTooltip({
      x: e.clientX - rect.left,
      y: e.clientY - rect.top,
      name: slice.name,
      value: slice.value,
      pct: slice.pct,
      color: slice.color,
    });
  };

  const handleMouseLeave = () => setTooltip(null);

  const svgEl = (
    <div className="relative min-h-0 flex-1">
      <svg viewBox={`0 0 ${size} ${size}`} width="100%" height="100%" aria-label="Pie chart" role="img">
        {slices.map((slice) => {
          const isActive = activeSlice === undefined || activeSlice === slice.name;
          return (
            <path
              key={slice.name}
              d={slice.path}
              fill={slice.color}
              stroke="#18181b"
              strokeWidth={2}
              opacity={isActive ? 1 : 0.35}
              className={
                onSliceClick
                  ? 'cursor-pointer transition-opacity duration-200 hover:opacity-90'
                  : 'transition-opacity duration-200'
              }
              onClick={onSliceClick ? () => onSliceClick(slice.name) : undefined}
              onMouseMove={(e) => handleMouseMove(e, slice)}
              onMouseLeave={handleMouseLeave}
            />
          );
        })}
      </svg>
    </div>
  );

  const tooltipEl = tooltip && (
    <ChartTooltip
      x={tooltip.x}
      y={tooltip.y}
      name={tooltip.name}
      color={tooltip.color}
      label={`${tooltip.value.toLocaleString()} · ${tooltip.pct}%`}
    />
  );

  if (compact) {
    return (
      <div ref={containerRef} className="relative flex h-full flex-col gap-3" onMouseLeave={handleMouseLeave}>
        {tooltipEl}
        {svgEl}
        <ul className="flex flex-shrink-0 flex-wrap justify-center gap-x-4 gap-y-1">
          {slices.map((slice) => {
            const isActive = activeSlice === undefined || activeSlice === slice.name;
            return (
              <li
                key={slice.name}
                className={`flex items-center gap-1.5 transition-opacity duration-200 ${isActive ? 'opacity-100' : 'opacity-40'}`}
                onMouseMove={(e) => handleMouseMove(e, slice)}
              >
                <span
                  className="inline-block h-2 w-2 flex-shrink-0 rounded-full"
                  style={{ backgroundColor: slice.color }}
                />
                <span className="text-xs text-zinc-200">{slice.name}</span>
                <span className="text-xs text-zinc-400">
                  {slice.value.toLocaleString()} · {slice.pct}%
                </span>
              </li>
            );
          })}
        </ul>
      </div>
    );
  }

  return (
    <div ref={containerRef} className="relative flex h-full items-stretch gap-4" onMouseLeave={handleMouseLeave}>
      {tooltipEl}
      <ul className="flex flex-shrink-0 flex-col justify-center gap-2">
        {slices.map((slice) => {
          const isActive = activeSlice === undefined || activeSlice === slice.name;
          return (
            <li
              key={slice.name}
              className={`transition-opacity duration-200 ${isActive ? 'opacity-100' : 'opacity-40'}`}
              onMouseMove={(e) => handleMouseMove(e, slice)}
            >
              {onSliceClick ? (
                <button
                  type="button"
                  className="tertiary flex w-full cursor-pointer items-center gap-2 text-left"
                  onClick={() => onSliceClick(slice.name)}
                >
                  <span
                    className="inline-block h-2.5 w-2.5 flex-shrink-0 rounded-full"
                    style={{ backgroundColor: slice.color }}
                  />
                  <span className="flex flex-col">
                    <span className="text-xs font-medium text-zinc-200">{slice.name}</span>
                    <span className="text-xs text-zinc-400">
                      {slice.value.toLocaleString()} · {slice.pct}%
                    </span>
                  </span>
                </button>
              ) : (
                <span className="flex items-center gap-2">
                  <span
                    className="inline-block h-2.5 w-2.5 flex-shrink-0 rounded-full"
                    style={{ backgroundColor: slice.color }}
                  />
                  <span className="flex flex-col">
                    <span className="text-xs font-medium text-zinc-200">{slice.name}</span>
                    <span className="text-xs text-zinc-400">
                      {slice.value.toLocaleString()} · {slice.pct}%
                    </span>
                  </span>
                </span>
              )}
            </li>
          );
        })}
      </ul>
      {svgEl}
    </div>
  );
}
