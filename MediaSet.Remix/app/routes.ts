import { get, route } from 'remix/routes';

export const routes = route({
  assets: get('/assets/*path'),
  config: get('/config.json'),
  home: get('/'),
  // Collection list pages for each media type.
  entityLists: route({
    books: get('/books'),
    movies: get('/movies'),
    games: get('/games'),
    musics: get('/musics'),
  }),
});
