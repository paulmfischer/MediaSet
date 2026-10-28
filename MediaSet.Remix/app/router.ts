import { compression } from 'remix/middleware/compression';
import { render } from 'remix/middleware/render';
import { staticFiles } from 'remix/middleware/static';
import { createRouter, type RouterContext } from 'remix/router';

import controller from './actions/controller.tsx';
import { assets } from './assets.ts';
import { routes } from './routes.ts';

export const router = createRouter({
  middleware: [compression(), staticFiles('./public', { index: false }), render({ assets })],
});

export type AppContext = RouterContext<typeof router>;

declare module 'remix' {
  interface RouterTypes {
    context: AppContext;
  }
}

router.map(routes, controller);
