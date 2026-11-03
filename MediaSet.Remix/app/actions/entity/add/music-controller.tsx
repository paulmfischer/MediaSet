import * as s from 'remix/data-schema';
import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { addEntity } from '../../../api/entity-data.ts';
import { getLookupCapabilities, isBarcodeLookupAvailable } from '../../../api/lookup-capabilities-data.ts';
import { Entity, type MusicEntity } from '../../../models.ts';
import type { AppContext } from '../../../router.ts';
import { routes } from '../../../routes.ts';
import { Document } from '../../../ui/document.tsx';
import { MusicFormPage } from '../../../ui/entity/add/music-form-page.tsx';
import { serverLogger } from '../../../utils/serverLogger.ts';
import { readCoverImage, entityWithImage } from '../../forms/cover-image.ts';
import { fieldErrors, submittedValues } from '../../forms/form-schema.ts';
import {
  LOOKUP_RESULTS_FIELD,
  parseApplyIntent,
  parseLookupResults,
  runLookup,
  type LookupOutcome,
} from '../../forms/lookup.ts';
import { loadMusicOptions } from '../music-options.ts';
import {
  MUSIC_FIELDS,
  musicForm,
  emptyMusicValues,
  toMusicEntity,
  toMusicValues,
  type MusicFormValues,
} from './music-form.ts';
import {
  applyMusicResult,
  parseMusicLookupIntent,
  toMusicLookupRequest,
  toMusicResultSummary,
} from './music-lookup.ts';

type MusicEntityResult = Extract<LookupOutcome, { status: 'results' }>['results'][number];

async function renderMusicForm(
  context: AppContext,
  state: {
    values?: MusicFormValues;
    errors?: Record<string, string>;
    formError?: string;
    status?: number;
    /** The outcome of a lookup, shown in the results dialog. */
    lookup?: LookupOutcome;
    /** Makes the browser take `values` over what is typed in its inputs. */
    formKey?: string;
  } = {}
) {
  const { values = emptyMusicValues, errors = {}, formError, status = 200, lookup, formKey } = state;
  const [{ options, unavailable }, capabilities] = await Promise.all([loadMusicOptions(), getLookupCapabilities()]);
  // The results travel back with the apply submission as form values, so applying needs no second lookup.
  const resultValues =
    lookup?.status === 'results' ? lookup.results.map((result) => toMusicValues(result as MusicEntityResult)) : [];
  return context.render(
    <Document path={context.url.pathname} title="Add Music | MediaSet" description="Add an album to your collection">
      <MusicFormPage
        heading="Add Music"
        action={routes.entityAdd.musics.action.href()}
        cancelHref={routes.entityLists.musics.href()}
        values={values}
        errors={errors}
        formError={formError}
        options={options}
        optionsUnavailable={unavailable}
        formKey={formKey}
        lookup={{
          available: isBarcodeLookupAvailable(capabilities, Entity.Musics),
          dialog: lookup
            ? {
                itemLabel: 'album',
                status: lookup.status,
                message: lookup.status === 'error' ? lookup.message : undefined,
                results: resultValues.map(toMusicResultSummary),
                resultsField: LOOKUP_RESULTS_FIELD,
                resultsValue: JSON.stringify(resultValues),
                returnFocusId: 'title',
              }
            : undefined,
        }}
      />
    </Document>,
    { status }
  );
}

// The form's other submit buttons: look metadata up, apply a result to the form, or close the dialog.
// None of them saves; each re-renders the form with what the user has entered so far.
async function handleLookupIntent(context: AppContext, intent: string) {
  const values = submittedValues(context.formData, MUSIC_FIELDS);

  const field = parseMusicLookupIntent(intent);
  if (field) {
    const outcome = toMusicLookupRequest(field, values);
    if ('missing' in outcome) {
      const labels = { title: 'a title', artist: 'an artist', barcode: 'a barcode' };
      return renderMusicForm(context, {
        values,
        errors: { [outcome.missing]: `Enter ${labels[outcome.missing]} to look up` },
        status: 400,
      });
    }
    const { request } = outcome;
    const lookup = await runLookup(Entity.Musics, request.identifierType, request.searchParams);
    return renderMusicForm(context, { values, lookup });
  }

  const index = parseApplyIntent(intent);
  if (index !== undefined) {
    const result = parseLookupResults(context.formData.get(LOOKUP_RESULTS_FIELD))[index];
    if (!result) {
      return renderMusicForm(context, {
        values,
        formError: 'That result is no longer available. Look it up again.',
        status: 400,
      });
    }
    return renderMusicForm(context, { values: applyMusicResult(values, result), formKey: crypto.randomUUID() });
  }

  // Closing the dialog, or an intent this form doesn't know.
  return renderMusicForm(context, { values });
}

export default createController(routes.entityAdd.musics, {
  actions: {
    index: (context) => renderMusicForm(context),
    async action(context) {
      const rawIntent = context.formData.get('intent');
      const intent = typeof rawIntent === 'string' ? rawIntent : 'save';
      if (intent !== 'save') return handleLookupIntent(context, intent);

      const parsed = s.parseSafe(musicForm, context.formData);
      const image = readCoverImage(context.formData);
      if (!parsed.success || image.error) {
        // Render here rather than redirecting to the GET page, which would lose the input and errors.
        return renderMusicForm(context, {
          values: submittedValues(context.formData, MUSIC_FIELDS),
          errors: {
            ...(image.error ? { coverImage: image.error } : {}),
            ...(parsed.success ? {} : fieldErrors(parsed.issues)),
          },
          status: 400,
        });
      }

      const album: MusicEntity = toMusicEntity(parsed.value);
      let saved: MusicEntity;
      try {
        // An uploaded file takes precedence over a URL, so the URL isn't sent with it.
        if (image.file) {
          delete album.imageUrl;
          saved = await addEntity(album, entityWithImage(album, image.file));
        } else {
          saved = await addEntity(album);
        }
      } catch (error) {
        serverLogger.error('Failed to add album', error, { operation: 'addMusic' });
        const values = submittedValues(context.formData, MUSIC_FIELDS);
        // The API rejecting the album (4xx) is the user's to fix and carries its reason; anything else is ours.
        if (error instanceof Response && error.status >= 400 && error.status < 500) {
          return renderMusicForm(context, { values, formError: await error.text(), status: 400 });
        }
        return renderMusicForm(context, {
          values,
          formError: 'The album could not be saved. Please try again.',
          status: 500,
        });
      }

      // The API always returns the new id; without it the album can't be opened, so say so rather than redirect blind.
      if (!saved.id) {
        serverLogger.error('Saved album has no id', undefined, { operation: 'addMusic' });
        return renderMusicForm(context, {
          values: submittedValues(context.formData, MUSIC_FIELDS),
          formError: 'The album was saved, but it could not be opened. Check your Music list before trying again.',
          status: 500,
        });
      }
      return redirect(routes.entityDetail.musics.href({ id: saved.id }), 303);
    },
  },
});
