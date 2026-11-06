import type { Handle, RemixNode } from 'remix/component';

interface ChartCardProps {
  title: string;
  children?: RemixNode;
}

export function ChartCard(handle: Handle<ChartCardProps>) {
  return () => (
    <div class="flex max-h-96 flex-col rounded-lg border border-zinc-700 bg-zinc-800/50 p-4">
      <p class="mb-3 text-sm font-medium text-zinc-400">{handle.props.title}</p>
      <div class="min-h-0 flex-1">{handle.props.children}</div>
    </div>
  );
}
