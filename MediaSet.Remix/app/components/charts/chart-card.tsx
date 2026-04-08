export default function ChartCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="flex max-h-96 flex-col rounded-lg border border-zinc-700 bg-zinc-800/50 p-4">
      <p className="mb-3 text-sm font-medium text-zinc-400">{title}</p>
      <div className="min-h-0 flex-1">{children}</div>
    </div>
  );
}
