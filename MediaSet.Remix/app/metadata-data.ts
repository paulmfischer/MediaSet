import { baseUrl } from "./constants";

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

export async function getGenres() {
  const response = await fetch(`${baseUrl}/metadata/genres`);
  const genres = await response.json() as string[];
  return genres.map(genre => ({ label: genre, value: genre }));
}

export async function getFormats() {
  const response = await fetch(`${baseUrl}/metadata/formats/Book`);
  const formats = await response.json() as string[];
  return formats.map(format => ({ label: format, value: format }));
}