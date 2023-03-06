// DO NOT EDIT. This file is generated by fresh.
// This file SHOULD be checked into source version control.
// This file is automatically updated during development when running `dev.ts`.

import config from './deno.json' assert { type: 'json' };
import * as $0 from './routes/_404.tsx';
import * as $1 from './routes/_500.tsx';
import * as $2 from './routes/books/[id].tsx';
import * as $3 from './routes/books/add.tsx';
import * as $4 from './routes/books/edit/[id].tsx';
import * as $5 from './routes/books/index.tsx';
import * as $6 from './routes/index.tsx';
import * as $$0 from './islands/Counter.tsx';
import * as $$1 from './islands/DeleteBook.tsx';
import * as $$2 from './islands/MobileMenu.tsx';

const manifest = {
  routes: {
    './routes/_404.tsx': $0,
    './routes/_500.tsx': $1,
    './routes/books/[id].tsx': $2,
    './routes/books/add.tsx': $3,
    './routes/books/edit/[id].tsx': $4,
    './routes/books/index.tsx': $5,
    './routes/index.tsx': $6,
  },
  islands: {
    './islands/Counter.tsx': $$0,
    './islands/DeleteBook.tsx': $$1,
    './islands/MobileMenu.tsx': $$2,
  },
  baseUrl: import.meta.url,
  config,
};

export default manifest;
