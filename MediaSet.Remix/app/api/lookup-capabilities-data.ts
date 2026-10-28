import { baseUrl } from '../constants.server.ts';
import { apiFetch } from '../utils/apiFetch.server.ts';
import { Entity } from '../models.ts';

export type LookupCapabilities = {
  supportsBookLookup: boolean;
  supportsMovieLookup: boolean;
  supportsGameLookup: boolean;
  supportsMusicLookup: boolean;
};

export async function getLookupCapabilities(): Promise<LookupCapabilities> {
  try {
    const response = await apiFetch(`${baseUrl}/config/lookup-capabilities`);
    if (!response.ok) {
      return {
        supportsBookLookup: false,
        supportsMovieLookup: false,
        supportsGameLookup: false,
        supportsMusicLookup: false,
      };
    }
    const data = (await response.json()) as LookupCapabilities;
    return data;
  } catch {
    return {
      supportsBookLookup: false,
      supportsMovieLookup: false,
      supportsGameLookup: false,
      supportsMusicLookup: false,
    };
  }
}

export function isBarcodeLookupAvailable(capabilities: LookupCapabilities, entityType: Entity): boolean {
  switch (entityType) {
    case Entity.Books:
      return capabilities.supportsBookLookup;
    case Entity.Movies:
      return capabilities.supportsMovieLookup;
    case Entity.Games:
      return capabilities.supportsGameLookup;
    case Entity.Musics:
      return capabilities.supportsMusicLookup;
    default:
      return false;
  }
}
