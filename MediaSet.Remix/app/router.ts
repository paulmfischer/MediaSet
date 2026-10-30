import { compression } from 'remix/middleware/compression';
import { render } from 'remix/middleware/render';
import { staticFiles } from 'remix/middleware/static';
import { createRouter, type RouterContext } from 'remix/router';

import controller from './actions/controller.tsx';
import { boundedFormData } from './actions/forms/form-data.tsx';
import entityAddController from './actions/entity/add/controller.tsx';
import entityDeleteController from './actions/entity/delete/controller.tsx';
import entityDetailController from './actions/entity/detail-controller.tsx';
import entityEditController from './actions/entity/edit/controller.tsx';
import entityListsController from './actions/entity/controller.tsx';
import { assets } from './assets.ts';
import { routes } from './routes.ts';

export const router = createRouter({
  middleware: [compression(), staticFiles('./public', { index: false }), render({ assets }), boundedFormData()],
});

export type AppContext = RouterContext<typeof router>;

declare module 'remix' {
  interface RouterTypes {
    context: AppContext;
  }
}

router.map(routes, controller);
router.map(routes.entityLists, entityListsController);
router.map(routes.entityDetail, entityDetailController);
router.map(routes.entityAdd.books, entityAddController);
router.map(routes.entityEdit.books, entityEditController);
router.map(routes.entityDelete.books, entityDeleteController);
