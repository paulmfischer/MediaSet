import type { MetaFunction, ActionFunctionArgs, LoaderFunctionArgs } from '@remix-run/node';
import { Form, redirect, useActionData, useLoaderData, useNavigate, useNavigation } from '@remix-run/react';
import { getEntity, updateEntity } from '~/api/entity-data';
import {
  getAuthors,
  getFormats,
  getGenres,
  getPublishers,
  getStudios,
  getDevelopers,
  getLabels,
  getGamePublishers,
  getPlatforms,
} from '~/api/metadata-data';
import { formToDto, getEntityFromParams, singular } from '~/utils/helpers';
import { BookEntity, Entity, GameEntity, MovieEntity, MusicEntity } from '~/models';
import { getLookupCapabilities, isBarcodeLookupAvailable } from '~/api/lookup-capabilities-data';
import { serverLogger } from '~/utils/serverLogger';
import BookForm from '../../components/book-form';
import MovieForm from '~/components/movie-form';
import GameForm from '~/components/game-form';
import MusicForm from '~/components/music-form';
import invariant from 'tiny-invariant';

export const meta: MetaFunction<typeof loader> = ({ params }) => {
  const entityType = getEntityFromParams(params);
  return [
    { title: `Add a ${singular(entityType)}` },
    { name: 'description', content: `Add a ${singular(entityType)}` },
  ];
};

export const loader = async ({ params }: LoaderFunctionArgs) => {
  invariant(params.entity, 'Missing entity param');
  invariant(params.entityId, 'Missing entityId param');
  const entityType = getEntityFromParams(params);

  const entity = await getEntity(entityType, params.entityId);
  const [genres, formats, authors, publishers, studios, developers, labels, platforms, lookupCapabilities] =
    await Promise.all([
      getGenres(entityType),
      getFormats(entityType),
      entity.type === Entity.Books ? getAuthors() : Promise.resolve([]),
      entity.type === Entity.Books
        ? getPublishers()
        : entity.type === Entity.Games
          ? getGamePublishers()
          : Promise.resolve([]),
      entity.type === Entity.Movies ? getStudios() : Promise.resolve([]),
      entity.type === Entity.Games ? getDevelopers() : Promise.resolve([]),
      entity.type === Entity.Musics ? getLabels() : Promise.resolve([]),
      entity.type === Entity.Games ? getPlatforms() : Promise.resolve([]),
      getLookupCapabilities(),
    ]);
  const barcodeLookupAvailable = isBarcodeLookupAvailable(lookupCapabilities, entityType);
  return {
    entity,
    authors,
    genres,
    publishers,
    formats,
    entityType,
    studios,
    developers,
    labels,
    platforms,
    barcodeLookupAvailable,
  };
};

export const action = async ({ request, params }: ActionFunctionArgs) => {
  invariant(params.entity, 'Missing entity param');
  invariant(params.entityId, 'Missing entityId param');
  const entityType = getEntityFromParams(params);
  const formData = await request.formData();
  const intent = formData.get('intent') as string;

  if (intent === 'lookup') {
    const fieldName = formData.get('fieldName') as string;
    const identifierValue = formData.get('identifierValue') as string;

    if (!identifierValue) {
      serverLogger.warn('Action: Lookup failed - missing identifier value', {
        entityType,
        entityId: params.entityId,
        fieldName,
      });
      return { error: { lookup: 'Identifier value is required' } };
    }
    try {
      const { lookup, getIdentifierTypeForField } = await import('~/api/lookup-data.server');
      const identifierType = getIdentifierTypeForField(entityType, fieldName);
      const lookupResult = await lookup(entityType, identifierType, identifierValue);
      // Include lookup timestamp so UI can force remounts on consecutive lookups
      return { lookupResult, identifierValue, fieldName, lookupTimestamp: Date.now() };
    } catch (error) {
      serverLogger.error('Action: Entity lookup failed', {
        entityType,
        entityId: params.entityId,
        fieldName,
        identifierValue,
        error: String(error),
      });
      throw error;
    }
  }

  const entity = formToDto(formData);
  if (!entity) {
    serverLogger.error('Action: Failed to convert form data to entity', { entityType, entityId: params.entityId });
    return { invalidObject: `Failed to convert form to a ${entityType}` };
  }

  try {
    // Get the existing entity to preserve coverImage if no new image is selected
    const existingEntity = await getEntity(entityType, params.entityId);

    // Check if image was explicitly cleared
    const imageClearedMarker = formData.get('coverImage-cleared') as string | null;
    if (imageClearedMarker === 'true') {
      // Image was cleared, remove it
      entity.coverImage = undefined;
    } else {
      // Check if coverImage data was submitted as hidden fields
      const coverImageFileName = formData.get('coverImage-fileName') as string | null;
      if (coverImageFileName) {
        // Reconstruct coverImage from hidden inputs
        entity.coverImage = {
          fileName: coverImageFileName,
          contentType: (formData.get('coverImage-contentType') as string) || '',
          fileSize: parseInt((formData.get('coverImage-fileSize') as string) || '0'),
          filePath: (formData.get('coverImage-filePath') as string) || '',
          createdAt: (formData.get('coverImage-createdAt') as string) || '',
          updatedAt: (formData.get('coverImage-updatedAt') as string) || '',
        };
      } else if (existingEntity?.coverImage) {
        // No new image selected and not cleared, preserve existing image
        entity.coverImage = existingEntity.coverImage;
      }
    }

    // Check if there's an image file to send as multipart/form-data
    const coverImageFile = formData.get('coverImage') as File | null;
    let apiFormData: FormData | undefined;

    if (coverImageFile && coverImageFile.size > 0) {
      // Create FormData to send to the backend API with entity as JSON and image file
      // This will replace the existing coverImage
      apiFormData = new FormData();
      apiFormData.append('entity', JSON.stringify(entity));
      apiFormData.append('coverImage', coverImageFile);
    }

    await updateEntity(params.entityId, entity, apiFormData);
    return redirect(`/${entityType.toLowerCase()}/${entity.id}`);
  } catch (error) {
    serverLogger.error('Action: Failed to update entity', {
      entityType,
      entityId: params.entityId,
      error: String(error),
    });
    throw error;
  }
};

