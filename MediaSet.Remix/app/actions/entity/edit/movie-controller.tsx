import * as s from 'remix/data-schema';
import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { getEntity, updateEntity } from '../../../api/entity-data.ts';
import { getLookupCapabilities, isBarcodeLookupAvailable } from '../../../api/lookup-capabilities-data.ts';
import { clientApiUrl } from '../../../constants.server.ts';
import { Entity, type MovieEntity } from '../../../models.ts';
import type { AppContext } from '../../../router.ts';
import { routes } from '../../../routes.ts';
import { Document } from '../../../ui/document.tsx';
import { MovieFormPage } from '../../../ui/entity/add/movie-form-page.tsx';
import { UnavailablePage } from '../../../ui/entity/unavailable-page.tsx';
import { serverLogger } from '../../../utils/serverLogger.ts';
import { entityWithImage, readCoverImage } from '../../forms/cover-image.ts';
import { fieldErrors, submittedValues } from '../../forms/form-schema.ts';
import { buildLookupProps, readEditSubmission, type LookupOutcome } from '../../forms/lookup.ts';
import { MOVIE_FIELDS, movieForm, toMovieUpdate, toMovieValues, type MovieFormValues } from '../add/movie-form.ts';
import { resolveMovieLookupIntent, toMovieResultSummary } from '../add/movie-lookup.ts';
import { loadMovieOptions } from '../movie-options.ts';

const isStatus = (error: unknown, predicate: (status: number) => boolean): error is Response =>
  error instanceof Response && predicate(error.status);

function renderUnavailable(context: AppContext, status: number, heading: string, message: string) {
  return context.render(
    <Document path={context.url.pathname} title={`${heading} | MediaSet`}>
      <UnavailablePage
        heading={heading}
        message={message}
        backHref={routes.entityLists.movies.href()}
        backLabel="Back to Movies"
      />
    </Document>,
    { status }
  );
}

async function renderEditForm(
  context: AppContext,
  id: string,
  movie: MovieEntity,
  state: {
    values?: MovieFormValues;
    errors?: Record<string, string>;
    formError?: string;
    status?: number;
    /** The outcome of a lookup, shown in the results dialog. */
    lookup?: LookupOutcome;
    /** Makes the browser take `values` over what is typed in its inputs. */
    formKey?: string;
    /** The "remove the current image" box was ticked; kept across a re-render. */
    removeImage?: boolean;
  } = {}
) {
  const { values = toMovieValues(movie), errors = {}, formError, status = 200, lookup, formKey, removeImage } = state;
  const [{ options, unavailable }, capabilities] = await Promise.all([loadMovieOptions(), getLookupCapabilities()]);
  return context.render(
    <Document
      path={context.url.pathname}
      title={`Edit ${movie.title ?? 'Movie'} | MediaSet`}
      description="Edit a movie in your collection"
    >
      <MovieFormPage
        heading="Edit Movie"
        action={routes.entityEdit.movies.action.href({ id })}
        cancelHref={routes.entityDetail.movies.href({ id })}
        values={values}
        errors={errors}
        formError={formError}
        options={options}
        optionsUnavailable={unavailable}
        formKey={formKey}
        removeImage={removeImage}
        lookup={buildLookupProps({
          available: isBarcodeLookupAvailable(capabilities, Entity.Movies),
          itemLabel: 'movie',
          lookup,
          toValues: (result) => toMovieValues(result as MovieEntity),
          toSummary: toMovieResultSummary,
        })}
        existingImageSrc={
          movie.coverImage
            ? `${clientApiUrl}/static/images/${movie.coverImage.filePath.split('/').map(encodeURIComponent).join('/')}`
            : undefined
        }
      />
    </Document>,
    { status }
  );
}

/** Loads the movie, or returns the 404/500 page to send instead. */
async function loadMovie(context: AppContext, id: string): Promise<MovieEntity | Response> {
  try {
    return await getEntity<MovieEntity>(Entity.Movies, id);
  } catch (error) {
    if (isStatus(error, (status) => status === 404)) {
      return renderUnavailable(context, 404, 'Movie not found', "We couldn't find that movie.");
    }
    serverLogger.error('Failed to load movie for editing', error, { operation: 'editMovie', id });
    return renderUnavailable(
      context,
      500,
      "Couldn't load this movie",
      'Something went wrong while loading it. Please try again.'
    );
  }
}

export default createController(routes.entityEdit.movies, {
  actions: {
    async index(context) {
      const movie = await loadMovie(context, context.params.id);
      return movie instanceof Response ? movie : renderEditForm(context, context.params.id, movie);
    },
    async action(context) {
      const { id } = context.params;
      const movie = await loadMovie(context, id);
      if (movie instanceof Response) return movie;

      // Lookup, apply and dismiss re-render the form with what the user has entered; they never save.
      const { intent, removeImage } = readEditSubmission(context.formData);
      if (intent !== 'save') {
        const state = await resolveMovieLookupIntent(context.formData, intent);
        return renderEditForm(context, id, movie, { ...state, removeImage });
      }

      const parsed = s.parseSafe(movieForm, context.formData);
      const image = readCoverImage(context.formData);
      if (!parsed.success || image.error) {
        return renderEditForm(context, id, movie, {
          values: submittedValues(context.formData, MOVIE_FIELDS),
          errors: {
            ...(image.error ? { coverImage: image.error } : {}),
            ...(parsed.success ? {} : fieldErrors(parsed.issues)),
          },
          status: 400,
          removeImage,
        });
      }

      try {
        const update = toMovieUpdate(movie, parsed.value, removeImage);
        // An uploaded file takes precedence over a URL, so the URL isn't sent with it.
        if (image.file) {
          delete update.imageUrl;
          await updateEntity(id, update, entityWithImage(update, image.file));
        } else {
          await updateEntity(id, update);
        }
      } catch (error) {
        serverLogger.error('Failed to update movie', error, { operation: 'editMovie', id });
        if (isStatus(error, (status) => status === 404)) {
          return renderUnavailable(context, 404, 'Movie not found', "We couldn't find that movie.");
        }
        const values = submittedValues(context.formData, MOVIE_FIELDS);
        // The API rejecting the movie (4xx) is the user's to fix and carries its reason; anything else is ours.
        if (isStatus(error, (status) => status >= 400 && status < 500)) {
          return renderEditForm(context, id, movie, {
            values,
            formError: await error.text(),
            status: 400,
            removeImage,
          });
        }
        return renderEditForm(context, id, movie, {
          values,
          formError: 'The movie could not be saved. Please try again.',
          status: 500,
          removeImage,
        });
      }
      return redirect(routes.entityDetail.movies.href({ id }), 303);
    },
  },
});
