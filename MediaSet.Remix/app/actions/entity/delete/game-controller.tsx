import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { deleteEntity, getEntity } from '../../../api/entity-data.ts';
import { Entity, type GameEntity } from '../../../models.ts';
import type { AppContext } from '../../../router.ts';
import { routes } from '../../../routes.ts';
import { Document } from '../../../ui/document.tsx';
import { DeletePage } from '../../../ui/entity/delete/delete-page.tsx';
import { createServerLogger } from '../../../utils/serverLogger.ts';
import { entityErrorPage } from '../../render-error.tsx';

const serverLogger = createServerLogger('app/actions/entity/delete/game-controller');

const renderUnavailable = entityErrorPage(routes.entityLists.games.href(), 'Back to Games');

const notFound = (context: AppContext) =>
  renderUnavailable(context, 404, 'Game not found', "We couldn't find that game.");

function renderConfirm(context: AppContext, game: GameEntity, id: string, formError?: string, status = 200) {
  return context.render(
    <Document path={context.url.pathname} title={`Delete ${game.title ?? 'Game'} | MediaSet`}>
      <DeletePage
        singular="Game"
        title={game.title ?? 'this game'}
        action={routes.entityDelete.games.action.href({ id })}
        cancelHref={routes.entityDetail.games.href({ id })}
        formError={formError}
      />
    </Document>,
    { status }
  );
}

export default createController(routes.entityDelete.games, {
  actions: {
    async index(context) {
      const { id } = context.params;
      try {
        return renderConfirm(context, await getEntity<GameEntity>(Entity.Games, id), id);
      } catch (error) {
        if (error instanceof Response && error.status === 404) return notFound(context);
        serverLogger.error('Failed to load game for deletion', error, { operation: 'deleteGame', id });
        return renderUnavailable(
          context,
          500,
          "Couldn't load this game",
          'Something went wrong while loading it. Please try again.',
          error
        );
      }
    },
    async action(context) {
      const { id } = context.params;
      try {
        await deleteEntity(Entity.Games, id);
      } catch (error) {
        if (error instanceof Response && error.status === 404) return notFound(context);
        serverLogger.error('Failed to delete game', error, { operation: 'deleteGame', id });
        // Show the confirmation again with the error; the title comes from a fresh lookup.
        try {
          const game = await getEntity<GameEntity>(Entity.Games, id);
          return renderConfirm(context, game, id, 'The game could not be deleted. Please try again.', 500);
        } catch {
          return renderUnavailable(
            context,
            500,
            "Couldn't delete this game",
            'Something went wrong. Please try again.'
          );
        }
      }
      return redirect(routes.entityLists.games.href(), 303);
    },
  },
});
