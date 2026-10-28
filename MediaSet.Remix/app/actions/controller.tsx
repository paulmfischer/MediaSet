import { createController } from 'remix/router';

import { assets } from '../assets.ts';
import { getClientConfig } from '../config.server.ts';
import { routes } from '../routes.ts';
import { serverLogger } from '../utils/serverLogger.ts';
import { HomePage } from './home-page.tsx';

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
  },
});
