import { JSX } from "preact";

interface InputFieldProps {
  name: string;
  label: string;
  inputProps?: JSX.HTMLAttributes<HTMLInputElement>;
}

export default function InputField({ label, inputProps, ...props }: JSX.HTMLAttributes<HTMLDivElement> & InputFieldProps) {
  return (
    <div {...props} className="flex flex-col gap-4">
      <label for={props.id} className="dark:text-slate-400">{label}</label>
      {props.children}
    </div>
  );
};