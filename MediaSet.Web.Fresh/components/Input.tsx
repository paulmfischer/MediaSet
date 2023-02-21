import { JSX } from "preact";

export function Input(props: JSX.HTMLAttributes<HTMLInputElement>) {
  return (
    <input
      {...props}
      class={`
        pl-1 rounded border(gray-500 1)
        ${props.class || ''}
      `}
    />
  )
}