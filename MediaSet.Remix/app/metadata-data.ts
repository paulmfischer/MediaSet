import { baseUrl } from "./constants.server";
import { Entity } from "./models";

/**
 * Generic function to fetch metadata for a specific property of a media type
 */
async function getMetadata(entityType: Entity, property: string) {
  const response = await fetch(`${baseUrl}/metadata/${entityType}/${property}`);
  const values = await response.json() as string[];
  return values.map(value => ({ label: value, value: value }));
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