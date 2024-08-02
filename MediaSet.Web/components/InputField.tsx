import { JSX } from "preact";

interface InputFieldProps {
  name: string;
  label: string;
}

export default function InputField({ label, ...props }: JSX.HTMLAttributes<HTMLDivElement> & InputFieldProps) {
  return (
    <div {...props} className="flex gap-4">
      <label for={props.id} className="dark:text-slate-400">{label}</label>
      {props.children
      ? props.children
      : <input id={props.id} name={props.name} />}
    </div>
  );
};