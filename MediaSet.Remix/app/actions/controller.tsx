import { createController } from 'remix/router';

import { getIntegrations } from '../api/integrations-data.ts';
import { getPlexStatistics } from '../api/plex-data.ts';
import { getStats } from '../api/stats-data.ts';
import { assets } from '../assets.ts';
import { getClientConfig } from '../config.server.ts';
import { routes } from '../routes.ts';
import type { DashboardStats } from '../ui/dashboard/stats.ts';
import { isPlexEnabledForRequest, isPlexMusicEnabledForRequest } from '../utils/plex-nav.server.ts';
import { createServerLogger } from '../utils/serverLogger.ts';
import { HomePage } from './home-page.tsx';
import { errorDetails } from './render-error.tsx';

const serverLogger = createServerLogger('app/actions/controller');

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
    async home(context) {
      // Credits never throw, so a stats failure still renders the shell and attribution.
      const integrationsRequest = getIntegrations();
      let stats: DashboardStats | null = null;
      let failure: unknown;
      try {
        stats = await getStats();
      } catch (error) {
        failure = error;
        serverLogger.error('Failed to load the dashboard', error, { operation: 'home' });
      }
      const integrations = await integrationsRequest;
      // The Plex section renders only when the request's Plex gate is on; the API already omits the statistics when
      // Plex is off, and this keeps the page invisible to Plex if the two ever disagree.
      if (stats && !(await isPlexEnabledForRequest())) stats = { ...stats, plexStats: null };
      // Music has no Plex numbers in /stats: when Plex is enabled for music they come from the Plex statistics.
      if (stats && (await isPlexMusicEnabledForRequest())) {
        stats = { ...stats, plexMusicStats: (await getPlexStatistics())?.music ?? null };
      }
      return context.render(
        <HomePage
          path={context.url.pathname}
          stats={stats}
          integrations={integrations}
          errorDetails={errorDetails(failure)}
        />,
        {
          status: stats ? 200 : 500,
        }
      );
    },
  },
});
