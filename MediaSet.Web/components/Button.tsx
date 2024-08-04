import { JSX } from "preact";

interface ButtonProps {
  style?: 'link';
}

export function Button({ style, ...props }: JSX.HTMLAttributes<HTMLButtonElement>) {
  if (style == 'link') {
    return (
      <button {...props}
        className="text-blue-600 dark:text-blue-400 border-none underline p-0"
      />
    );
  }

  return (
    <button
      {...props}
      class="px-2 py-1 dark:text-slate-400 dark:bg-slate-800 dark:hover:bg-slate-600 dark:border-slate-600 border-gray-500 border-2 rounded bg-white hover:bg-gray-200 transition-colors"
    />
  );
}
