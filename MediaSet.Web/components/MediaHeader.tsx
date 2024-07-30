import { JSX } from 'preact';
interface MediaHeaderProps {
  title: string;
}

export function MediaHeader({ title, ...props }: MediaHeaderProps | JSX.HTMLAttributes<HTMLDivElement>) {
  return (
    <h2 {...props} className="text-6xl lg:text-3xl dark:text-slate-400">{title}</h2>
  );
};
