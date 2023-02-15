interface HeaderProps {
  active: string;
}

export function Header({ active }: HeaderProps) {
  const menus = [
    { name: "Home", href: "/", matchingRoutes: ['/'] },
    { name: "Books", href: "/books", matchingRoutes: ['/books', '/books/:id'] },
  ];

  const activeTab = (path: string[]) => path.includes(active);

  return (
    <div class="bg-white w-full py-6 px-8 flex flex-col gap-4">
      <div class="flex items-center flex-1">
        <div class="text-2xl  ml-1 font-bold">
          MediaSet
        </div>
      </div>
      <ul class="flex border-b">
        {menus.map((menu) => (
          <li class={`${activeTab(menu.matchingRoutes) ? '-mb-px' : ''} mr-1`}>
            <a
              href={menu.href}
              class={"bg-white inline-block text-gray-500 hover:text-gray-700 py-2 px-4 font-semibold" +
                (activeTab(menu.matchingRoutes) ? " border-l border-t border-r rounded-t" : "")}
            >
              {menu.name}
            </a>
          </li>
        ))}
      </ul>
    </div>
  );
}