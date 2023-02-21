import { JSX } from "preact";

const colorTheme = "text-white bg-indigo-600 border(indigo-600 1) hover:bg-indigo-700";

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
        ${props.removeColored ?  '' : colorTheme}
      `}
    >
      {props.children}
    </a>
  );
}