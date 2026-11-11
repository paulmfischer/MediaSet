import { createController } from 'remix/router';

import { getPlexStatuses } from '../../api/plex-data.ts';
import { routes } from '../../routes.ts';
import { isPlexEnabledForRequest } from '../../utils/plex-nav.server.ts';
import { isListedStatus } from '../../utils/plex-sync.ts';

const NO_STORE = { 'Cache-Control': 'no-store' };

// The status the sync page's script polls: the enabled entity types only, as the page lists them. It forwards the
// API's status so the browser never calls the API itself, and answers 404 when Plex is off, like the page.
export default createController(routes.plexStatus, {
  actions: {
    async index() {
      if (!(await isPlexEnabledForRequest())) return Response.json({}, { status: 404, headers: NO_STORE });
      try {
        const statuses = await getPlexStatuses();
        return Response.json(statuses.filter(isListedStatus), { headers: NO_STORE });
      } catch {
        return Response.json({}, { status: 502, headers: NO_STORE });
      }
    },
  },
});
