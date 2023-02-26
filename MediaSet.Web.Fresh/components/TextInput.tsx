import { JSX } from 'preact';
import { Input } from './Input.tsx';

type HTMLInputTypeAttribute =
  | 'button'
  | 'checkbox'
  | 'color'
  | 'date'
  | 'datetime-loca'
  | 'email'
  | 'file'
  | 'hidden'
  | 'image'
  | 'month'
  | 'number'
  | 'password'
  | 'radio'
  | 'range'
  | 'search'
  | 'submit'
  | 'tel'
  | 'text'
  | 'time'
  | 'url'
  | 'week';

type FormInputProps = {
  inputLabel: string | JSX.Element;
  name: string;
  type?: HTMLInputTypeAttribute;
  value?: string | number | string[] | undefined;
  error?: string | string[];
  required?: boolean;
};

export function FormInput(
  { inputLabel, error, type = 'text', required, ...inputProps }: FormInputProps & JSX.HTMLAttributes<HTMLInputElement>,
) {
  const textLabel = typeof inputLabel === 'string' ? <label>{inputLabel} {required && '*'}</label> : inputLabel;
  return (
    <>
      {textLabel}
      <Input {...inputProps} required={required} type={type} class={error ? 'border-red-500' : ''} />
      {error && <div class='mt-1 text-red-600'>{error}</div>}
    </>
  );
}
