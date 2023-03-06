import { JSX } from 'preact';
import { Anchor } from './Anchor.tsx';

interface NavigationProps {
  activeRoute: string;
}

const menus = [
  { name: 'Home', href: '/', matchingRoutes: ['/'] },
  { name: 'Books', href: '/books', matchingRoutes: ['/books', '/books/:id', '/books/add'] },
];

export function Navigation({ activeRoute, ...navProps }: NavigationProps & JSX.HTMLAttributes<HTMLElement>) {
const activeTab = (path: string[]) => path.includes(activeRoute);

  // class='flex flex-col space-y-4 xl:hidden'
  return (
    <nav {...navProps}>
      <ul class='flex flex-col space-y-3 xl:space-y-0 xl:flex-row xl:space-x-8 xl:items-center'>
        {menus.map((menu) => (
          // class= {`${activeTab(menu.matchingRoutes) ? '-mb-px' : ''} mr-1`}
          <li>
            <Anchor
              href={menu.href}
              className={`
                  no-underline font-semibold
                  ${(activeTab(menu.matchingRoutes) ? ' text-indigo-600 bg-white' : '')}
                `}
            >
              {menu.name}
            </Anchor>
          </li>
        ))}
      </ul>
    </nav>
  )
}