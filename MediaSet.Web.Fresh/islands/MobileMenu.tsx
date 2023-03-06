import { Button } from "../components/Button.tsx";
import IconMenu2 from 'tabler-icons/menu-2.tsx';

interface MobileMenuProps {
  mobileNavId: string;
}

export default function MobileMenu({ mobileNavId }: MobileMenuProps) {
  const clickHandler = () => {
    const mobileNav = document.getElementById(mobileNavId);
    if (mobileNav?.classList.contains('hidden')) {
      mobileNav?.classList.remove('hidden');
    } else {
      mobileNav?.classList.add('hidden');
    }
  };

  return (
    <Button onClick={clickHandler}>
      <IconMenu2 class='w-6 h-6' />
    </Button>
  )
}