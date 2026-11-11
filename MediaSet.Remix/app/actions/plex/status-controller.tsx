import { createController } from 'remix/router';

import { getPlexStatuses, isPlexEnabledForMovies } from '../../api/plex-data.ts';
import { routes } from '../../routes.ts';

const NO_STORE = { 'Cache-Control': 'no-store' };

// The status the sync page's script polls. It forwards the API's per-type status so the browser never calls the API
// itself, and answers 404 when Plex is off, like the page.
export default createController(routes.plexStatus, {
  actions: {
    async index() {
      if (!(await isPlexEnabledForMovies())) return Response.json({}, { status: 404, headers: NO_STORE });
      try {
        return Response.json(await getPlexStatuses(), { headers: NO_STORE });
      } catch {
        return Response.json({}, { status: 502, headers: NO_STORE });
      }
    },
  },
});
