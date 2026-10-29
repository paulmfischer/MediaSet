import { createController } from 'remix/router';

import { pagedSearchEntities } from '../api/entity-data.ts';
import { assets } from '../assets.ts';
import type { AppContext } from '../router.ts';
import { clientApiUrl } from '../constants.server.ts';
import { getClientConfig } from '../config.server.ts';
import { routes } from '../routes.ts';
import { serverLogger } from '../utils/serverLogger.ts';
import type { BaseEntity } from '../models.ts';
import { ListPage } from '../ui/list-page.tsx';
import { booksList, gamesList, moviesList, musicsList, type ListConfig } from '../ui/list-config.ts';
import { Document } from '../ui/document.tsx';
import { HomePage } from './home-page.tsx';
import { parseListParams } from './list-params.ts';

async function renderList<T extends BaseEntity>(context: AppContext, config: ListConfig<T>) {
  const { searchText, orderBy, page } = parseListParams(context.url);
  const result = await pagedSearchEntities<T>(config.entity, searchText, orderBy, page);
  const title = `${config.entity} List | MediaSet`;
  return context.render(
    <Document path={context.url.pathname} title={title} description={`${config.entity} List`}>
      <ListPage
        config={config}
        basePath={context.url.pathname}
        result={result}
        searchText={searchText}
        orderBy={orderBy}
        apiUrl={clientApiUrl}
      />
    </Document>
  );
}

export default createController(routes, {
  actions: {
    async assets(context) {
      return (await assets.fetch(context.request)) ?? new Response('Not Found', { status: 404 });
    },
    // Runtime configuration for the browser; read from the environment per request, not baked in.
    config() {
      serverLogger.info('Serving config.json');
      return Response.json(getClientConfig(), {
        headers: { 'Cache-Control': 'public, max-age=3600' },
      });
    },
    home(context) {
      return context.render(<HomePage path={context.url.pathname} />);
    },
    books: (context) => renderList(context, booksList),
    movies: (context) => renderList(context, moviesList),
    games: (context) => renderList(context, gamesList),
    musics: (context) => renderList(context, musicsList),
  },
});
