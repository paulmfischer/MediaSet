import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { deleteEntity, getEntity } from '../../../api/entity-data.ts';
import { Entity, type MovieEntity } from '../../../models.ts';
import type { AppContext } from '../../../router.ts';
import { routes } from '../../../routes.ts';
import { Document } from '../../../ui/document.tsx';
import { DeletePage } from '../../../ui/entity/delete/delete-page.tsx';
import { serverLogger } from '../../../utils/serverLogger.ts';
import { entityErrorPage } from '../../render-error.tsx';

const renderUnavailable = entityErrorPage(routes.entityLists.movies.href(), 'Back to Movies');

const notFound = (context: AppContext) =>
  renderUnavailable(context, 404, 'Movie not found', "We couldn't find that movie.");

function renderConfirm(context: AppContext, movie: MovieEntity, id: string, formError?: string, status = 200) {
  return context.render(
    <Document path={context.url.pathname} title={`Delete ${movie.title ?? 'Movie'} | MediaSet`}>
      <DeletePage
        singular="Movie"
        title={movie.title ?? 'this movie'}
        action={routes.entityDelete.movies.action.href({ id })}
        cancelHref={routes.entityDetail.movies.href({ id })}
        formError={formError}
      />
    </Document>,
    { status }
  );
}

export default createController(routes.entityDelete.movies, {
  actions: {
    async index(context) {
      const { id } = context.params;
      try {
        return renderConfirm(context, await getEntity<MovieEntity>(Entity.Movies, id), id);
      } catch (error) {
        if (error instanceof Response && error.status === 404) return notFound(context);
        serverLogger.error('Failed to load movie for deletion', error, { operation: 'deleteMovie', id });
        return renderUnavailable(
          context,
          500,
          "Couldn't load this movie",
          'Something went wrong while loading it. Please try again.',
          error
        );
      }
    },
    async action(context) {
      const { id } = context.params;
      try {
        await deleteEntity(Entity.Movies, id);
      } catch (error) {
        if (error instanceof Response && error.status === 404) return notFound(context);
        serverLogger.error('Failed to delete movie', error, { operation: 'deleteMovie', id });
        // Show the confirmation again with the error; the title comes from a fresh lookup.
        try {
          const movie = await getEntity<MovieEntity>(Entity.Movies, id);
          return renderConfirm(context, movie, id, 'The movie could not be deleted. Please try again.', 500);
        } catch {
          return renderUnavailable(
            context,
            500,
            "Couldn't delete this movie",
            'Something went wrong. Please try again.'
          );
        }
      }
      return redirect(routes.entityLists.movies.href(), 303);
    },
  },
});
