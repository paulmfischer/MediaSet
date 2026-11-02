import * as s from 'remix/data-schema';
import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { addEntity } from '../../../api/entity-data.ts';
import { getLookupCapabilities, isBarcodeLookupAvailable } from '../../../api/lookup-capabilities-data.ts';
import { Entity, type MovieEntity } from '../../../models.ts';
import type { AppContext } from '../../../router.ts';
import { routes } from '../../../routes.ts';
import { Document } from '../../../ui/document.tsx';
import { MovieFormPage } from '../../../ui/entity/add/movie-form-page.tsx';
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
import { loadMovieOptions } from '../movie-options.ts';
import {
  MOVIE_FIELDS,
  movieForm,
  emptyMovieValues,
  toMovieEntity,
  toMovieValues,
  type MovieFormValues,
} from './movie-form.ts';
import {
  applyMovieResult,
  parseMovieLookupIntent,
  toMovieLookupRequest,
  toMovieResultSummary,
} from './movie-lookup.ts';

type MovieEntityResult = Extract<LookupOutcome, { status: 'results' }>['results'][number];

async function renderMovieForm(
  context: AppContext,
  state: {
    values?: MovieFormValues;
    errors?: Record<string, string>;
    formError?: string;
    status?: number;
    /** The outcome of a lookup, shown in the results dialog. */
    lookup?: LookupOutcome;
    /** Makes the browser take `values` over what is typed in its inputs. */
    formKey?: string;
  } = {}
) {
  const { values = emptyMovieValues, errors = {}, formError, status = 200, lookup, formKey } = state;
  const [{ options, unavailable }, capabilities] = await Promise.all([loadMovieOptions(), getLookupCapabilities()]);
  // The results travel back with the apply submission as form values, so applying needs no second lookup.
  const resultValues =
    lookup?.status === 'results' ? lookup.results.map((result) => toMovieValues(result as MovieEntityResult)) : [];
  return context.render(
    <Document path={context.url.pathname} title="Add Movie | MediaSet" description="Add a movie to your collection">
      <MovieFormPage
        heading="Add Movie"
        action={routes.entityAdd.movies.action.href()}
        cancelHref={routes.entityLists.movies.href()}
        values={values}
        errors={errors}
        formError={formError}
        options={options}
        optionsUnavailable={unavailable}
        formKey={formKey}
        lookup={{
          available: isBarcodeLookupAvailable(capabilities, Entity.Movies),
          dialog: lookup
            ? {
                itemLabel: 'movie',
                status: lookup.status,
                message: lookup.status === 'error' ? lookup.message : undefined,
                results: resultValues.map(toMovieResultSummary),
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
  const values = submittedValues(context.formData, MOVIE_FIELDS);

  const field = parseMovieLookupIntent(intent);
  if (field) {
    const request = toMovieLookupRequest(field, values);
    if (!request) {
      const label = field === 'barcode' ? 'a barcode' : 'a title';
      return renderMovieForm(context, { values, errors: { [field]: `Enter ${label} to look up` }, status: 400 });
    }
    const lookup = await runLookup(Entity.Movies, request.identifierType, request.searchParams);
    return renderMovieForm(context, { values, lookup });
  }

  const index = parseApplyIntent(intent);
  if (index !== undefined) {
    const result = parseLookupResults(context.formData.get(LOOKUP_RESULTS_FIELD))[index];
    if (!result) {
      return renderMovieForm(context, {
        values,
        formError: 'That result is no longer available. Look it up again.',
        status: 400,
      });
    }
    return renderMovieForm(context, { values: applyMovieResult(values, result), formKey: crypto.randomUUID() });
  }

  // Closing the dialog, or an intent this form doesn't know.
  return renderMovieForm(context, { values });
}

export default createController(routes.entityAdd.movies, {
  actions: {
    index: (context) => renderMovieForm(context),
    async action(context) {
      const rawIntent = context.formData.get('intent');
      const intent = typeof rawIntent === 'string' ? rawIntent : 'save';
      if (intent !== 'save') return handleLookupIntent(context, intent);

      const parsed = s.parseSafe(movieForm, context.formData);
      const image = readCoverImage(context.formData);
      if (!parsed.success || image.error) {
        // Render here rather than redirecting to the GET page, which would lose the input and errors.
        return renderMovieForm(context, {
          values: submittedValues(context.formData, MOVIE_FIELDS),
          errors: {
            ...(image.error ? { coverImage: image.error } : {}),
            ...(parsed.success ? {} : fieldErrors(parsed.issues)),
          },
          status: 400,
        });
      }

      const movie: MovieEntity = toMovieEntity(parsed.value);
      let saved: MovieEntity;
      try {
        // An uploaded file takes precedence over a URL, so the URL isn't sent with it.
        if (image.file) {
          delete movie.imageUrl;
          saved = await addEntity(movie, entityWithImage(movie, image.file));
        } else {
          saved = await addEntity(movie);
        }
      } catch (error) {
        serverLogger.error('Failed to add movie', error, { operation: 'addMovie' });
        const values = submittedValues(context.formData, MOVIE_FIELDS);
        // The API rejecting the movie (4xx) is the user's to fix and carries its reason; anything else is ours.
        if (error instanceof Response && error.status >= 400 && error.status < 500) {
          return renderMovieForm(context, { values, formError: await error.text(), status: 400 });
        }
        return renderMovieForm(context, {
          values,
          formError: 'The movie could not be saved. Please try again.',
          status: 500,
        });
      }

      // The API always returns the new id; without it the movie can't be opened, so say so rather than redirect blind.
      if (!saved.id) {
        serverLogger.error('Saved movie has no id', undefined, { operation: 'addMovie' });
        return renderMovieForm(context, {
          values: submittedValues(context.formData, MOVIE_FIELDS),
          formError: 'The movie was saved, but it could not be opened. Check your Movies list before trying again.',
          status: 500,
        });
      }
      return redirect(routes.entityDetail.movies.href({ id: saved.id }), 303);
    },
  },
});
