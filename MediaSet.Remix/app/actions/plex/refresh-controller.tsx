import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { isPlexEnabledForMovies, PlexActionError, refreshPlexItem, type PlexFailure } from '../../api/plex-data.ts';
import type { AppContext } from '../../router.ts';
import { routes } from '../../routes.ts';
import { renderError } from '../render-error.tsx';

const FAILURES: Record<PlexFailure, { status: number; heading: string; message: string }> = {
  unreachable: {
    status: 502,
    heading: "Couldn't reach Plex",
    message: 'MediaSet could not reach your Plex server. Check that it is running and try again.',
  },
  syncInProgress: {
    status: 409,
    heading: 'A Plex sync is running',
    message: 'A Plex sync is already in progress. Wait for it to finish, then refresh again.',
  },
  itemMissing: {
    status: 404,
    heading: 'Item missing on Plex',
    message: 'Plex no longer has this item. It may have been removed from your library.',
  },
  notLinked: {
    status: 400,
    heading: 'Not linked to Plex',
    message: 'This movie is not linked to a Plex item, so there is nothing to refresh.',
  },
  other: {
    status: 500,
    heading: "Couldn't refresh from Plex",
    message: 'Something went wrong while refreshing from Plex. Please try again.',
  },
};

// Re-reads one movie from Plex, then returns to its detail page. Reached only by the card's Refresh button.
async function refreshMovie(context: AppContext) {
  const { id } = context.params as { id: string };
  const detailHref = routes.entityDetail.movies.href({ id });
  const back = { backHref: detailHref, backLabel: 'Back to Movie' };

  if (!(await isPlexEnabledForMovies())) return renderError(context, 404);

  try {
    await refreshPlexItem(id);
  } catch (error) {
    const failure = error instanceof PlexActionError ? FAILURES[error.failure] : FAILURES.other;
    return renderError(context, failure.status, error, { heading: failure.heading, message: failure.message, ...back });
  }
  return redirect(detailHref, 303);
}

export default createController(routes.entityPlexRefresh, {
  actions: {
    movies: (context) => refreshMovie(context),
  },
});
