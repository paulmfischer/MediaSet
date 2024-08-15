import { JSX } from "preact";

export function Anchor({ children, className, ...props }: JSX.HTMLAttributes<HTMLAnchorElement>) {
  return (
    <a {...props} className={`text-blue-700 underline ${className}`}>
      {children}
    </a>
  );
}