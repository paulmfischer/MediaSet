import { JSX } from "preact";

export function IconAnchor(props: JSX.HTMLAttributes<HTMLAnchorElement>) {
  return (
    <a
      {...props}
      class={`
        ${props.class || ''}
        rounded flex items-center gap-2 px-1
        text-white bg-indigo-600 border(indigo-600 1) hover:bg-indigo-700
      `}
    >
      {props.children}
    </a>
  );
}