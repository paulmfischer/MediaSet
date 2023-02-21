import { Anchor } from "./Anchor.tsx";

interface HeaderProps {
  active: string;
}

export function Header({ active }: HeaderProps) {
  const menus = [
    { name: "Home", href: "/", matchingRoutes: ['/'] },
    { name: "Books", href: "/books", matchingRoutes: ['/books', '/books/:id', '/books/add'] },
  ];
  
  const activeTab = (path: string[]) => path.includes(active);

  return (
    <div class="bg-white w-full pt-6 pb-2 px-8 flex flex-col gap-4">
      <div class="flex items-center flex-1">
        <div class="text-2xl  ml-1 font-bold">
          MediaSet
        </div>
      </div>
      <nav>
        <ul class="flex border(indigo-600 b)">
          {menus.map((menu) => (
            <li class={`${activeTab(menu.matchingRoutes) ? '-mb-px' : ''} mr-1`}>
              <Anchor
                href={menu.href}
                className={`
                  no-underline inline-block py-2 px-4 font-semibold rounded-t
                  ${(activeTab(menu.matchingRoutes) ? " border-l border-t border-r" : "")}
                `}
                removeColored={!activeTab(menu.matchingRoutes)}
              >
                {menu.name}
              </Anchor>
            </li>
          ))}
        </ul>
      </nav>
    </div>
  );
}