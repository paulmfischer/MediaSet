import { JSX } from 'preact';
import { Anchor } from "./Anchor.tsx";
// import BookIcon from 'https://icons.church/fa6-solid/book';

export function Navigation(props: JSX.HTMLAttributes<HTMLElement>) {
  return (
    <nav {...props}>
      <ul>
        <li>
          <Anchor href="/books" className="flex">
            {/* <BookIcon  className="w-6 h-6 mr-4" /> */}
            <span className="text-xl text-blue-600 dark:text-blue-400">Books</span>
          </Anchor>
        </li>
      </ul>
    </nav>
  );
};
