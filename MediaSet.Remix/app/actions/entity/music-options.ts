import { getArtist, getFormats, getGenres, getLabels } from '../../api/metadata-data.ts';
import { Entity, type Option } from '../../models.ts';
import type { MusicFormOptions } from '../../ui/entity/add/music-form-page.tsx';
import { serverLogger } from '../../utils/serverLogger.ts';

// A failed lookup leaves a select empty rather than blocking the form: new values can still be typed.
// The page tells the user the lists are missing so they don't re-enter existing values as new ones.
export async function loadMusicOptions(): Promise<{ options: MusicFormOptions; unavailable: boolean }> {
  let unavailable = false;
  const orEmpty = (property: string, lookup: Promise<Option[]>): Promise<Option[]> =>
    lookup.catch((error) => {
      unavailable = true;
      serverLogger.error(`Failed to load music ${property} options`, error, {
        operation: 'loadMusicOptions',
        property,
      });
      return [];
    });
  const [artists, labels, genres, formats] = await Promise.all([
    orEmpty('artists', getArtist()),
    orEmpty('labels', getLabels()),
    orEmpty('genres', getGenres(Entity.Musics)),
    orEmpty('formats', getFormats(Entity.Musics)),
  ]);
  return { options: { artists, labels, genres, formats }, unavailable };
}
