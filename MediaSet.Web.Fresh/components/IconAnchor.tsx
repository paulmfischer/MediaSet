import { JSX } from "preact";

export function IconAnchor(props: JSX.HTMLAttributes<HTMLAnchorElement>) {
  return (
    <a
      {...props}
      class="w-8 h-8 border-solid rouded-md border-gray-300 flex justify-center items-center"
    >
      {props.children}
    </a>
  );
}