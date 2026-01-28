import { baseUrl } from "./constants.server";
import { Entity } from "./models";
import { serverLogger } from "./utils/serverLogger";
import { apiFetch } from "./utils/apiFetch.server";
import { getTraceId } from "./utils/requestContext.server";

/**
 * Generic function to fetch metadata for a specific property of a media type
 */
async function getMetadata(entityType: Entity, property: string) {
  const traceId = getTraceId();
  serverLogger.info("Fetching metadata", { entityType, property, traceId });
  try {
    const response = await apiFetch(`${baseUrl}/metadata/${entityType}/${property}`);
    if (!response.ok) {
      serverLogger.error("Failed to fetch metadata", { entityType, property, status: response.status, traceId });
      throw new Response(`Error fetching ${property} metadata`, { status: response.status });
    }
    const values = await response.json() as string[];
    serverLogger.info("Successfully fetched metadata", { entityType, property, count: values.length, traceId });
    return values.map(value => ({ label: value, value: value }));
  } catch (error) {
    serverLogger.error("Error fetching metadata", { entityType, property, error: String(error) });
    throw error;
  }
}

export async function getAuthors() {
  return getMetadata(Entity.Books, "authors");
}

export async function getPublishers() {
  return getMetadata(Entity.Books, "publisher");
}

export async function getGenres(entityType: Entity) {
  return getMetadata(entityType, "genres");
}

export async function getFormats(entityType: Entity) {
  return getMetadata(entityType, "format");
}

export async function getStudios() {
  return getMetadata(Entity.Movies, "studios");
}

export async function getDevelopers() {
  return getMetadata(Entity.Games, "developers");
}

export async function getPlatforms() {
  return getMetadata(Entity.Games, "platform");
}

export async function getLabels() {
  return getMetadata(Entity.Musics, "label");
}

export async function getGamePublishers() {
  return getMetadata(Entity.Games, "publishers");
}

export async function getArtist() {
  return getMetadata(Entity.Musics, "artist");
}