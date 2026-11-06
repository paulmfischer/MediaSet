import type { Handle } from 'remix/component';

import { Icon } from './icon.tsx';
import { isActivePath, mainNav, toolsNav, type NavItem } from './nav.ts';
import { MobileMenu } from './public/mobile-menu.tsx';
import { ToolsMenu } from './public/tools-menu.tsx';

interface NavLinkProps {
  item: NavItem;
  active: boolean;
  class: string;
}

// Active state is decided on the server from the request path, so it needs no client code.
function NavLink(handle: Handle<NavLinkProps>) {
  return () => {
    const { item, active, class: className } = handle.props;
    const classes = [item.accent ? `entity-${item.accent}` : '', className, active ? 'active' : '']
      .filter(Boolean)
      .join(' ');

    return (
      <a href={item.href} class={classes} aria-current={active ? 'page' : undefined}>
        <Icon icon={item.icon} class={item.accent ? 'text-entity' : undefined} />
        {item.label}
      </a>
    );
  };
}

export function Header(handle: Handle<{ path: string }>) {
  return () => {
    const { path } = handle.props;
    const links = (className: string, items: NavItem[]) =>
      items.map((item) => (
        <NavLink key={item.href} item={item} active={isActivePath(path, item.href)} class={className} />
      ));

    return (
      <header class="sticky top-0 z-30 bg-zinc-200 dark:bg-zinc-700 p-2 lg:px-8">
        <div class="flex flex-wrap items-center justify-between gap-4">
          <a href="/">
            <span class="text-3xl">MediaSet</span>
          </a>

          <nav aria-label="Main" class="hidden md:flex flex-row gap-4 items-center">
            {links('p-3 flex gap-2 items-center rounded-lg', mainNav)}
            <ToolsMenu active={toolsNav.some((item) => isActivePath(path, item.href))}>
              {toolsNav.map((item) => (
                <NavLink
                  key={item.href}
                  item={item}
                  active={isActivePath(path, item.href)}
                  class="flex gap-2 items-center px-4 py-3 rounded-lg hover:dark:bg-zinc-700"
                />
              ))}
            </ToolsMenu>
          </nav>

          <MobileMenu>
            {links('p-3 rounded-lg flex gap-2 items-center', mainNav)}
            <hr class="border-zinc-600 mx-3" />
            {links('p-3 rounded-lg flex gap-2 items-center', toolsNav)}
          </MobileMenu>
        </div>
      </header>
    );
  };
}
