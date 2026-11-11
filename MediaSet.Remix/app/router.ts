import { compression } from 'remix/middleware/compression';
import { render } from 'remix/middleware/render';
import { staticFiles } from 'remix/middleware/static';
import { createRouter, type Middleware, type RouterContext } from 'remix/router';

import controller from './actions/controller.tsx';
import { boundedFormData } from './actions/forms/form-data.tsx';
import entityAddController from './actions/entity/add/controller.tsx';
import entityAddGameController from './actions/entity/add/game-controller.tsx';
import entityAddMovieController from './actions/entity/add/movie-controller.tsx';
import entityAddMusicController from './actions/entity/add/music-controller.tsx';
import entityClearImageLookupController from './actions/entity/clear-image-lookup-controller.tsx';
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
import entityPlexLinkController from './actions/plex/link-controller.tsx';
import entityPlexRefreshController from './actions/plex/refresh-controller.tsx';
import entityPlexUnlinkController from './actions/plex/unlink-controller.tsx';
import imageStatsController from './actions/image-stats/controller.tsx';
import { renderError, type ErrorContext } from './actions/render-error.tsx';
import { assets } from './assets.ts';
import { requestContext } from './middleware/request-context.ts';
import { securityHeaders, type SecurityHeadersOptions } from './middleware/security-headers.ts';
import { routes } from './routes.ts';
import { serverLogger } from './utils/serverLogger.ts';

// Turns anything an action throws into a rendered error page: a thrown 404 Response is the shared
// 404 page, everything else is logged and answered with the generic 500 page.
export const errorPages: Middleware = async (context, next) => {
  try {
    return await next();
  } catch (error) {
    const app = context as unknown as ErrorContext;
    if (error instanceof Response && error.status === 404) return renderError(app, 404, error);
    serverLogger.error('Unhandled error while handling a request', error, {
      operation: 'router',
      method: context.request.method,
      path: context.url.pathname,
    });
    return renderError(app, 500, error);
  }
};

export function createAppRouter(options: SecurityHeadersOptions = {}) {
  const router = createRouter({
    middleware: [
      // First, so static files and error pages get the headers and every API call a trace context.
      securityHeaders(options),
      requestContext(),
      compression(),
      staticFiles('./public', { index: false }),
      render({ assets }),
      errorPages,
      boundedFormData(),
    ],
    // Paths no route matches, including unknown collections, get the shared 404 page.
    defaultHandler: (context) => renderError(context, 404),
  });

  router.map(routes, controller);
  router.map(routes.entityLists, entityListsController);
  router.map(routes.entityDetail, entityDetailController);
  router.map(routes.entityClearImageLookup, entityClearImageLookupController);
  router.map(routes.entityPlexRefresh, entityPlexRefreshController);
  router.map(routes.entityPlexLink.movies, entityPlexLinkController);
  router.map(routes.entityPlexUnlink.movies, entityPlexUnlinkController);
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
  router.map(routes.imageStats, imageStatsController);

  return router;
}

export const router = createAppRouter();

export type AppContext = RouterContext<typeof router>;

declare module 'remix' {
  interface RouterTypes {
    context: AppContext;
  }
}
