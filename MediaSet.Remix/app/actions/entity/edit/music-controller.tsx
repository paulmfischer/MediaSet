import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { getEntity, updateEntity } from '../../../api/entity-data.ts';
import { getLookupCapabilities, isLookupAvailable } from '../../../api/lookup-capabilities-data.ts';
import { clientApiUrl } from '../../../constants.server.ts';
import { Entity, type MusicEntity } from '../../../models.ts';
import type { AppContext } from '../../../router.ts';
import { routes } from '../../../routes.ts';
import { Document } from '../../../ui/document.tsx';
import { MusicFormPage } from '../../../ui/entity/add/music-form-page.tsx';
import { createServerLogger } from '../../../utils/serverLogger.ts';
import { entityWithImage, readCoverImage, withImageError } from '../../forms/cover-image.ts';
import { buildLookupProps, readEditSubmission, type LookupOutcome } from '../../forms/lookup.ts';
import { entityErrorPage } from '../../render-error.tsx';
import {
  parseMusicSubmission,
  submittedMusicValues,
  toMusicUpdate,
  toMusicValues,
  type MusicFormValues,
} from '../add/music-form.ts';
import { resolveMusicLookupIntent, toMusicResultSummary } from '../add/music-lookup.ts';
import { loadMusicOptions } from '../music-options.ts';

const serverLogger = createServerLogger('app/actions/entity/edit/music-controller');

const isStatus = (error: unknown, predicate: (status: number) => boolean): error is Response =>
  error instanceof Response && predicate(error.status);

const renderUnavailable = entityErrorPage(routes.entityLists.musics.href(), 'Back to Music');

async function renderEditForm(
  context: AppContext,
  id: string,
  album: MusicEntity,
  state: {
    values?: MusicFormValues;
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
  const { values = toMusicValues(album), errors = {}, formError, status = 200, lookup, formKey, removeImage } = state;
  const [{ options, unavailable }, capabilities] = await Promise.all([loadMusicOptions(), getLookupCapabilities()]);
  return context.render(
    <Document
      path={context.url.pathname}
      title={`Edit ${album.title ?? 'Music'} | MediaSet`}
      description="Edit an album in your collection"
    >
      <MusicFormPage
        heading="Edit Music"
        action={routes.entityEdit.musics.action.href({ id })}
        cancelHref={routes.entityDetail.musics.href({ id })}
        submitLabel="Update"
        values={values}
        errors={errors}
        formError={formError}
        options={options}
        optionsUnavailable={unavailable}
        formKey={formKey}
        removeImage={removeImage}
        lookup={buildLookupProps({
          entityType: Entity.Musics,
          available: isLookupAvailable(capabilities, Entity.Musics),
          itemLabel: 'album',
          lookup,
          toValues: toMusicValues,
          toSummary: toMusicResultSummary,
        })}
        existingImageSrc={
          album.coverImage
            ? `${clientApiUrl}/static/images/${album.coverImage.filePath.split('/').map(encodeURIComponent).join('/')}`
            : undefined
        }
      />
    </Document>,
    { status }
  );
}

/** Loads the album, or returns the 404/500 page to send instead. */
async function loadMusic(context: AppContext, id: string): Promise<MusicEntity | Response> {
  try {
    return await getEntity<MusicEntity>(Entity.Musics, id);
  } catch (error) {
    if (isStatus(error, (status) => status === 404)) {
      return renderUnavailable(context, 404, 'Album not found', "We couldn't find that album.");
    }
    serverLogger.error('Failed to load album for editing', error, { operation: 'editMusic', id });
    return renderUnavailable(
      context,
      500,
      "Couldn't load this album",
      'Something went wrong while loading it. Please try again.',
      error
    );
  }
}

export default createController(routes.entityEdit.musics, {
  actions: {
    async index(context) {
      const album = await loadMusic(context, context.params.id);
      return album instanceof Response ? album : renderEditForm(context, context.params.id, album);
    },
    async action(context) {
      const { id } = context.params;
      const album = await loadMusic(context, id);
      if (album instanceof Response) return album;

      // Lookup, apply and dismiss re-render the form with what the user has entered; they never save.
      const { intent, removeImage } = readEditSubmission(context.formData);
      if (intent !== 'save') {
        const state = await resolveMusicLookupIntent(context.formData, intent);
        return renderEditForm(context, id, album, { ...state, removeImage });
      }

      const parsed = parseMusicSubmission(context.formData);
      const image = readCoverImage(context.formData);
      if (!parsed.success || image.error) {
        return renderEditForm(context, id, album, {
          values: submittedMusicValues(context.formData),
          errors: {
            ...(image.error ? { coverImage: image.error } : {}),
            ...(parsed.success ? {} : parsed.errors),
          },
          status: 400,
          removeImage,
        });
      }

      let imageError: string | undefined;
      try {
        const update = toMusicUpdate(
          album,
          parsed.value,
          parsed.discList,
          context.formData.get('removeImage') !== null
        );
        // An uploaded file takes precedence over a URL, so the URL isn't sent with it.
        if (image.file) {
          delete update.imageUrl;
          ({ imageError } = await updateEntity(id, update, entityWithImage(update, image.file)));
        } else {
          ({ imageError } = await updateEntity(id, update));
        }
      } catch (error) {
        serverLogger.error('Failed to update album', error, { operation: 'editMusic', id });
        if (isStatus(error, (status) => status === 404)) {
          return renderUnavailable(context, 404, 'Album not found', "We couldn't find that album.");
        }
        const values = submittedMusicValues(context.formData);
        // The API rejecting the album (4xx) is the user's to fix and carries its reason; anything else is ours.
        if (isStatus(error, (status) => status >= 400 && status < 500)) {
          return renderEditForm(context, id, album, {
            values,
            formError: await error.text(),
            status: 400,
            removeImage,
          });
        }
        return renderEditForm(context, id, album, {
          values,
          formError: 'The album could not be saved. Please try again.',
          status: 500,
          removeImage,
        });
      }
      return redirect(withImageError(routes.entityDetail.musics.href({ id }), imageError), 303);
    },
  },
});
