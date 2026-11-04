import { baseUrl } from '../constants.server.ts';
import { Entity } from '../models.ts';
import { type ApiDeps, defaultApiDeps } from './api-deps.ts';

/**
 * Generic function to fetch metadata for a specific property of a media type
 */
async function getMetadata(entityType: Entity, property: string, { apiFetch, serverLogger }: ApiDeps) {
  serverLogger.info(`Fetching ${property} metadata for ${entityType}`, { entityType, property });
  try {
    const response = await apiFetch(`${baseUrl}/metadata/${entityType}/${property}`);
    if (!response.ok) {
      serverLogger.error(`Failed to fetch ${property} metadata for ${entityType}`, {
        entityType,
        property,
        status: response.status,
      });
      throw new Response(`Error fetching ${property} metadata`, { status: response.status });
    }
    const values = (await response.json()) as string[];
    serverLogger.info(`Successfully fetched ${values.length} ${property} values for ${entityType}`, {
      entityType,
      property,
      count: values.length,
    });
    return values.map((value) => ({ label: value, value: value }));
  } catch (error) {
    serverLogger.error(`Error fetching ${property} metadata for ${entityType}`, error, { entityType, property });
    throw error;
  }
}

export async function getAuthors(deps: ApiDeps = defaultApiDeps) {
  return getMetadata(Entity.Books, 'authors', deps);
}

export async function getPublishers(deps: ApiDeps = defaultApiDeps) {
  return getMetadata(Entity.Books, 'publisher', deps);
}

export async function getGenres(entityType: Entity, deps: ApiDeps = defaultApiDeps) {
  return getMetadata(entityType, 'genres', deps);
}

export async function getFormats(entityType: Entity, deps: ApiDeps = defaultApiDeps) {
  return getMetadata(entityType, 'format', deps);
}

export async function getStudios(deps: ApiDeps = defaultApiDeps) {
  return getMetadata(Entity.Movies, 'studios', deps);
}

export async function getDevelopers(deps: ApiDeps = defaultApiDeps) {
  return getMetadata(Entity.Games, 'developers', deps);
}

export async function getPlatforms(deps: ApiDeps = defaultApiDeps) {
  return getMetadata(Entity.Games, 'platform', deps);
}

export async function getLabels(deps: ApiDeps = defaultApiDeps) {
  return getMetadata(Entity.Musics, 'label', deps);
}

export async function getGamePublishers(deps: ApiDeps = defaultApiDeps) {
  return getMetadata(Entity.Games, 'publishers', deps);
}

export async function getArtist(deps: ApiDeps = defaultApiDeps) {
  return getMetadata(Entity.Musics, 'artist', deps);
}
