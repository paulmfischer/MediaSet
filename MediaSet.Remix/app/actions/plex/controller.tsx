import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { getPlexStatuses, startPlexSync } from '../../api/plex-data.ts';
import type { PlexEntityStatus } from '../../models.ts';
import type { AppContext } from '../../router.ts';
import { routes } from '../../routes.ts';
import { isPlexEnabledForRequest } from '../../utils/plex-nav.server.ts';
import { hasActiveRun, isListedStatus, SYNCABLE_ENTITY_TYPES } from '../../utils/plex-sync.ts';
import { Document } from '../../ui/document.tsx';
import { PlexSyncPage } from '../../ui/plex/sync-page.tsx';
import { renderError } from '../render-error.tsx';
import { plexFailurePage } from './failures.ts';

const TITLE = 'Plex sync | MediaSet';
const RUNNING_NOTICE = 'notice=running';
const RUNNING_MESSAGE = 'A sync was already in progress, so no new one was started. This is the current run.';
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
  if (!(await isPlexEnabledForRequest())) return renderError(context, 404);
  try {
    const statuses = (await getPlexStatuses()).filter(isListedStatus);
    // The notice only makes sense while a run is active; a stale link shows the plain page.
    const notice =
      context.url.searchParams.get('notice') === 'running' && hasActiveRun(statuses) ? RUNNING_MESSAGE : undefined;
    return renderPage(context, statuses, notice);
  } catch (error) {
    return failure(context, error);
  }
}

// Starts a sync, then returns to the page (303). A request that finds a run already active is not an error: it goes
// back to the page with a notice, which shows that run, so a refresh does not submit the form again.
async function action(context: AppContext) {
  if (!(await isPlexEnabledForRequest())) return renderError(context, 404);
  const entityType = context.formData?.get('entityType');
  if (typeof entityType !== 'string' || !SYNCABLE_ENTITY_TYPES.includes(entityType)) {
    return renderError(context, 400, undefined, {
      heading: 'Unknown entity type',
      message: 'Pick one of the listed Plex entity types.',
      ...back,
    });
  }
  try {
    const started = await startPlexSync(entityType);
    return redirect(started ? routes.plex.index.href() : `${routes.plex.index.href()}?${RUNNING_NOTICE}`, 303);
  } catch (error) {
    return failure(context, error);
  }
}

export default createController(routes.plex, { actions: { index, action } });
