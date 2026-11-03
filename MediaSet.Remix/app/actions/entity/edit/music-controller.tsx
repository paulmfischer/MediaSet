import * as s from 'remix/data-schema';
import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { getEntity, updateEntity } from '../../../api/entity-data.ts';
import { clientApiUrl } from '../../../constants.server.ts';
import { Entity, type MusicEntity } from '../../../models.ts';
import type { AppContext } from '../../../router.ts';
import { routes } from '../../../routes.ts';
import { Document } from '../../../ui/document.tsx';
import { MusicFormPage } from '../../../ui/entity/add/music-form-page.tsx';
import { UnavailablePage } from '../../../ui/entity/unavailable-page.tsx';
import { serverLogger } from '../../../utils/serverLogger.ts';
import { entityWithImage, readCoverImage } from '../../forms/cover-image.ts';
import { fieldErrors, submittedValues } from '../../forms/form-schema.ts';
import { MUSIC_FIELDS, musicForm, toMusicUpdate, toMusicValues, type MusicFormValues } from '../add/music-form.ts';
import { loadMusicOptions } from '../music-options.ts';

const isStatus = (error: unknown, predicate: (status: number) => boolean): error is Response =>
  error instanceof Response && predicate(error.status);

function renderUnavailable(context: AppContext, status: number, heading: string, message: string) {
  return context.render(
    <Document path={context.url.pathname} title={`${heading} | MediaSet`}>
      <UnavailablePage
        heading={heading}
        message={message}
        backHref={routes.entityLists.musics.href()}
        backLabel="Back to Music"
      />
    </Document>,
    { status }
  );
}

async function renderEditForm(
  context: AppContext,
  id: string,
  album: MusicEntity,
  state: { values?: MusicFormValues; errors?: Record<string, string>; formError?: string; status?: number } = {}
) {
  const { values = toMusicValues(album), errors = {}, formError, status = 200 } = state;
  const { options, unavailable } = await loadMusicOptions();
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
        values={values}
        errors={errors}
        formError={formError}
        options={options}
        optionsUnavailable={unavailable}
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
      'Something went wrong while loading it. Please try again.'
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

      const parsed = s.parseSafe(musicForm, context.formData);
      const image = readCoverImage(context.formData);
      if (!parsed.success || image.error) {
        return renderEditForm(context, id, album, {
          values: submittedValues(context.formData, MUSIC_FIELDS),
          errors: {
            ...(image.error ? { coverImage: image.error } : {}),
            ...(parsed.success ? {} : fieldErrors(parsed.issues)),
          },
          status: 400,
        });
      }

      try {
        const update = toMusicUpdate(album, parsed.value, context.formData.get('removeImage') !== null);
        // An uploaded file takes precedence over a URL, so the URL isn't sent with it.
        if (image.file) {
          delete update.imageUrl;
          await updateEntity(id, update, entityWithImage(update, image.file));
        } else {
          await updateEntity(id, update);
        }
      } catch (error) {
        serverLogger.error('Failed to update album', error, { operation: 'editMusic', id });
        if (isStatus(error, (status) => status === 404)) {
          return renderUnavailable(context, 404, 'Album not found', "We couldn't find that album.");
        }
        const values = submittedValues(context.formData, MUSIC_FIELDS);
        // The API rejecting the album (4xx) is the user's to fix and carries its reason; anything else is ours.
        if (isStatus(error, (status) => status >= 400 && status < 500)) {
          return renderEditForm(context, id, album, { values, formError: await error.text(), status: 400 });
        }
        return renderEditForm(context, id, album, {
          values,
          formError: 'The album could not be saved. Please try again.',
          status: 500,
        });
      }
      return redirect(routes.entityDetail.musics.href({ id }), 303);
    },
  },
});
