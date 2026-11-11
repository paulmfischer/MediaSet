import { createController } from 'remix/router';

import { getEntity } from '../../api/entity-data.ts';
import { clientApiUrl } from '../../constants.server.ts';
import { Entity, type BaseEntity } from '../../models.ts';
import type { AppContext } from '../../router.ts';
import { isPlexEnabledForRequest } from '../../utils/plex-nav.server.ts';
import { routes } from '../../routes.ts';
import { serverLogger } from '../../utils/serverLogger.ts';
import { Document } from '../../ui/document.tsx';
import { bookDetail, gameDetail, movieDetail, musicDetail, type DetailConfig } from '../../ui/entity/detail-config.ts';
import { DetailPage } from '../../ui/entity/detail-page.tsx';
import { renderError } from '../render-error.tsx';

async function renderDetail<T extends BaseEntity>(context: AppContext, config: DetailConfig<T>, id: string) {
  const basePath = `/${config.entity.toLowerCase()}`;
  const unavailable = (status: number, heading: string, message: string, error?: unknown) =>
    renderError(context, status, error, {
      heading,
      message,
      backHref: basePath,
      backLabel: `Back to ${config.entity}`,
    });

  let entity: T;
  try {
    entity = await getEntity<T>(config.entity, id);
  } catch (error) {
    if (error instanceof Response && error.status === 404) {
      return unavailable(
        404,
        `${config.singular} not found`,
        `We couldn't find that ${config.singular.toLowerCase()}.`
      );
    }
    serverLogger.error(`Failed to load ${config.singular.toLowerCase()} details`, error, {
      operation: 'renderDetail',
      entityType: config.entity,
      id,
    });
    return unavailable(
      500,
      `Couldn't load this ${config.singular.toLowerCase()}`,
      'Something went wrong while loading it. Please try again.',
      error
    );
  }

  // Only movies have a Plex layer; the gate is read once per page and decides whether any of it renders.
  const plexEnabled = config.entity === Entity.Movies && (await isPlexEnabledForRequest());
  const title = config.titleOf(entity) || config.singular;
  return context.render(
    <Document
      path={context.url.pathname}
      title={`${title} | MediaSet`}
      description={`${config.singular} Detail: ${title}`}
    >
      <DetailPage config={config} entity={entity} basePath={basePath} apiUrl={clientApiUrl} plexEnabled={plexEnabled} />
    </Document>
  );
}

export default createController(routes.entityDetail, {
  actions: {
    books: (context) => renderDetail(context, bookDetail, context.params.id),
    movies: (context) => renderDetail(context, movieDetail, context.params.id),
    games: (context) => renderDetail(context, gameDetail, context.params.id),
    musics: (context) => renderDetail(context, musicDetail, context.params.id),
  },
});
