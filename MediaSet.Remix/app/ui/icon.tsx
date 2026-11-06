import type { IconNode } from 'lucide';
import type { Handle } from 'remix/component';

interface IconProps {
  /** An icon node from `./icons.ts`. */
  icon: IconNode;
  /** Width and height in pixels. Defaults to 24, like the icon library. */
  size?: number;
  class?: string;
}

// Renders lucide icon data as inline SVG that draws with the current text colour.
export function Icon(handle: Handle<IconProps>) {
  return () => {
    const { icon, size = 24, class: className } = handle.props;

    return (
      <svg
        xmlns="http://www.w3.org/2000/svg"
        width={size}
        height={size}
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="2"
        stroke-linecap="round"
        stroke-linejoin="round"
        class={className ? `lucide ${className}` : 'lucide'}
        aria-hidden="true"
      >
        {icon.map(([tag, attributes], index) => {
          const Shape = tag as 'path';
          return <Shape key={index} {...attributes} />;
        })}
      </svg>
    );
  };
}
