import { form, get, route } from 'remix/routes';

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
  // Item detail pages for each media type.
  entityDetail: route({
    books: get('/books/:id'),
    movies: get('/movies/:id'),
    games: get('/games/:id'),
    musics: get('/musics/:id'),
  }),
  // Add forms: each is a GET page and a POST action at one URL.
  entityAdd: route({
    books: form('/books/add'),
  }),
  // Edit forms: a GET page prefilled from the item and a POST action at one URL.
  entityEdit: route({
    books: form('/books/:id/edit'),
  }),
  // Delete confirmation pages: the no-JavaScript fallback for the detail page's confirm dialog.
  entityDelete: route({
    books: form('/books/:id/delete'),
  }),
});
