// lucide ships one ES module per icon without per-file types. Each default export is an icon node.
declare module 'lucide/dist/esm/icons/*.mjs' {
  import type { IconNode } from 'lucide';
  const iconNode: IconNode;
  export default iconNode;
}
