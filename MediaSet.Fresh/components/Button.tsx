import { JSX } from "preact";

interface ButtonProps {
  displayStyle?: 'link';
}

export function Button({ displayStyle, className, ...props }: JSX.HTMLAttributes<HTMLButtonElement> & ButtonProps) {
  if (displayStyle == 'link') {
    return (
      <button {...props}
        className={`text-blue-700 border-none underline p-0 ${className}`}
      />
    );
  }

  return (
    <button
      {...props}
      class={`px-4 py-2 dark:text-slate-400 dark:bg-blue-900 dark:hover:bg-blue-800 rounded bg-white transition-colors ${className}`}
    />
  );
}
