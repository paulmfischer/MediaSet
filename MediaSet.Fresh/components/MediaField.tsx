import { JSX } from "preact";

interface MediaFieldProps {
  label: string;
  fieldContent?: string | number | null;
}

export default function MediaField({ label, fieldContent, ...props }: JSX.HTMLAttributes<HTMLDivElement> & MediaFieldProps) {
  return (
    <div {...props} className="flex gap-4">
      <label for={props.id} className="dark:text-slate-400">{label}</label>
      <div id={props.id}>{fieldContent}</div>
    </div>
  );
};