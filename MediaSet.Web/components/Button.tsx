import { JSX } from "preact";

export function Button(props: JSX.HTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      {...props}
      class="px-2 py-1 dark:text-slate-400 dark:bg-slate-800 dark:hover:bg-slate-600 dark:border-slate-600 border-gray-500 border-2 rounded bg-white hover:bg-gray-200 transition-colors"
    />
  );
}
