import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { resetImageLookup } from '../../api/image-management-data.ts';
import { Entity } from '../../models.ts';
import type { AppContext } from '../../router.ts';
import { routes } from '../../routes.ts';
import { entityErrorPage } from '../render-error.tsx';

// Resets the item's failed image lookup so it is retried, then returns to its detail page.
async function clearImageLookup(
  context: AppContext,
  entity: Entity,
  singular: string,
  detailHref: (id: string) => string
) {
  const { id } = context.params as { id: string };
  const unavailable = entityErrorPage(detailHref(id), `Back to ${singular}`);
  try {
    await resetImageLookup([id], entity);
  } catch (error) {
    return unavailable(
      context,
      500,
      "Couldn't retry the image lookup",
      'Something went wrong while clearing the lookup. Please try again.',
      error
    );
  }
  return redirect(detailHref(id), 303);
}

export default createController(routes.entityClearImageLookup, {
  actions: {
    books: (context) => clearImageLookup(context, Entity.Books, 'Book', (id) => routes.entityDetail.books.href({ id })),
    movies: (context) =>
      clearImageLookup(context, Entity.Movies, 'Movie', (id) => routes.entityDetail.movies.href({ id })),
    games: (context) => clearImageLookup(context, Entity.Games, 'Game', (id) => routes.entityDetail.games.href({ id })),
    musics: (context) =>
      clearImageLookup(context, Entity.Musics, 'Music', (id) => routes.entityDetail.musics.href({ id })),
  },
});
