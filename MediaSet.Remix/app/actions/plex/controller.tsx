import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { getPlexStatuses, isPlexEnabledForMovies, startPlexSync } from '../../api/plex-data.ts';
import type { PlexEntityStatus } from '../../models.ts';
import type { AppContext } from '../../router.ts';
import { routes } from '../../routes.ts';
import { entityTypeLabel } from '../../utils/plex-sync.ts';
import { Document } from '../../ui/document.tsx';
import { PlexSyncPage } from '../../ui/plex/sync-page.tsx';
import { renderError } from '../render-error.tsx';
import { plexFailurePage } from './failures.ts';

const TITLE = 'Plex sync | MediaSet';
const back = { backHref: routes.home.href(), backLabel: 'Back to Home' };

function renderPage(context: AppContext, statuses: PlexEntityStatus[], notice?: string) {
  return context.render(
    <Document path={context.url.pathname} title={TITLE} description="Plex sync status">
      <PlexSyncPage
        statuses={statuses}
        action={routes.plex.action.href()}
        statusHref={routes.plexStatus.index.href()}
        notice={notice}
      />
    </Document>
  );
}

function failure(context: AppContext, error: unknown) {
  const page = plexFailurePage(error, 'sync');
  return renderError(context, page.status, error, { heading: page.heading, message: page.message, ...back });
}

async function index(context: AppContext) {
  if (!(await isPlexEnabledForMovies())) return renderError(context, 404);
  try {
    return renderPage(
      context,
      (await getPlexStatuses()).filter((status) => status.enabled)
    );
  } catch (error) {
    return failure(context, error);
  }
}

// Starts a sync, then returns to the page (303). A request that finds a run already active is not an error: the page
// is rendered again showing that run.
async function action(context: AppContext) {
  if (!(await isPlexEnabledForMovies())) return renderError(context, 404);
  const entityType = context.formData?.get('entityType');
  try {
    const statuses = (await getPlexStatuses()).filter((status) => status.enabled);
    const target = statuses.find((status) => typeof entityType === 'string' && status.entityType === entityType);
    if (!target)
      return renderError(context, 400, undefined, {
        heading: 'Unknown entity type',
        message: 'Pick one of the listed Plex entity types.',
        ...back,
      });
    const result = await startPlexSync(target.entityType);
    if (result.started) return redirect(routes.plex.index.href(), 303);
    // Read the status again so the page shows the run that is active now.
    return renderPage(
      context,
      (await getPlexStatuses()).filter((status) => status.enabled),
      `A ${entityTypeLabel(target.entityType)} sync is already in progress, so no new one was started.`
    );
  } catch (error) {
    return failure(context, error);
  }
}

export default createController(routes.plex, { actions: { index, action } });
