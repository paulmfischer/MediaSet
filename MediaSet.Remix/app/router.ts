import { compression } from 'remix/middleware/compression';
import { formData } from 'remix/middleware/form-data';
import { render } from 'remix/middleware/render';
import { staticFiles } from 'remix/middleware/static';
import { createRouter, type RouterContext } from 'remix/router';

import controller from './actions/controller.tsx';
import entityAddController from './actions/entity/add/controller.tsx';
import entityListsController from './actions/entity/controller.tsx';
import { assets } from './assets.ts';
import { routes } from './routes.ts';

export const router = createRouter({
  middleware: [compression(), staticFiles('./public', { index: false }), formData(), render({ assets })],
});

export type AppContext = RouterContext<typeof router>;

declare module 'remix' {
  interface RouterTypes {
    context: AppContext;
  }
}

router.map(routes, controller);
router.map(routes.entityLists, entityListsController);
router.map(routes.entityAdd.books, entityAddController);
