import { getLookupCapabilities, isBarcodeLookupAvailable } from '../../api/lookup-capabilities-data.ts';
import { isLookupError, lookup } from '../../api/lookup-data.server.ts';
import type { BookEntity, Entity, GameEntity, IdentifierType, MovieEntity, MusicEntity } from '../../models.ts';
import { serverLogger } from '../../utils/serverLogger.ts';

type LookupEntity = BookEntity | MovieEntity | GameEntity | MusicEntity;

/** What a metadata lookup came back with; the results dialog shows one of these. */
export type LookupOutcome =
  | { status: 'results'; results: LookupEntity[] }
  | { status: 'notFound' }
  | { status: 'unavailable' }
  | { status: 'error'; message: string };

/**
 * Looks metadata up for a media type, honoring the provider capability checks. Failures are
 * returned as an outcome rather than thrown, so the form can show them in the dialog.
 */
export async function runLookup(
  entityType: Entity,
  identifierType: IdentifierType,
  searchParams: Record<string, string>
): Promise<LookupOutcome> {
  if (!isBarcodeLookupAvailable(await getLookupCapabilities(), entityType)) return { status: 'unavailable' };

  try {
    const result = await lookup(entityType, identifierType, searchParams);
    if (isLookupError(result)) {
      if (result.statusCode === 404) return { status: 'notFound' };
      serverLogger.error('Metadata lookup failed', undefined, { operation: 'lookup', statusCode: result.statusCode });
      // A 4xx carries the API's reason (for example an unsupported search); anything else is ours.
      const message =
        result.statusCode < 500 && result.message ? result.message : 'The lookup failed. Please try again.';
      return { status: 'error', message };
    }
    return result.length === 0 ? { status: 'notFound' } : { status: 'results', results: result };
  } catch (error) {
    serverLogger.error('Metadata lookup failed', error, { operation: 'lookup' });
    return { status: 'error', message: 'The lookup could not be reached. Please try again.' };
  }
}

/** The hidden field that carries the shown results (as form values) back with the apply submission. */
export const LOOKUP_RESULTS_FIELD = 'lookupResults';

/** The index in an `apply:<n>` intent, the value of a result's submit button in the dialog, or `undefined` for any other intent. */
export function parseApplyIntent(intent: string): number | undefined {
  const match = /^apply:(\d{1,3})$/.exec(intent);
  return match ? Number(match[1]) : undefined;
}

/** Reads the results back from the hidden field; anything malformed is dropped. */
export function parseLookupResults(raw: FormDataEntryValue | null): Record<string, unknown>[] {
  if (typeof raw !== 'string') return [];
  try {
    const parsed: unknown = JSON.parse(raw);
    if (!Array.isArray(parsed)) return [];
    return parsed.filter(
      (item): item is Record<string, unknown> => typeof item === 'object' && item !== null && !Array.isArray(item)
    );
  } catch {
    return [];
  }
}
