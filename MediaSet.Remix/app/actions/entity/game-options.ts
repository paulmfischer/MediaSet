import { getDevelopers, getFormats, getGamePublishers, getGenres, getPlatforms } from '../../api/metadata-data.ts';
import { Entity, type Option } from '../../models.ts';
import type { GameFormOptions } from '../../ui/entity/add/game-form-page.tsx';
import { createServerLogger } from '../../utils/serverLogger.ts';

const serverLogger = createServerLogger('app/actions/entity/game-options');

// A failed lookup leaves a select empty rather than blocking the form: new values can still be typed.
// The page tells the user the lists are missing so they don't re-enter existing values as new ones.
export async function loadGameOptions(): Promise<{ options: GameFormOptions; unavailable: boolean }> {
  let unavailable = false;
  const orEmpty = (property: string, lookup: Promise<Option[]>): Promise<Option[]> =>
    lookup.catch((error) => {
      unavailable = true;
      serverLogger.error('Failed to load game {property} options', error, {
        operation: 'loadGameOptions',
        property,
      });
      return [];
    });
  const [developers, publishers, genres, platforms, formats] = await Promise.all([
    orEmpty('developers', getDevelopers()),
    orEmpty('publishers', getGamePublishers()),
    orEmpty('genres', getGenres(Entity.Games)),
    orEmpty('platforms', getPlatforms()),
    orEmpty('formats', getFormats(Entity.Games)),
  ]);
  return { options: { developers, publishers, genres, platforms, formats }, unavailable };
}
