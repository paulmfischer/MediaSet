import { get, route } from 'remix/routes';

export const routes = route({
  assets: get('/assets/*path'),
  config: get('/config.json'),
  home: get('/'),
  books: get('/books'),
  movies: get('/movies'),
  games: get('/games'),
  musics: get('/musics'),
});
