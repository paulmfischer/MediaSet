import { JSX } from "preact";
// import { IS_BROWSER } from "$fresh/runtime.ts";

export function Navigation(props: JSX.HTMLAttributes<HTMLElement>) {
  return (
    <nav {...props}>
      <ul>
        <li>
          <a href="/books">Books</a>
        </li>
      </ul>
    </nav>
  );
}
