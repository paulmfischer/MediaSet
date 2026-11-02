import * as s from 'remix/data-schema';
import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { getEntity, updateEntity } from '../../../api/entity-data.ts';
import { clientApiUrl } from '../../../constants.server.ts';
import { Entity, type GameEntity } from '../../../models.ts';
import type { AppContext } from '../../../router.ts';
import { routes } from '../../../routes.ts';
import { Document } from '../../../ui/document.tsx';
import { GameFormPage } from '../../../ui/entity/add/game-form-page.tsx';
import { UnavailablePage } from '../../../ui/entity/unavailable-page.tsx';
import { serverLogger } from '../../../utils/serverLogger.ts';
import { entityWithImage, readCoverImage } from '../../forms/cover-image.ts';
import { fieldErrors, submittedValues } from '../../forms/form-schema.ts';
import { GAME_FIELDS, gameForm, toGameUpdate, toGameValues, type GameFormValues } from '../add/game-form.ts';
import { loadGameOptions } from '../game-options.ts';

const isStatus = (error: unknown, predicate: (status: number) => boolean): error is Response =>
  error instanceof Response && predicate(error.status);

function renderUnavailable(context: AppContext, status: number, heading: string, message: string) {
  return context.render(
    <Document path={context.url.pathname} title={`${heading} | MediaSet`}>
      <UnavailablePage
        heading={heading}
        message={message}
        backHref={routes.entityLists.games.href()}
        backLabel="Back to Games"
      />
    </Document>,
    { status }
  );
}

async function renderEditForm(
  context: AppContext,
  id: string,
  game: GameEntity,
  state: { values?: GameFormValues; errors?: Record<string, string>; formError?: string; status?: number } = {}
) {
  const { values = toGameValues(game), errors = {}, formError, status = 200 } = state;
  const { options, unavailable } = await loadGameOptions();
  return context.render(
    <Document
      path={context.url.pathname}
      title={`Edit ${game.title ?? 'Game'} | MediaSet`}
      description="Edit a game in your collection"
    >
      <GameFormPage
        heading="Edit Game"
        action={routes.entityEdit.games.action.href({ id })}
        cancelHref={routes.entityDetail.games.href({ id })}
        values={values}
        errors={errors}
        formError={formError}
        options={options}
        optionsUnavailable={unavailable}
        existingImageSrc={
          game.coverImage
            ? `${clientApiUrl}/static/images/${game.coverImage.filePath.split('/').map(encodeURIComponent).join('/')}`
            : undefined
        }
      />
    </Document>,
    { status }
  );
}

/** Loads the game, or returns the 404/500 page to send instead. */
async function loadGame(context: AppContext, id: string): Promise<GameEntity | Response> {
  try {
    return await getEntity<GameEntity>(Entity.Games, id);
  } catch (error) {
    if (isStatus(error, (status) => status === 404)) {
      return renderUnavailable(context, 404, 'Game not found', "We couldn't find that game.");
    }
    serverLogger.error('Failed to load game for editing', error, { operation: 'editGame', id });
    return renderUnavailable(
      context,
      500,
      "Couldn't load this game",
      'Something went wrong while loading it. Please try again.'
    );
  }
}

export default createController(routes.entityEdit.games, {
  actions: {
    async index(context) {
      const game = await loadGame(context, context.params.id);
      return game instanceof Response ? game : renderEditForm(context, context.params.id, game);
    },
    async action(context) {
      const { id } = context.params;
      const game = await loadGame(context, id);
      if (game instanceof Response) return game;

      const parsed = s.parseSafe(gameForm, context.formData);
      const image = readCoverImage(context.formData);
      if (!parsed.success || image.error) {
        return renderEditForm(context, id, game, {
          values: submittedValues(context.formData, GAME_FIELDS),
          errors: {
            ...(image.error ? { coverImage: image.error } : {}),
            ...(parsed.success ? {} : fieldErrors(parsed.issues)),
          },
          status: 400,
        });
      }

      try {
        const update = toGameUpdate(game, parsed.value, context.formData.get('removeImage') !== null);
        // An uploaded file takes precedence over a URL, so the URL isn't sent with it.
        if (image.file) {
          delete update.imageUrl;
          await updateEntity(id, update, entityWithImage(update, image.file));
        } else {
          await updateEntity(id, update);
        }
      } catch (error) {
        serverLogger.error('Failed to update game', error, { operation: 'editGame', id });
        if (isStatus(error, (status) => status === 404)) {
          return renderUnavailable(context, 404, 'Game not found', "We couldn't find that game.");
        }
        const values = submittedValues(context.formData, GAME_FIELDS);
        // The API rejecting the game (4xx) is the user's to fix and carries its reason; anything else is ours.
        if (isStatus(error, (status) => status >= 400 && status < 500)) {
          return renderEditForm(context, id, game, { values, formError: await error.text(), status: 400 });
        }
        return renderEditForm(context, id, game, {
          values,
          formError: 'The game could not be saved. Please try again.',
          status: 500,
        });
      }
      return redirect(routes.entityDetail.games.href({ id }), 303);
    },
  },
});
