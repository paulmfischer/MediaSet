import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { getEntity } from '../../api/entity-data.ts';
import { PlexActionError, unlinkPlexItem } from '../../api/plex-data.ts';
import { Entity, type MovieEntity } from '../../models.ts';
import type { AppContext } from '../../router.ts';
import { isPlexEnabledForRequest } from '../../utils/plex-nav.server.ts';
import { routes } from '../../routes.ts';
import { Document } from '../../ui/document.tsx';
import { UnlinkPage } from '../../ui/plex/unlink-page.tsx';
import { createServerLogger } from '../../utils/serverLogger.ts';
import { entityErrorPage, renderError } from '../render-error.tsx';
import { plexFailurePage } from './failures.ts';

const serverLogger = createServerLogger('app/actions/plex/unlink-controller');

const renderUnavailable = entityErrorPage(routes.entityLists.movies.href(), 'Back to Movies');

function unlinkFailure(context: AppContext, id: string, error: unknown) {
  const failure = plexFailurePage(error, 'unlink');
  return renderError(context, failure.status, error, {
    heading: failure.heading,
    message: failure.message,
    backHref: routes.entityDetail.movies.href({ id }),
    backLabel: 'Back to Movie',
  });
}

// The confirmation page matches the delete pattern: GET shows it, POST unlinks and redirects (303).
async function index(context: AppContext) {
  const { id } = context.params as { id: string };
  if (!(await isPlexEnabledForRequest())) return renderError(context, 404);
  try {
    const movie = await getEntity<MovieEntity>(Entity.Movies, id);
    // Nothing to confirm for an unlinked movie: the same answer the unlink itself gives
    if (!movie.plex) return unlinkFailure(context, id, new PlexActionError('notLinked', 404));
    return context.render(
      <Document path={context.url.pathname} title={`Unlink ${movie.title ?? 'Movie'} from Plex | MediaSet`}>
        <UnlinkPage
          title={movie.title ?? 'this movie'}
          action={routes.entityPlexUnlink.movies.action.href({ id })}
          cancelHref={routes.entityDetail.movies.href({ id })}
        />
      </Document>
    );
  } catch (error) {
    if (error instanceof Response && error.status === 404) {
      return renderUnavailable(context, 404, 'Movie not found', "We couldn't find that movie.");
    }
    serverLogger.error('Failed to load movie for Plex unlink', error, { operation: 'plexUnlink', id });
    return renderUnavailable(
      context,
      500,
      "Couldn't load this movie",
      'Something went wrong while loading it. Please try again.',
      error
    );
  }
}

async function action(context: AppContext) {
  const { id } = context.params as { id: string };
  const detailHref = routes.entityDetail.movies.href({ id });
  if (!(await isPlexEnabledForRequest())) return renderError(context, 404);
  try {
    await unlinkPlexItem(id);
  } catch (error) {
    if (!(error instanceof PlexActionError)) {
      serverLogger.error('Failed to unlink movie from Plex', error, { operation: 'plexUnlink', id });
    }
    return unlinkFailure(context, id, error);
  }
  return redirect(detailHref, 303);
}

export default createController(routes.entityPlexUnlink.movies, { actions: { index, action } });
