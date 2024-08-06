import { JSX } from "preact";

interface InputFieldProps {
  label: string;
}

export default function InputField({ label, ...props }: JSX.HTMLAttributes<HTMLDivElement> & InputFieldProps) {
  return (
    <div {...props} className="flex flex-col gap-4">
      <label for={props.id} className="dark:text-slate-400">{label}</label>
      {props.children}
    </div>
  );
};