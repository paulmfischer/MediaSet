import { JSX } from "preact";

export function IconAnchor(props: JSX.HTMLAttributes<HTMLAnchorElement>) {
  return (
    <a
      {...props}
      class="bg-white text-gray-500 hover:text-gray-700 rounded border(gray-400 1) hover:bg-gray-200 flex items-center gap-2"
    >
      {props.children}
    </a>
  );
}