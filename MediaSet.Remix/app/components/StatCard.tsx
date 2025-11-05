import { type LucideIcon } from "lucide-react";

interface StatCardProps {
  title: string;
  value: string | number;
  icon: LucideIcon;
  subtitle?: string;
  colorClass?: string;
}

export default function StatCard({
  title,
  value,
  icon: Icon,
  subtitle,
  colorClass = "bg-blue-500/10 text-blue-400 border-blue-500/20",
}: StatCardProps) {
  return (
    <div className="rounded-lg border border-zinc-700 bg-zinc-800/50 p-6 transition-all hover:border-zinc-600 hover:bg-zinc-800">
      <div className="flex items-center justify-between">
        <div className="flex-1">
          <p className="text-sm font-medium text-zinc-400">{title}</p>
          <p className="mt-2 text-3xl font-bold text-white">{value}</p>
          {subtitle && (
            <p className="mt-1 text-sm text-zinc-500">{subtitle}</p>
          )}
        </div>
        <div
          className={`rounded-lg border p-3 ${colorClass}`}
        >
          <Icon className="h-6 w-6" />
        </div>
      </div>
    </div>
  );
}
