import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { deleteEntity, getEntity } from '../../../api/entity-data.ts';
import { Entity, type MusicEntity } from '../../../models.ts';
import type { AppContext } from '../../../router.ts';
import { routes } from '../../../routes.ts';
import { Document } from '../../../ui/document.tsx';
import { DeletePage } from '../../../ui/entity/delete/delete-page.tsx';
import { serverLogger } from '../../../utils/serverLogger.ts';
import { entityErrorPage } from '../../render-error.tsx';

const renderUnavailable = entityErrorPage(routes.entityLists.musics.href(), 'Back to Music');

const notFound = (context: AppContext) =>
  renderUnavailable(context, 404, 'Album not found', "We couldn't find that album.");

function renderConfirm(context: AppContext, album: MusicEntity, id: string, formError?: string, status = 200) {
  return context.render(
    <Document path={context.url.pathname} title={`Delete ${album.title ?? 'Music'} | MediaSet`}>
      <DeletePage
        singular="Music"
        title={album.title ?? 'this album'}
        action={routes.entityDelete.musics.action.href({ id })}
        cancelHref={routes.entityDetail.musics.href({ id })}
        formError={formError}
      />
    </Document>,
    { status }
  );
}

export default createController(routes.entityDelete.musics, {
  actions: {
    async index(context) {
      const { id } = context.params;
      try {
        return renderConfirm(context, await getEntity<MusicEntity>(Entity.Musics, id), id);
      } catch (error) {
        if (error instanceof Response && error.status === 404) return notFound(context);
        serverLogger.error('Failed to load album for deletion', error, { operation: 'deleteMusic', id });
        return renderUnavailable(
          context,
          500,
          "Couldn't load this album",
          'Something went wrong while loading it. Please try again.',
          error
        );
      }
    },
    async action(context) {
      const { id } = context.params;
      try {
        await deleteEntity(Entity.Musics, id);
      } catch (error) {
        if (error instanceof Response && error.status === 404) return notFound(context);
        serverLogger.error('Failed to delete album', error, { operation: 'deleteMusic', id });
        // Show the confirmation again with the error; the title comes from a fresh lookup.
        try {
          const album = await getEntity<MusicEntity>(Entity.Musics, id);
          return renderConfirm(context, album, id, 'The album could not be deleted. Please try again.', 500);
        } catch {
          return renderUnavailable(
            context,
            500,
            "Couldn't delete this album",
            'Something went wrong. Please try again.'
          );
        }
      }
      return redirect(routes.entityLists.musics.href(), 303);
    },
  },
});