export default function Edit() {
  const {
    entity,
    authors,
    genres,
    publishers,
    formats,
    studios,
    developers,
    labels,
    platforms,
    barcodeLookupAvailable,
  } = useLoaderData<typeof loader>();
  const actionData = useActionData<typeof action>();
  const navigate = useNavigate();
  const navigation = useNavigation();
  const isSubmitting = navigation.location?.pathname === `/${entity.type.toLowerCase()}/${entity.id}/edit`;
  const formId = `edit-${singular(entity.type)}`;
  const actionUrl = `/${entity.type.toLowerCase()}/${entity.id}/edit`;
  const isLookupError = (r: unknown): r is { message: string; statusCode: number } =>
    !!r &&
    typeof (r as { message?: unknown }).message === 'string' &&
    typeof (r as { statusCode?: unknown }).statusCode === 'number';
  const lookupResult =
    actionData && 'lookupResult' in actionData ? (actionData as Record<string, unknown>).lookupResult : undefined;
  const lookupEntity = lookupResult && !isLookupError(lookupResult) ? lookupResult : undefined;
  const lookupError = lookupResult && isLookupError(lookupResult) ? lookupResult.message : undefined;
  const lookupTimestamp =
    actionData && 'lookupTimestamp' in actionData ? (actionData as Record<string, unknown>).lookupTimestamp : undefined;

  // When lookup succeeds, use lookup data and preserve only the database id and type
  // This ensures fresh lookup data isn't contaminated by stale database values
  const mergedEntity = lookupEntity ? { ...(lookupEntity as object), id: entity.id, type: entity.type } : entity;

  // Use a key to force form remount when lookup data changes
  // This ensures defaultValue props are re-applied with new lookup data
  // Include a timestamp to ensure each lookup gets a unique key, even for the same identifier
  const identifierValue =
    actionData && 'identifierValue' in actionData ? (actionData as Record<string, unknown>).identifierValue : undefined;
  const fieldName =
    actionData && 'fieldName' in actionData ? (actionData as Record<string, unknown>).fieldName : undefined;
  const formKey =
    lookupEntity && identifierValue && fieldName
      ? `lookup-${identifierValue}-${fieldName}-${lookupTimestamp ?? '0'}`
      : `entity-${entity.id}`;

  let formComponent;
  if (entity.type === Entity.Books) {
    formComponent = (
      <BookForm
        key={formKey}
        book={mergedEntity as BookEntity}
        authors={authors}
        genres={genres}
        publishers={publishers}
        formats={formats}
        isSubmitting={isSubmitting}
        isbnLookupAvailable={barcodeLookupAvailable}
      />
    );
  } else if (entity.type === Entity.Movies) {
    formComponent = (
      <MovieForm
        key={formKey}
        movie={mergedEntity as MovieEntity}
        genres={genres}
        studios={studios}
        formats={formats}
        isSubmitting={isSubmitting}
        barcodeLookupAvailable={barcodeLookupAvailable}
      />
    );
  } else if (entity.type === Entity.Games) {
    formComponent = (
      <GameForm
        key={formKey}
        game={mergedEntity as GameEntity}
        developers={developers}
        publishers={publishers}
        genres={genres}
        formats={formats}
        platforms={platforms}
        isSubmitting={isSubmitting}
        barcodeLookupAvailable={barcodeLookupAvailable}
      />
    );
  } else if (entity.type === Entity.Musics) {
    formComponent = (
      <MusicForm
        key={formKey}
        music={mergedEntity as MusicEntity}
        genres={genres}
        formats={formats}
        labels={labels}
        isSubmitting={isSubmitting}
        barcodeLookupAvailable={barcodeLookupAvailable}
      />
    );
  }

  return (
    <div className="min-h-screen flex text-white py-4">
      <div className="w-full max-w-3xl mx-auto px-2">
        <div className="flex flex-row items-center justify-between">
          <div className="flex flex-row gap-4 items-end">
            <h2 className="text-2xl">Editing {entity.title}</h2>
          </div>
        </div>
        <div className="h-full mt-4">
          <div className="mt-4 flex flex-col gap-2">
            <Form id={formId} method="post" action={actionUrl} encType="multipart/form-data">
              <input id="type" name="type" type="hidden" value={entity.type} />
              {lookupError && (
                <div className="mb-4 p-4 bg-yellow-900 border border-yellow-700 rounded-md">
                  <p className="text-yellow-300">{lookupError}</p>
                </div>
              )}
              {formComponent}
              <div className="flex flex-row gap-2 mt-3">
                <button type="submit" disabled={isSubmitting}>
                  Update
                </button>
                <button type="button" onClick={() => navigate(-1)} className="secondary" disabled={isSubmitting}>
                  Cancel
                </button>
              </div>
            </Form>
          </div>
        </div>
      </div>
    </div>
  );
}
