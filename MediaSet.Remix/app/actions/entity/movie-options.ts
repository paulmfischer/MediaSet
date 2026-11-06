import { getFormats, getGenres, getStudios } from '../../api/metadata-data.ts';
import { Entity, type Option } from '../../models.ts';
import type { MovieFormOptions } from '../../ui/entity/add/movie-form-page.tsx';
import { serverLogger } from '../../utils/serverLogger.ts';

// A failed lookup leaves a select empty rather than blocking the form: new values can still be typed.
// The page tells the user the lists are missing so they don't re-enter existing values as new ones.
export async function loadMovieOptions(): Promise<{ options: MovieFormOptions; unavailable: boolean }> {
  let unavailable = false;
  const orEmpty = (property: string, lookup: Promise<Option[]>): Promise<Option[]> =>
    lookup.catch((error) => {
      unavailable = true;
      serverLogger.error(`Failed to load movie ${property} options`, error, {
        operation: 'loadMovieOptions',
        property,
      });
      return [];
    });
  const [studios, genres, formats] = await Promise.all([
    orEmpty('studios', getStudios()),
    orEmpty('genres', getGenres(Entity.Movies)),
    orEmpty('formats', getFormats(Entity.Movies)),
  ]);
  return { options: { studios, genres, formats }, unavailable };
}
