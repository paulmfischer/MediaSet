import { JSX } from "preact";

export function Anchor(props: JSX.HTMLAttributes<HTMLAnchorElement>) {
  return (
    <a
      {...props}
      class="underline hover:text-gray-400"
    >
      {props.children}
    </a>
  );
}