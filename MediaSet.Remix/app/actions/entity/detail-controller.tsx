import { createController } from 'remix/router';

import { getEntity } from '../../api/entity-data.ts';
import { getIntegrations } from '../../api/integrations-data.ts';
import { clientApiUrl } from '../../constants.server.ts';
import type { BaseEntity } from '../../models.ts';
import type { AppContext } from '../../router.ts';
import { routes } from '../../routes.ts';
import { serverLogger } from '../../utils/serverLogger.ts';
import { Document } from '../../ui/document.tsx';
import { bookDetail, gameDetail, movieDetail, musicDetail, type DetailConfig } from '../../ui/entity/detail-config.ts';
import { DetailPage } from '../../ui/entity/detail-page.tsx';
import { UnavailablePage } from '../../ui/entity/unavailable-page.tsx';

async function renderDetail<T extends BaseEntity>(context: AppContext, config: DetailConfig<T>, id: string) {
  const basePath = `/${config.entity.toLowerCase()}`;
  const unavailable = (status: number, heading: string, message: string) =>
    context.render(
      <Document path={context.url.pathname} title={`${heading} | MediaSet`}>
        <UnavailablePage
          heading={heading}
          message={message}
          backHref={basePath}
          backLabel={`Back to ${config.entity}`}
        />
      </Document>,
      { status }
    );

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
      'Something went wrong while loading it. Please try again.'
    );
  }

  const title = config.titleOf(entity) || config.singular;
  const integrations = await getIntegrations();
  return context.render(
    <Document
      path={context.url.pathname}
      title={`${title} | MediaSet`}
      description={`${config.singular} Detail: ${title}`}
    >
      <DetailPage
        config={config}
        entity={entity}
        basePath={basePath}
        apiUrl={clientApiUrl}
        integrations={integrations}
      />
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
