import { JSX } from "preact";

export default function Input(props: JSX.HTMLAttributes<HTMLInputElement | HTMLTextAreaElement>) {
  if (props.type == 'textarea') {
    return <textarea {...props as JSX.HTMLAttributes<HTMLTextAreaElement>} className="p-1 rounded dark:text-slate-800" />
  }

  return (
    <input {...props as JSX.HTMLAttributes<HTMLInputElement>} className="p-1 rounded dark:text-slate-800" />
  );
};