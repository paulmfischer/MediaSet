import { PropsWithChildren } from "react";

type BadgeProps = {
  color: string;
}

export default function Badge({ children }: PropsWithChildren) {
  return (
    <span className="inline-flex items-center rounded-md bg-zinc-700 px-2 py-1 text-xs font-medium text-slate-300 ring-1 ring-inset ring-slate-400">
      {children}
    </span>
  )
}