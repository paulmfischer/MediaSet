import { JSX } from 'preact';
import { Anchor } from "./Anchor.tsx";
import { TbBook2 } from '@preact-icons/tb/TbBook2';

export function Navigation(props: JSX.HTMLAttributes<HTMLElement>) {
  return (
    <nav {...props}>
      <ul>
        <li>
          <Anchor href="/books" className="flex items-center">
            <TbBook2 size={24} />
            <span className="ml-2 text-xl">Books</span>
          </Anchor>
        </li>
      </ul>
    </nav>
  );
};
