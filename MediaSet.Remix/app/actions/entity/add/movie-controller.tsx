import * as s from 'remix/data-schema';
import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { addEntity } from '../../../api/entity-data.ts';
import { getLookupCapabilities, isLookupAvailable } from '../../../api/lookup-capabilities-data.ts';
import { Entity, type MovieEntity } from '../../../models.ts';
import type { AppContext } from '../../../router.ts';
import { routes } from '../../../routes.ts';
import { Document } from '../../../ui/document.tsx';
import { MovieFormPage } from '../../../ui/entity/add/movie-form-page.tsx';
import { serverLogger } from '../../../utils/serverLogger.ts';
import { readCoverImage, entityWithImage } from '../../forms/cover-image.ts';
import { fieldErrors, submittedValues } from '../../forms/form-schema.ts';
import { buildLookupProps, type LookupOutcome } from '../../forms/lookup.ts';
import { loadMovieOptions } from '../movie-options.ts';
import {
  MOVIE_FIELDS,
  movieForm,
  emptyMovieValues,
  toMovieEntity,
  toMovieValues,
  type MovieFormValues,
} from './movie-form.ts';
import { resolveMovieLookupIntent, toMovieResultSummary } from './movie-lookup.ts';

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
  return context.render(
    <Document path={context.url.pathname} title="Add Movie | MediaSet" description="Add a movie to your collection">
      <MovieFormPage
        heading="Add Movie"
        action={routes.entityAdd.movies.action.href()}
        cancelHref={routes.entityLists.movies.href()}
        submitLabel="Add Movie"
        values={values}
        errors={errors}
        formError={formError}
        options={options}
        optionsUnavailable={unavailable}
        formKey={formKey}
        lookup={buildLookupProps({
          entityType: Entity.Movies,
          available: isLookupAvailable(capabilities, Entity.Movies),
          itemLabel: 'movie',
          lookup,
          toValues: toMovieValues,
          toSummary: toMovieResultSummary,
        })}
      />
    </Document>,
    { status }
  );
}

// The form's other submit buttons: look metadata up, apply a result to the form, or close the dialog.
// None of them saves; each re-renders the form with what the user has entered so far.
function handleLookupIntent(context: AppContext, intent: string) {
  return resolveMovieLookupIntent(context.formData, intent).then((state) => renderMovieForm(context, state));
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
