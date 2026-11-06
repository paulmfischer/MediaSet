import type { Handle } from 'remix/component';

export interface PieSlice {
  name: string;
  value: number;
}

interface PieChartProps {
  data: PieSlice[];
  colors: string[];
  /** Names the chart for assistive technology; the card title. */
  title: string;
  size?: number;
}

// Minimum visible slice: 8 degrees.
const MIN_ANGLE = (8 * Math.PI) / 180;

function polarToCartesian(cx: number, cy: number, r: number, angleRad: number): { x: number; y: number } {
  return { x: cx + r * Math.cos(angleRad), y: cy + r * Math.sin(angleRad) };
}

function slicePath(cx: number, cy: number, r: number, startAngle: number, endAngle: number): string {
  const start = polarToCartesian(cx, cy, r, startAngle);
  const end = polarToCartesian(cx, cy, r, endAngle);
  const largeArc = endAngle - startAngle > Math.PI ? 1 : 0;
  return `M ${cx} ${cy} L ${start.x} ${start.y} A ${r} ${r} 0 ${largeArc} 1 ${end.x} ${end.y} Z`;
}

// Slices below the minimum angle are widened and the others shrink to make room.
export function computeAngles(data: PieSlice[], total: number): number[] {
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

const count = (value: number): string => value.toLocaleString('en-US');

// A server-rendered pie with a legend. Hover detail is each slice's SVG <title>.
export function PieChart(handle: Handle<PieChartProps>) {
  return () => {
    const { data, colors, title, size = 220 } = handle.props;
    const total = data.reduce((sum, d) => sum + d.value, 0);
    if (total === 0) return null;

    const cx = size / 2;
    const r = size / 2 - 4;
    const angles = computeAngles(data, total);
    let angle = -Math.PI / 2;

    const slices = data.map((d, i) => {
      const endAngle = angle + angles[i];
      const slice = {
        path: slicePath(cx, cx, r, angle, endAngle),
        color: colors[i % colors.length],
        name: d.name,
        value: d.value,
        pct: ((d.value / total) * 100).toFixed(1),
      };
      angle = endAngle;
      return slice;
    });

    return (
      <div class="relative flex h-full flex-col gap-3">
        <div class="relative min-h-0 flex-1">
          <svg viewBox={`0 0 ${size} ${size}`} width="100%" height="100%" aria-label={`${title} pie chart`} role="img">
            {slices.map((slice) => (
              <path d={slice.path} fill={slice.color} stroke="#18181b" stroke-width="2">
                <title>{`${slice.name}: ${count(slice.value)} · ${slice.pct}%`}</title>
              </path>
            ))}
          </svg>
        </div>
        <ul class="flex flex-shrink-0 flex-wrap justify-center gap-x-4 gap-y-1">
          {slices.map((slice) => (
            <li class="flex items-center gap-1.5">
              <span class="inline-block h-2 w-2 flex-shrink-0 rounded-full" style={{ backgroundColor: slice.color }} />
              <span class="text-xs text-zinc-200">{slice.name}</span>
              <span class="text-xs text-zinc-400">
                {count(slice.value)} · {slice.pct}%
              </span>
            </li>
          ))}
        </ul>
      </div>
    );
  };
}
