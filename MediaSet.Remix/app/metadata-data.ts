import { baseUrl } from "./constants.server";
import { Entity } from "./models";

export async function getAuthors() {
  const response = await fetch(`${baseUrl}/metadata/authors`);
  const authors = await response.json() as string[];
  return authors.map(author => ({ label: author, value: author }));
}

export async function getPublishers() {
  const response = await fetch(`${baseUrl}/metadata/publishers`);
  const publishers = await response.json() as string[];
  return publishers.map(publisher => ({ label: publisher, value: publisher }));
}

export async function getGenres(entityType: Entity) {
  const response = await fetch(`${baseUrl}/metadata/genres/${entityType}`);
  const genres = await response.json() as string[];
  return genres.map(genre => ({ label: genre, value: genre }));
}

export async function getFormats(entityType: Entity) {
  const response = await fetch(`${baseUrl}/metadata/formats/${entityType}`);
  const formats = await response.json() as string[];
  return formats.map(format => ({ label: format, value: format }));
}

export async function getStudios() {
  const response = await fetch(`${baseUrl}/metadata/studios`);
  const studios = await response.json() as string[];
  return studios.map(studio => ({ label: studio, value: studio }));
}

export async function getDevelopers() {
  const response = await fetch(`${baseUrl}/metadata/developers`);
  const developers = await response.json() as string[];
  return developers.map(developer => ({ label: developer, value: developer }));
}