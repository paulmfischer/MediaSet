import type { Handle } from 'remix/component';

interface BarChartProps {
  data: { label: string; value: number }[];
}

// Server-rendered SVG: no client JS required for charts.
export function BarChart(handle: Handle<BarChartProps>) {
  return () => {
    let { data } = handle.props;
    let max = Math.max(1, ...data.map((d) => d.value));
    return (
      <svg role="img" aria-label="Bar chart" viewBox={`0 0 ${data.length * 60} 120`} class="w-full max-w-md">
        {data.map((d, i) => {
          let h = (d.value / max) * 90;
          return (
            <g key={d.label}>
              <rect x={i * 60 + 10} y={100 - h} width="40" height={h} class="fill-blue-500" />
              <text x={i * 60 + 30} y="115" text-anchor="middle" class="fill-slate-300" font-size="10">
                {d.label}
              </text>
            </g>
          );
        })}
      </svg>
    );
  };
}
