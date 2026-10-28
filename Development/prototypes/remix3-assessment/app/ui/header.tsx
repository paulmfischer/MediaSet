import type { Handle } from 'remix/component';

import { routes } from '../routes.ts';
import { ENTITIES } from '../data.ts';
import { MobileMenu } from './public/mobile-menu.tsx';

export function Header(handle: Handle<{ path: string }>) {
  return () => {
    let { path } = handle.props;
    let link = (href: string, label: string, cls = '') => (
      <a href={href} class={`p-3 rounded-lg ${cls} ${path === href ? 'active' : ''}`}>
        {label}
      </a>
    );
    return (
      <header class="sticky top-0 z-30 dark:bg-zinc-700 p-2 lg:px-8">
        <div class="flex items-center justify-between gap-4">
          <a href={routes.home.href()}>
            <h1 class="text-3xl">MediaSet</h1>
          </a>
          <nav class="hidden md:flex flex-row gap-4 items-center">
            {link(routes.home.href(), 'Home')}
            {ENTITIES.map((e) => link(routes.entities.href({ entity: e }), e, `entity-${e}`))}
            {link(routes.stats.href(), 'Image Statistics')}
            {link(routes.scan.href(), 'Scan')}
          </nav>
          <MobileMenu
            links={[
              ['Home', routes.home.href()],
              ...ENTITIES.map((e) => [e, routes.entities.href({ entity: e })] as [string, string]),
              ['Scan', routes.scan.href()],
            ]}
          />
        </div>
      </header>
    );
  };
}
