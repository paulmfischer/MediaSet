import { JSX } from "preact";

export function Button(props: JSX.HTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      type="button"
      {...props}
      class={`
        ${props.class || ''}
        px-1 rounded flex gap-2
        text-white bg-indigo-600 border(indigo-600 1) hover:bg-indigo-700 
      `}
    />
  );
}
