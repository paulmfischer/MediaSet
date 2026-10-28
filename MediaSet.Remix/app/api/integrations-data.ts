import { baseUrl } from '../constants.server.ts';
import { apiFetch } from '../utils/apiFetch.server.ts';

export type Integration = {
  key: string;
  displayName: string;
  enabled: boolean;
  attributionUrl?: string | null;
  attributionText?: string | null;
  logoPath?: string | null;
};

export async function getIntegrations(): Promise<Integration[]> {
  try {
    const response = await apiFetch(`${baseUrl}/config/integrations`);
    if (!response.ok) return [];
    const data = (await response.json()) as Integration[];
    return data;
  } catch {
    return [];
  }
}
