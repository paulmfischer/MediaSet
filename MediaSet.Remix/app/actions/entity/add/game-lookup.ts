import { Entity, type IdentifierType } from '../../../models.ts';
import { getIdentifierTypeForField } from '../../../api/lookup-data.server.ts';
import { GAME_FIELDS, type GameFormValues } from './game-form.ts';

/** The form field a lookup searches by: the title, or the barcode. */
export type GameLookupField = 'title' | 'barcode';

export interface GameLookupRequest {
  field: GameLookupField;
  identifierType: IdentifierType;
  /** The search parameters, in the names the lookup API expects. */
  searchParams: Record<string, string>;
}

/** The field a `lookup:<field>` intent searches by, or `undefined` for any other intent. */
export function parseGameLookupIntent(intent: string): GameLookupField | undefined {
  return intent === 'lookup:title' ? 'title' : intent === 'lookup:barcode' ? 'barcode' : undefined;
}

/** The lookup to run for a search field; `undefined` when the search field is blank. */
export function toGameLookupRequest(field: GameLookupField, values: GameFormValues): GameLookupRequest | undefined {
  const term = values[field].trim();
  if (!term) return undefined;

  const identifierType = getIdentifierTypeForField(Entity.Games, field);
  // The lookup API reads a barcode search by its identifier type (`upc`), a title search by `title`.
  const key = field === 'barcode' ? identifierType : field;
  return { field, identifierType, searchParams: { [key]: term } };
}

/** One result as the dialog lists it: its title and a few details to tell releases apart. */
export function toGameResultSummary(values: GameFormValues): { title: string; details: string[]; imageUrl?: string } {
  const details = [values.releaseDate, values.platform, values.developers.join(', '), values.format].filter(Boolean);
  return { title: values.title, details, ...(values.imageUrl ? { imageUrl: values.imageUrl } : {}) };
}

const isText = (value: unknown): value is string => typeof value === 'string';

/**
 * Applies a looked-up result to the form: each field the result has replaces the form's value, and
 * a field it lacks keeps what the user already entered. Only known form fields are read, and only
 * as text, because the result comes back from the browser.
 */
export function applyGameResult(current: GameFormValues, result: Record<string, unknown>): GameFormValues {
  const applied: GameFormValues = { ...current };
  for (const name of GAME_FIELDS.single) {
    const value = result[name];
    if (isText(value) && value.trim() !== '') applied[name] = value.trim();
  }
  for (const name of GAME_FIELDS.repeated) {
    const value = result[name];
    const list = Array.isArray(value) ? value.filter(isText).filter((item) => item.trim() !== '') : [];
    if (list.length > 0) applied[name] = list.map((item) => item.trim());
  }
  return applied;
}
