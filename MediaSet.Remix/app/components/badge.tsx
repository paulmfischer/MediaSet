import { PropsWithChildren } from "react";

type BadgeProps = {
  color: string;
}

export default function Badge({ children }: PropsWithChildren) {
  return (
    <span className="inline-flex items-center rounded-md bg-blue-800 px-2 py-1 text-xs font-medium text-blue-100 ring-1 ring-inset ring-blue-600">
      {children}
    </span>
  )
}