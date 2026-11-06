import { Entity, type IdentifierType, type BookEntity } from '../../../models.ts';
import { getIdentifierTypeForField } from '../../../api/lookup-data.server.ts';
import { BOOK_FIELDS, toBookValues, type BookFormValues } from './book-form.ts';
import { submittedValues } from '../../forms/form-schema.ts';
import { resolveLookupIntent, type LookupIntentState } from '../../forms/lookup.ts';

/** The form field a lookup searches by: the title, or the ISBN. */
export type BookLookupField = 'title' | 'isbn';

export interface BookLookupRequest {
  field: BookLookupField;
  identifierType: IdentifierType;
  /** The search parameters, in the names the lookup API expects. */
  searchParams: Record<string, string>;
}

/** The field a `lookup:<field>` intent searches by, or `undefined` for any other intent. */
export function parseLookupIntent(intent: string): BookLookupField | undefined {
  return intent === 'lookup:title' ? 'title' : intent === 'lookup:isbn' ? 'isbn' : undefined;
}

/**
 * The lookup to run for a search field. A title search also sends the lookup panel's Author as an
 * extra parameter, which narrows the matches; an ISBN search sends the ISBN alone. `undefined` when the
 * search field is blank.
 */
export function toBookLookupRequest(field: BookLookupField, values: BookFormValues): BookLookupRequest | undefined {
  const term = values[field].trim();
  if (!term) return undefined;

  const searchParams: Record<string, string> = { [field]: term };
  const author = values.lookupAuthor.trim();
  if (field === 'title' && author) searchParams.author = author;
  return { field, identifierType: getIdentifierTypeForField(Entity.Books, field), searchParams };
}

/** One result as the dialog lists it: its title and a few details to tell editions apart. */
export function toBookResultSummary(values: BookFormValues): { title: string; details: string[]; imageUrl?: string } {
  const details = [
    values.authors.join(', '),
    values.publisher,
    values.publicationDate,
    values.pages ? `${values.pages} pages` : '',
    values.format,
  ].filter(Boolean);
  return {
    title: values.subtitle ? `${values.title}: ${values.subtitle}` : values.title,
    details,
    ...(values.imageUrl ? { imageUrl: values.imageUrl } : {}),
  };
}

const isText = (value: unknown): value is string => typeof value === 'string';

/**
 * Applies a looked-up result to the form: each field the result has replaces the form's value, and
 * a field it lacks keeps what the user already entered. Only known form fields are read, and only
 * as text, because the result comes back from the browser.
 */
export function applyBookResult(current: BookFormValues, result: Record<string, unknown>): BookFormValues {
  const applied: BookFormValues = { ...current };
  for (const name of BOOK_FIELDS.single) {
    const value = result[name];
    if (isText(value) && value.trim() !== '') applied[name] = value.trim();
  }
  for (const name of BOOK_FIELDS.repeated) {
    const value = result[name];
    const list = Array.isArray(value) ? value.filter(isText).filter((item) => item.trim() !== '') : [];
    if (list.length > 0) applied[name] = list.map((item) => item.trim());
  }
  return applied;
}

/** Handles this form's lookup intents (look up, apply a result, dismiss) for both the add and edit forms. */
export function resolveBookLookupIntent(
  formData: FormData,
  intent: string
): Promise<LookupIntentState<BookFormValues>> {
  return resolveLookupIntent(formData, intent, {
    entityType: Entity.Books,
    readValues: (data) => submittedValues(data, BOOK_FIELDS),
    parseLookupIntent: parseLookupIntent,
    toRequest: (field, values) => {
      const request = toBookLookupRequest(field, values);
      return request ? { request } : { missing: field };
    },
    missingLabels: { title: 'a title', isbn: 'an ISBN' },
    resultToValues: (result) => ({ ...toBookValues(result as BookEntity) }),
    applyResult: applyBookResult,
  });
}
