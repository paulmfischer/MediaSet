import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { deleteOrphanedImages, resetImageLookup } from '../../api/image-management-data.ts';
import { getImageStats, type ImageStats } from '../../api/image-stats-data.ts';
import type { AppContext } from '../../router.ts';
import { routes } from '../../routes.ts';
import { Document } from '../../ui/document.tsx';
import { ENTITY_ENUMS, isEntityType } from '../../ui/image-stats/format.ts';
import { ImageStatsPage } from '../../ui/image-stats/image-stats-page.tsx';
import { renderError } from '../render-error.tsx';
import { serverLogger } from '../../utils/serverLogger.ts';

const TITLE = 'Image Statistics | MediaSet';
const DESCRIPTION = 'Statistics about stored cover images';

/** Parses the JSON array of entity ids a reset form submits; null when it is not a string array. */
export function parseEntityIds(raw: unknown): string[] | null {
  if (typeof raw !== 'string') return null;
  try {
    const parsed: unknown = JSON.parse(raw);
    if (Array.isArray(parsed) && parsed.every((id): id is string => typeof id === 'string')) return parsed;
  } catch {
    // Falls through to null.
  }
  return null;
}

function renderPage(context: AppContext, stats: ImageStats | null, error?: string, status = 200, tab?: string) {
  return context.render(
    <Document path={context.url.pathname} title={TITLE} description={DESCRIPTION}>
      <ImageStatsPage stats={stats} error={error} tab={tab} />
    </Document>,
    { status }
  );
}

function renderUnavailable(context: AppContext, error?: unknown) {
  return renderError(context, 500, error, {
    heading: "Couldn't load image statistics",
    message: 'Something went wrong while loading them. Please try again.',
    title: 'Image Statistics',
    backHref: routes.home.href(),
    backLabel: 'Back to Home',
  });
}

async function renderFormError(context: AppContext, message: string) {
  let stats: ImageStats | null = null;
  try {
    stats = await getImageStats();
  } catch (error) {
    serverLogger.error('Failed to load image statistics', error, { operation: 'imageStats' });
    return renderUnavailable(context, error);
  }
  return renderPage(context, stats, message, 400);
}

function renderUnavailableNotFound(context: AppContext) {
  return renderError(context, 404, undefined, {
    heading: 'Unknown media type',
    message: "That media type isn't supported.",
    title: 'Image Statistics',
    backHref: routes.imageStats.index.href(),
    backLabel: 'Back to Image Statistics',
  });
}

export default createController(routes.imageStats, {
  actions: {
    async index(context) {
      let stats: ImageStats | null;
      try {
        stats = await getImageStats();
      } catch (error) {
        serverLogger.error('Failed to load image statistics', error, { operation: 'imageStats' });
        return renderUnavailable(context, error);
      }
      return renderPage(context, stats, undefined, 200, context.url.searchParams.get('tab') ?? undefined);
    },

    async action(context) {
      const form = context.formData;
      const intent = form.get('intent');
      const tab = form.get('tab');
      const back = redirect(
        isEntityType(tab) ? `${routes.imageStats.index.href()}?tab=${tab}` : routes.imageStats.index.href(),
        303
      );

      try {
        if (intent === 'delete-orphaned') {
          await deleteOrphanedImages();
        } else if (intent === 'reset-lookup') {
          const entityType = form.get('entityType');
          if (!isEntityType(entityType)) return renderUnavailableNotFound(context);
          const entity = ENTITY_ENUMS[entityType];
          const ids = parseEntityIds(form.get('entityIds'));
          if (!ids) return renderFormError(context, 'Invalid entity IDs format.');
          await resetImageLookup(ids, entity);
        } else {
          return renderFormError(context, 'Unknown action.');
        }
      } catch (error) {
        serverLogger.error('Image statistics action failed', error, { operation: 'imageStatsAction' });
        return renderUnavailable(context, error);
      }
      return back;
    },
  },
});
