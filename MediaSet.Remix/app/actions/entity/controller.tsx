import { createController } from 'remix/router';
import { redirect } from 'remix/response/redirect';

import { pagedSearchEntities } from '../../api/entity-data.ts';
import { clientApiUrl } from '../../constants.server.ts';
import { Entity, type BaseEntity } from '../../models.ts';
import type { AppContext } from '../../router.ts';
import { routes } from '../../routes.ts';
import { isPlexEnabledForRequest } from '../../utils/plex-nav.server.ts';
import { Document } from '../../ui/document.tsx';
import { booksList, gamesList, moviesList, musicsList, type ListConfig } from '../../ui/entity/list-config.ts';
import { ListPage } from '../../ui/entity/list-page.tsx';
import { listHref, parseListParams } from './list-params.ts';

async function renderList<T extends BaseEntity>(context: AppContext, config: ListConfig<T>) {
  const params = parseListParams(context.url);
  const { searchText, orderBy, page } = params;
  // Only movies have a Plex layer; the request-scoped gate decides whether the filter and icons exist at all.
  const plexEnabled = config.entity === Entity.Movies && (await isPlexEnabledForRequest());
  const plex = plexEnabled ? params.plex : null;
  const result = await pagedSearchEntities<T>(config.entity, searchText, orderBy, page, undefined, plex);
  // A stale link past the last page goes to the last page rather than showing an empty list.
  if (result.items.length === 0 && result.totalCount > 0 && page > result.totalPages) {
    return redirect(listHref(context.url.pathname, { searchText, orderBy, page: result.totalPages, plex }));
  }
  const title = `${config.entity} List | MediaSet`;
  return context.render(
    <Document path={context.url.pathname} title={title} description={`${config.entity} List`}>
      <ListPage
        config={config}
        basePath={context.url.pathname}
        result={result}
        searchText={searchText}
        orderBy={orderBy}
        plexEnabled={plexEnabled}
        plex={plex}
        apiUrl={clientApiUrl}
      />
    </Document>
  );
}

export default createController(routes.entityLists, {
  actions: {
    books: (context) => renderList(context, booksList),
    movies: (context) => renderList(context, moviesList),
    games: (context) => renderList(context, gamesList),
    musics: (context) => renderList(context, musicsList),
  },
});
