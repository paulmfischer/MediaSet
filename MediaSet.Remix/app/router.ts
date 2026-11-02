import { compression } from 'remix/middleware/compression';
import { render } from 'remix/middleware/render';
import { staticFiles } from 'remix/middleware/static';
import { createRouter, type RouterContext } from 'remix/router';

import controller from './actions/controller.tsx';
import { boundedFormData } from './actions/forms/form-data.tsx';
import entityAddController from './actions/entity/add/controller.tsx';
import entityAddGameController from './actions/entity/add/game-controller.tsx';
import entityAddMovieController from './actions/entity/add/movie-controller.tsx';
import entityAddMusicController from './actions/entity/add/music-controller.tsx';
import entityDeleteController from './actions/entity/delete/controller.tsx';
import entityDeleteGameController from './actions/entity/delete/game-controller.tsx';
import entityDeleteMovieController from './actions/entity/delete/movie-controller.tsx';
import entityDeleteMusicController from './actions/entity/delete/music-controller.tsx';
import entityDetailController from './actions/entity/detail-controller.tsx';
import entityEditController from './actions/entity/edit/controller.tsx';
import entityEditGameController from './actions/entity/edit/game-controller.tsx';
import entityEditMovieController from './actions/entity/edit/movie-controller.tsx';
import entityEditMusicController from './actions/entity/edit/music-controller.tsx';
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
router.map(routes.entityAdd.movies, entityAddMovieController);
router.map(routes.entityEdit.movies, entityEditMovieController);
router.map(routes.entityDelete.movies, entityDeleteMovieController);
router.map(routes.entityAdd.games, entityAddGameController);
router.map(routes.entityEdit.games, entityEditGameController);
router.map(routes.entityDelete.games, entityDeleteGameController);
router.map(routes.entityAdd.musics, entityAddMusicController);
router.map(routes.entityEdit.musics, entityEditMusicController);
router.map(routes.entityDelete.musics, entityDeleteMusicController);
