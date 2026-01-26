/**
 * Config endpoint that serves runtime environment variables to the client
 * This allows the client to access server-side environment configuration
 */

import type { LoaderFunction } from '@remix-run/node';
import { getClientConfig } from '~/config.server';

export const loader: LoaderFunction = () => {
  const config = getClientConfig();
  console.log("Serving config.json", config);
  return Response.json(config, {
    headers: {
      'Cache-Control': 'public, max-age=3600', // Cache for 1 hour
    },
  });
};
