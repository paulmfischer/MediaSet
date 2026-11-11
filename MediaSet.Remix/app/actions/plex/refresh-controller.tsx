import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { refreshPlexItem } from '../../api/plex-data.ts';
import type { AppContext } from '../../router.ts';
import { isPlexEnabledForRequest } from '../../utils/plex-nav.server.ts';
import { routes } from '../../routes.ts';
import { renderError } from '../render-error.tsx';
import { plexFailurePage } from './failures.ts';

// Re-reads one movie from Plex, then returns to its detail page. Reached only by the card's Refresh button.
async function refreshMovie(context: AppContext) {
  const { id } = context.params as { id: string };
  const detailHref = routes.entityDetail.movies.href({ id });
  const back = { backHref: detailHref, backLabel: 'Back to Movie' };

  if (!(await isPlexEnabledForRequest())) return renderError(context, 404);

  try {
    await refreshPlexItem(id);
  } catch (error) {
    const failure = plexFailurePage(error, 'refresh');
    return renderError(context, failure.status, error, { heading: failure.heading, message: failure.message, ...back });
  }
  return redirect(detailHref, 303);
}

export default createController(routes.entityPlexRefresh, {
  actions: {
    movies: (context) => refreshMovie(context),
  },
});
