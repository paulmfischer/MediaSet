import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { getEntity } from '../../api/entity-data.ts';
import { PlexActionError, unlinkPlexItem } from '../../api/plex-data.ts';
import { Entity, type MusicEntity } from '../../models.ts';
import type { AppContext } from '../../router.ts';
import { isPlexMusicEnabledForRequest } from '../../utils/plex-nav.server.ts';
import { routes } from '../../routes.ts';
import { Document } from '../../ui/document.tsx';
import { UnlinkPage } from '../../ui/plex/unlink-page.tsx';
import { createServerLogger } from '../../utils/serverLogger.ts';
import { entityErrorPage, renderError } from '../render-error.tsx';
import { plexFailurePage } from './failures.ts';

const serverLogger = createServerLogger('app/actions/plex/unlink-music-controller');

const renderUnavailable = entityErrorPage(routes.entityLists.musics.href(), 'Back to Music');

function unlinkFailure(context: AppContext, id: string, error: unknown) {
  const failure = plexFailurePage(error, 'unlink', 'album');
  return renderError(context, failure.status, error, {
    heading: failure.heading,
    message: failure.message,
    backHref: routes.entityDetail.musics.href({ id }),
    backLabel: 'Back to Album',
  });
}

// The confirmation page matches the delete pattern: GET shows it, POST unlinks and redirects (303).
async function index(context: AppContext) {
  const { id } = context.params as { id: string };
  if (!(await isPlexMusicEnabledForRequest())) return renderError(context, 404);
  try {
    const music = await getEntity<MusicEntity>(Entity.Musics, id);
    // Nothing to confirm for an unlinked album: the same answer the unlink itself gives
    if (!music.plex) return unlinkFailure(context, id, new PlexActionError('notLinked', 404));
    return context.render(
      <Document path={context.url.pathname} title={`Unlink ${music.title ?? 'Album'} from Plex | MediaSet`}>
        <UnlinkPage
          title={music.title ?? 'this album'}
          noun="album"
          action={routes.entityPlexUnlink.musics.action.href({ id })}
          cancelHref={routes.entityDetail.musics.href({ id })}
        />
      </Document>
    );
  } catch (error) {
    if (error instanceof Response && error.status === 404) {
      return renderUnavailable(context, 404, 'Music not found', "We couldn't find that album.");
    }
    serverLogger.error('Failed to load album for Plex unlink', error, { operation: 'plexUnlink', id });
    return renderUnavailable(
      context,
      500,
      "Couldn't load this album",
      'Something went wrong while loading it. Please try again.',
      error
    );
  }
}

async function action(context: AppContext) {
  const { id } = context.params as { id: string };
  const detailHref = routes.entityDetail.musics.href({ id });
  if (!(await isPlexMusicEnabledForRequest())) return renderError(context, 404);
  try {
    await unlinkPlexItem(id, 'music');
  } catch (error) {
    if (!(error instanceof PlexActionError)) {
      serverLogger.error('Failed to unlink album from Plex', error, { operation: 'plexUnlink', id });
    }
    return unlinkFailure(context, id, error);
  }
  return redirect(detailHref, 303);
}

export default createController(routes.entityPlexUnlink.musics, { actions: { index, action } });
