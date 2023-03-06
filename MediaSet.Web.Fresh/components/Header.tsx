import { Anchor } from './Anchor.tsx';
import IconMenu2 from 'tabler-icons/menu-2.tsx';
import { Navigation } from './Navigation.tsx';
import MobileMenu from '../islands/MobileMenu.tsx';

interface HeaderProps {
  activeRoute: string;
}

export function Header({ activeRoute }: HeaderProps) {
  // xl:fixed xl:top-0 xl:left-0
  return (
    <header class='border-b backdrop-blur bg-white/90 dark:bg-gray-900/70 dark:border-gray-700  xl:w-full xl:z-30'>
      <div class='container px-3 py-3 mx-auto space-y-4 xl:space-y-0 xl:flex xl:items-center xl:justify-between xl:space-x-10'>
        <div class='flex justify-between'>
          <a href='#' class='flex'>
            <span class='self-center text-lg font-semibold whitespace-nowrap'>MediaSite</span>
          </a>
          <div class='flex items-center space-x-2 xl:hidden'>
            <MobileMenu mobileNavId='mobile-nav'/>
          </div>
        </div>
        <Navigation id='mobile-nav' activeRoute={activeRoute} class='flex flex-col space-y-4 hidden' />
        <Navigation activeRoute={activeRoute} class='hidden xl:flex xl:flex-row xl:items-center xl:justify-between xl:flex-1 xl:space-x-2' />
      </div>
    </header>
  );
}
