/**
 * Client-side hook to access runtime configuration
 * Fetches config from /config.json endpoint
 */

import { useEffect, useState } from 'react';
import type { ClientConfig } from '~/config.server';

let configCache: ClientConfig | null = null;
let configPromise: Promise<ClientConfig> | null = null;

async function fetchConfig(): Promise<ClientConfig> {
  // Return cached config if available
  if (configCache) {
    return configCache;
  }

  // Return existing promise if already fetching
  if (configPromise) {
    return configPromise;
  }

  // Fetch config
  console.log("Fetching runtime config from /config.json");
  configPromise = fetch('/config.json')
    .then((res) => {
      console.log("Config fetch response", { status: res.status });
      if (!res.ok) {
        throw new Error(`Failed to fetch config: ${res.statusText}`);
      }
      return res.json();
    })
    .then((config) => {
      configCache = config;
      return config;
    })
    .catch((error) => {
      console.error('Error fetching config:', error);
      // Return defaults on error
      return {
        apiUrl: 'http://localhost:7130',
        showErrorDetails: false,
      };
    })
    .finally(() => {
      configPromise = null;
    });

  return configPromise;
}

/**
 * Hook to access runtime configuration
 * Can be used in any client-side component
 */
export function useConfig() {
  const [config, setConfig] = useState<ClientConfig | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    fetchConfig()
      .then((cfg) => {
        setConfig(cfg);
        setIsLoading(false);
      })
      .catch((err) => {
        setError(err);
        setIsLoading(false);
      });
  }, []);

  return { config, isLoading, error };
}
