import { baseUrl } from "./constants";

export async function getAuthors() {
  const response = await fetch(`${baseUrl}/metadata/authors`);
  const authors = await response.json() as string[];
  return authors.map(author => ({ label: author, value: author }));
}