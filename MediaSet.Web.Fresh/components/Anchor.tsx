import { JSX } from 'preact';

const colorTheme = 'hover:text-indigo-800';

interface AnchorProps {
  removeColored?: boolean;
}

export function Anchor(props: JSX.HTMLAttributes<HTMLAnchorElement> & AnchorProps) {
  return (
    <a
      {...props}
      class={`
        ${props.class || ''}
        underline
        ${colorTheme}
      `}
    >
      {props.children}
    </a>
  );
}
