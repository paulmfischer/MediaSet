import { JSX } from "preact";

export function IconAnchor(props: JSX.HTMLAttributes<HTMLAnchorElement>) {
  return (
    <a
      {...props}
      class="w-16 h-16 border-2 border-solid rounded-md border-gray-300 flex justify-center items-center hover:bg-gray-300"
    >
      {props.children}
    </a>
  );
}