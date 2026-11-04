import { Entity, type IdentifierType } from '../../../models.ts';
import { getIdentifierTypeForField } from '../../../api/lookup-data.server.ts';
import { MUSIC_FIELDS, submittedMusicValues, type MusicFormValues, type TrackRowValues } from './music-form.ts';
import { resolveLookupIntent, type LookupIntentState } from '../../forms/lookup.ts';

/** The form field a lookup searches by: the title with the artist, or the barcode. */
export type MusicLookupField = 'title' | 'barcode';

export interface MusicLookupRequest {
  field: MusicLookupField;
  identifierType: IdentifierType;
  /** The search parameters, in the names the lookup API expects. */
  searchParams: Record<string, string>;
}

/** The field a `lookup:<field>` intent searches by, or `undefined` for any other intent. */
export function parseMusicLookupIntent(intent: string): MusicLookupField | undefined {
  return intent === 'lookup:title' ? 'title' : intent === 'lookup:barcode' ? 'barcode' : undefined;
}

/**
 * The lookup to run for a search field. A title search needs the artist as well; the first missing
 * field is returned as `missing` so the form can ask for it. Otherwise the request is returned.
 */
export function toMusicLookupRequest(
  field: MusicLookupField,
  values: MusicFormValues
): { request: MusicLookupRequest } | { missing: 'title' | 'artist' | 'barcode' } {
  if (field === 'barcode') {
    const barcode = values.barcode.trim();
    if (!barcode) return { missing: 'barcode' };
    const identifierType = getIdentifierTypeForField(Entity.Musics, 'barcode');
    // The lookup API reads a barcode search by its identifier type (`upc`).
    return { request: { field, identifierType, searchParams: { [identifierType]: barcode } } };
  }
  const title = values.title.trim();
  if (!title) return { missing: 'title' };
  const artist = values.artist.trim();
  if (!artist) return { missing: 'artist' };
  return { request: { field, identifierType: 'entity', searchParams: { title, artist } } };
}

/** One result as the dialog lists it: its title and a few details to tell releases apart. */
export function toMusicResultSummary(values: MusicFormValues): { title: string; details: string[]; imageUrl?: string } {
  const details = [values.artist, values.releaseDate, values.label, values.format].filter(Boolean);
  return { title: values.title, details, ...(values.imageUrl ? { imageUrl: values.imageUrl } : {}) };
}

const isText = (value: unknown): value is string => typeof value === 'string';

/** The track rows of a result (form values that travelled back from the browser); anything malformed is dropped. */
function readResultTracks(value: unknown): TrackRowValues[] {
  if (!Array.isArray(value)) return [];
  return value.flatMap((item): TrackRowValues[] => {
    if (typeof item !== 'object' || item === null) return [];
    const { trackNumber, title, minutes, seconds } = item as Record<string, unknown>;
    const text = (field: unknown) => (isText(field) ? field.trim() : '');
    return [{ trackNumber: text(trackNumber), title: text(title), minutes: text(minutes), seconds: text(seconds) }];
  });
}

/**
 * Applies a looked-up result to the form: each field the result has replaces the form's value, and
 * a field it lacks keeps what the user already entered. Only known form fields are read, and only
 * as text, because the result comes back from the browser.
 */
export function applyMusicResult(current: MusicFormValues, result: Record<string, unknown>): MusicFormValues {
  // The track list belongs to one release, so a result without one clears the rows entered or applied earlier.
  const applied: MusicFormValues = { ...current, trackList: readResultTracks(result.trackList) };
  for (const name of MUSIC_FIELDS.single) {
    const value = result[name];
    if (isText(value) && value.trim() !== '') applied[name] = value.trim();
  }
  for (const name of MUSIC_FIELDS.repeated) {
    const value = result[name];
    const list = Array.isArray(value) ? value.filter(isText).filter((item) => item.trim() !== '') : [];
    if (list.length > 0) applied[name] = list.map((item) => item.trim());
  }
  return applied;
}

/** Handles this form's lookup intents (look up, apply a result, dismiss) for both the add and edit forms. */
export function resolveMusicLookupIntent(
  formData: FormData,
  intent: string
): Promise<LookupIntentState<MusicFormValues>> {
  return resolveLookupIntent(formData, intent, {
    entityType: Entity.Musics,
    readValues: submittedMusicValues,
    parseLookupIntent: parseMusicLookupIntent,
    toRequest: toMusicLookupRequest,
    missingLabels: { title: 'a title', artist: 'an artist', barcode: 'a barcode' },
    applyResult: applyMusicResult,
  });
}
