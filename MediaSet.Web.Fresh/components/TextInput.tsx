import { JSX } from 'preact';
import { Input } from './Input.tsx'

type FormInputProps = {
  inputLabel: string | JSX.Element,
  name: string,
};

export function TextInput({ inputLabel, name, ...labelProps }: FormInputProps) {
  const textLabel = typeof inputLabel === 'string'
    ? <label {...labelProps}>{inputLabel}</label>
    : inputLabel;
  return (
    <>
      {textLabel}
      <Input type="text" name={name} />
    </>
  );
}