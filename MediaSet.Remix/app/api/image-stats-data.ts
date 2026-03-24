import { baseUrl } from '~/constants.server';
import { serverLogger } from '~/utils/serverLogger';
import { apiFetch } from '~/utils/apiFetch.server';

export type BrokenImageLink = {
  entityId: string;
  entityType: string;
  title: string;
  missingFilePath: string;
};

export type OrphanedImageFile = {
  relativePath: string;
  sizeBytes: number;
};

export type ImageLookupFailure = {
  entityId: string;
  entityType: string;
  title: string;
  lookupAttemptedAt: string;
  failureReason?: string;
  permanentFailure: boolean;
};

export type ImageStats = {
  totalFiles: number;
  totalSizeBytes: number;
  filesByEntityType: Record<string, number>;
  sizeByEntityType: Record<string, number>;
  brokenLinks: BrokenImageLink[];
  orphanedFiles: OrphanedImageFile[];
  imageLookupFailures: ImageLookupFailure[];
  lastUpdated: string;
};

export async function getImageStats(): Promise<ImageStats | null> {
  try {
    const response = await apiFetch(`${baseUrl}/stats/images`);
    if (response.status === 204) {
      serverLogger.info('No image stats available yet', {});
      return null;
    }
    if (!response.ok) {
      serverLogger.error('Failed to fetch image stats', { status: response.status });
      throw new Response('Error fetching image stats', { status: 500 });
    }
    const stats = (await response.json()) as ImageStats;
    return stats;
  } catch (error) {
    if (error instanceof Response) throw error;
    serverLogger.error('Error fetching image stats', { error: String(error) });
    throw error;
  }
}
