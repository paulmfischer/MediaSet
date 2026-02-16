/**
 * Config endpoint that serves runtime environment variables to the client
 * This allows the client to access server-side environment configuration
 */

import type { LoaderFunction } from "@remix-run/node";
import { getClientConfig } from "~/config.server";
import { serverLogger } from "~/utils/serverLogger";

export const loader: LoaderFunction = () => {
  const config = getClientConfig();
  serverLogger.info("Serving config.json");
  return Response.json(config, {
    headers: {
      "Cache-Control": "public, max-age=3600", // Cache for 1 hour
    },
  });
};
