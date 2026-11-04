import * as s from 'remix/data-schema';
import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { addEntity } from '../../../api/entity-data.ts';
import { getLookupCapabilities, isLookupAvailable } from '../../../api/lookup-capabilities-data.ts';
import { Entity, type GameEntity } from '../../../models.ts';
import type { AppContext } from '../../../router.ts';
import { routes } from '../../../routes.ts';
import { Document } from '../../../ui/document.tsx';
import { GameFormPage } from '../../../ui/entity/add/game-form-page.tsx';
import { serverLogger } from '../../../utils/serverLogger.ts';
import { readCoverImage, entityWithImage } from '../../forms/cover-image.ts';
import { fieldErrors, submittedValues } from '../../forms/form-schema.ts';
import { buildLookupProps, type LookupOutcome } from '../../forms/lookup.ts';
import { loadGameOptions } from '../game-options.ts';
import {
  GAME_FIELDS,
  gameForm,
  emptyGameValues,
  toGameEntity,
  toGameValues,
  type GameFormValues,
} from './game-form.ts';
import { resolveGameLookupIntent, toGameResultSummary } from './game-lookup.ts';

async function renderGameForm(
  context: AppContext,
  state: {
    values?: GameFormValues;
    errors?: Record<string, string>;
    formError?: string;
    status?: number;
    /** The outcome of a lookup, shown in the results dialog. */
    lookup?: LookupOutcome;
    /** Makes the browser take `values` over what is typed in its inputs. */
    formKey?: string;
  } = {}
) {
  const { values = emptyGameValues, errors = {}, formError, status = 200, lookup, formKey } = state;
  const [{ options, unavailable }, capabilities] = await Promise.all([loadGameOptions(), getLookupCapabilities()]);
  return context.render(
    <Document path={context.url.pathname} title="Add Game | MediaSet" description="Add a game to your collection">
      <GameFormPage
        heading="Add Game"
        action={routes.entityAdd.games.action.href()}
        cancelHref={routes.entityLists.games.href()}
        values={values}
        errors={errors}
        formError={formError}
        options={options}
        optionsUnavailable={unavailable}
        formKey={formKey}
        lookup={buildLookupProps({
          entityType: Entity.Games,
          available: isLookupAvailable(capabilities, Entity.Games),
          itemLabel: 'game',
          lookup,
          toValues: toGameValues,
          toSummary: toGameResultSummary,
        })}
      />
    </Document>,
    { status }
  );
}

// The form's other submit buttons: look metadata up, apply a result to the form, or close the dialog.
// None of them saves; each re-renders the form with what the user has entered so far.
function handleLookupIntent(context: AppContext, intent: string) {
  return resolveGameLookupIntent(context.formData, intent).then((state) => renderGameForm(context, state));
}

export default createController(routes.entityAdd.games, {
  actions: {
    index: (context) => renderGameForm(context),
    async action(context) {
      const rawIntent = context.formData.get('intent');
      const intent = typeof rawIntent === 'string' ? rawIntent : 'save';
      if (intent !== 'save') return handleLookupIntent(context, intent);

      const parsed = s.parseSafe(gameForm, context.formData);
      const image = readCoverImage(context.formData);
      if (!parsed.success || image.error) {
        // Render here rather than redirecting to the GET page, which would lose the input and errors.
        return renderGameForm(context, {
          values: submittedValues(context.formData, GAME_FIELDS),
          errors: {
            ...(image.error ? { coverImage: image.error } : {}),
            ...(parsed.success ? {} : fieldErrors(parsed.issues)),
          },
          status: 400,
        });
      }

      const game: GameEntity = toGameEntity(parsed.value);
      let saved: GameEntity;
      try {
        // An uploaded file takes precedence over a URL, so the URL isn't sent with it.
        if (image.file) {
          delete game.imageUrl;
          saved = await addEntity(game, entityWithImage(game, image.file));
        } else {
          saved = await addEntity(game);
        }
      } catch (error) {
        serverLogger.error('Failed to add game', error, { operation: 'addGame' });
        const values = submittedValues(context.formData, GAME_FIELDS);
        // The API rejecting the game (4xx) is the user's to fix and carries its reason; anything else is ours.
        if (error instanceof Response && error.status >= 400 && error.status < 500) {
          return renderGameForm(context, { values, formError: await error.text(), status: 400 });
        }
        return renderGameForm(context, {
          values,
          formError: 'The game could not be saved. Please try again.',
          status: 500,
        });
      }

      // The API always returns the new id; without it the game can't be opened, so say so rather than redirect blind.
      if (!saved.id) {
        serverLogger.error('Saved game has no id', undefined, { operation: 'addGame' });
        return renderGameForm(context, {
          values: submittedValues(context.formData, GAME_FIELDS),
          formError: 'The game was saved, but it could not be opened. Check your Games list before trying again.',
          status: 500,
        });
      }
      return redirect(routes.entityDetail.games.href({ id: saved.id }), 303);
    },
  },
});
