import type { Handle } from 'remix/component';

import { Icon } from './icon.tsx';
import { Shell } from './icons.ts';

export function Spinner(handle: Handle<{ size?: number }>) {
  return () => <Icon icon={Shell} size={handle.props.size} class="animate-spin" />;
}
