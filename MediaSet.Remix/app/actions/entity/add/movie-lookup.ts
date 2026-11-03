import { Entity, type IdentifierType } from '../../../models.ts';
import { getIdentifierTypeForField } from '../../../api/lookup-data.server.ts';
import { MOVIE_FIELDS, type MovieFormValues } from './movie-form.ts';
import { submittedValues } from '../../forms/form-schema.ts';
import { resolveLookupIntent, type LookupIntentState } from '../../forms/lookup.ts';

/** The form field a lookup searches by: the title, or the barcode. */
export type MovieLookupField = 'title' | 'barcode';

export interface MovieLookupRequest {
  field: MovieLookupField;
  identifierType: IdentifierType;
  /** The search parameters, in the names the lookup API expects. */
  searchParams: Record<string, string>;
}

/** The field a `lookup:<field>` intent searches by, or `undefined` for any other intent. */
export function parseMovieLookupIntent(intent: string): MovieLookupField | undefined {
  return intent === 'lookup:title' ? 'title' : intent === 'lookup:barcode' ? 'barcode' : undefined;
}

/** The lookup to run for a search field; `undefined` when the search field is blank. */
export function toMovieLookupRequest(field: MovieLookupField, values: MovieFormValues): MovieLookupRequest | undefined {
  const term = values[field].trim();
  if (!term) return undefined;

  const identifierType = getIdentifierTypeForField(Entity.Movies, field);
  // The lookup API reads a barcode search by its identifier type (`upc`), a title search by `title`.
  const key = field === 'barcode' ? identifierType : field;
  return { field, identifierType, searchParams: { [key]: term } };
}

/** One result as the dialog lists it: its title and a few details to tell releases apart. */
export function toMovieResultSummary(values: MovieFormValues): { title: string; details: string[]; imageUrl?: string } {
  const details = [
    values.releaseDate,
    values.runtime ? `${values.runtime} min` : '',
    values.studios.join(', '),
    values.format,
  ].filter(Boolean);
  return { title: values.title, details, ...(values.imageUrl ? { imageUrl: values.imageUrl } : {}) };
}

const isText = (value: unknown): value is string => typeof value === 'string';

/**
 * Applies a looked-up result to the form: each field the result has replaces the form's value, and
 * a field it lacks keeps what the user already entered. Only known form fields are read, and only
 * as text, because the result comes back from the browser.
 */
export function applyMovieResult(current: MovieFormValues, result: Record<string, unknown>): MovieFormValues {
  const applied: MovieFormValues = { ...current };
  for (const name of MOVIE_FIELDS.single) {
    if (name === 'isTvSeries') continue;
    const value = result[name];
    if (isText(value) && value.trim() !== '') applied[name] = value.trim();
  }
  for (const name of MOVIE_FIELDS.repeated) {
    const value = result[name];
    const list = Array.isArray(value) ? value.filter(isText).filter((item) => item.trim() !== '') : [];
    if (list.length > 0) applied[name] = list.map((item) => item.trim());
  }
  return applied;
}

/** Handles this form's lookup intents (look up, apply a result, dismiss) for both the add and edit forms. */
export function resolveMovieLookupIntent(
  formData: FormData,
  intent: string
): Promise<LookupIntentState<MovieFormValues>> {
  return resolveLookupIntent(formData, intent, {
    entityType: Entity.Movies,
    readValues: (data) => submittedValues(data, MOVIE_FIELDS),
    parseLookupIntent: parseMovieLookupIntent,
    toRequest: (field, values) => {
      const request = toMovieLookupRequest(field, values);
      return request ? { request } : { missing: field };
    },
    missingLabels: { title: 'a title', barcode: 'a barcode' },
    applyResult: applyMovieResult,
  });
}
