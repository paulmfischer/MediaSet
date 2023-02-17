import { JSX } from "preact";

export function Anchor(props: JSX.HTMLAttributes<HTMLAnchorElement>) {
  return (
    <a
      {...props}
      class="underline bg-white text-gray-500 hover:text-gray-700 hover:bg-gray-200"
    >
      {props.children}
    </a>
  );
}