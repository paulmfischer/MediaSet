import { getAuthors, getFormats, getGenres, getPublishers } from '../../api/metadata-data.ts';
import { Entity, type Option } from '../../models.ts';
import type { BookFormOptions } from '../../ui/entity/add/book-form-page.tsx';
import { serverLogger } from '../../utils/serverLogger.ts';

// A failed lookup leaves a select empty rather than blocking the form: new values can still be typed.
// The page tells the user the lists are missing so they don't re-enter existing values as new ones.
export async function loadBookOptions(): Promise<{ options: BookFormOptions; unavailable: boolean }> {
  let unavailable = false;
  const orEmpty = (property: string, lookup: Promise<Option[]>): Promise<Option[]> =>
    lookup.catch((error) => {
      unavailable = true;
      serverLogger.error(`Failed to load book ${property} options`, error, { operation: 'loadBookOptions', property });
      return [];
    });
  const [authors, genres, publishers, formats] = await Promise.all([
    orEmpty('authors', getAuthors()),
    orEmpty('genres', getGenres(Entity.Books)),
    orEmpty('publishers', getPublishers()),
    orEmpty('formats', getFormats(Entity.Books)),
  ]);
  return { options: { authors, genres, publishers, formats }, unavailable };
}
