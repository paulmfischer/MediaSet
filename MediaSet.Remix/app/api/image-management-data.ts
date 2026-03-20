import { baseUrl } from '~/constants.server';
import { serverLogger } from '~/utils/serverLogger';
import { apiFetch } from '~/utils/apiFetch.server';

export async function deleteOrphanedImages(): Promise<{ deleted: number }> {
  try {
    const response = await apiFetch(`${baseUrl}/images/orphaned`, { method: 'DELETE' });
    if (!response.ok) {
      serverLogger.error('Failed to delete orphaned images', { status: response.status });
      throw new Response('Error deleting orphaned images', { status: 500 });
    }
    return (await response.json()) as { deleted: number };
  } catch (error) {
    if (error instanceof Response) throw error;
    serverLogger.error('Error deleting orphaned images', { error: String(error) });
    throw error;
  }
}
