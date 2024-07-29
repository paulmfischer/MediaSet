import { JSX } from 'preact';
interface MediaHeaderProps {
    title: string;
}

export function MediaHeader({ title, ...props }: MediaHeaderProps | JSX.HTMLAttributes<HTMLDivElement>) {
    return (
        <div {...props}>
            <h2 className="mt-4 mb-3 text-6xl lg:text-3xl dark:text-slate-400">{title}</h2>
        </div>
    );
};
