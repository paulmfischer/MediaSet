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
};

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

export default function PieChart({ data, colors, size = 220, onSliceClick, activeSlice }: PieChartProps) {
  const total = data.reduce((sum, d) => sum + d.value, 0);
  if (total === 0) return null;

  const cx = size / 2;
  const cy = size / 2;
  const r = size / 2 - 4;

  const slices: { path: string; color: string; name: string; value: number; pct: string }[] = [];
  let angle = -Math.PI / 2;

  for (let i = 0; i < data.length; i++) {
    const slice = data[i];
    const sweep = (slice.value / total) * 2 * Math.PI;
    const endAngle = angle + sweep;
    slices.push({
      path: slicePath(cx, cy, r, angle, endAngle),
      color: colors[i % colors.length],
      name: slice.name,
      value: slice.value,
      pct: ((slice.value / total) * 100).toFixed(1),
    });
    angle = endAngle;
  }

  return (
    <div>
      <svg
        viewBox={`0 0 ${size} ${size}`}
        width={size}
        height={size}
        className="mx-auto block"
        aria-label="Pie chart"
        role="img"
      >
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
            >
              <title>
                {slice.name}: {slice.value.toLocaleString()} ({slice.pct}%)
              </title>
            </path>
          );
        })}
      </svg>
      <ul className="mt-3 flex flex-wrap justify-center gap-x-4 gap-y-1">
        {slices.map((slice) => (
          <li
            key={slice.name}
            className={`flex items-center gap-1.5 text-xs transition-opacity duration-200 ${activeSlice && activeSlice !== slice.name ? 'opacity-40' : 'opacity-100'}`}
          >
            <span
              className="inline-block h-2.5 w-2.5 rounded-full flex-shrink-0"
              style={{ backgroundColor: slice.color }}
            />
            <span className="text-zinc-300">{slice.name}</span>
            <span className="text-zinc-500">({slice.pct}%)</span>
          </li>
        ))}
      </ul>
    </div>
  );
}
