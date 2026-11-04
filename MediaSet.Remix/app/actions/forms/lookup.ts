import { getLookupCapabilities, isLookupAvailable } from '../../api/lookup-capabilities-data.ts';
import { isLookupError, lookup } from '../../api/lookup-data.server.ts';
import type { LookupDialogProps } from '../../ui/forms/public/lookup-dialog.tsx';
import type { BookEntity, Entity, GameEntity, IdentifierType, MovieEntity, MusicEntity } from '../../models.ts';
import { serverLogger } from '../../utils/serverLogger.ts';

/*
 * Metadata lookup is a form `intent`, not a separate page. The submit buttons `lookup:<field>`,
 * `apply:<index>` and `dismiss` re-render the form with status 200 instead of saving; the plain
 * Save button sends no intent. Things that are easy to get wrong:
 * - A hidden first Save button keeps Enter saving rather than triggering a lookup button.
 * - The results travel back in the hidden `lookupResults` field, so applying one needs no second
 *   lookup.
 * - The browser keeps typed input over a re-rendered value, so an apply render sets a new `formKey`
 *   (`data-rmx-key` on the form) to make the server's values win.
 * - The lookup buttons submit urlencoded, so a chosen cover file is not sent and is dropped by the
 *   re-render, on the add and the edit form alike; the user picks the cover after looking up. On
 *   edit the saved image is unaffected, and the remove-image checkbox is carried back as a value.
 * Another media type adds its own search-parameter and apply helpers (see `book-lookup.ts`), a
 * `resolve<Type>LookupIntent` built on `resolveLookupIntent`, and reuses `runLookup` and the
 * `LookupDialog` island. The add and edit controllers both call that resolver.
 */

type LookupEntity = BookEntity | MovieEntity | GameEntity | MusicEntity;

/** The lookup result type for one media type. */
type LookupResultOf<T extends Entity> = Extract<LookupEntity, { type: T }>;

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
  if (!isLookupAvailable(await getLookupCapabilities(), entityType)) return { status: 'unavailable' };

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

/** What a lookup intent asks the form to render: the values to show and, if any, an error or dialog. */
export interface LookupIntentState<Values> {
  values: Values;
  errors?: Record<string, string>;
  formError?: string;
  status?: number;
  /** The outcome of a lookup, shown in the results dialog. */
  lookup?: LookupOutcome;
  /** Set on an apply, so the browser takes `values` over what is typed in its inputs. */
  formKey?: string;
}

/** The type-specific parts of handling a form's lookup intents. */
export interface LookupIntentAdapter<Values, Field extends string, Missing extends string = Field> {
  entityType: Entity;
  /** The values the user has entered so far. */
  readValues: (formData: FormData) => Values;
  parseLookupIntent: (intent: string) => Field | undefined;
  /** The lookup to run, or the field that is missing. */
  toRequest: (
    field: Field,
    values: Values
  ) => { request: { identifierType: IdentifierType; searchParams: Record<string, string> } } | { missing: Missing };
  /** How a missing field reads in "Enter ... to look up". */
  missingLabels: Record<Missing, string>;
  applyResult: (current: Values, result: Record<string, unknown>) => Values;
}

/**
 * Handles the form's non-save intents: look metadata up, apply a result to the form, or close the
 * dialog. None of them saves. Add and edit forms share this, each rendering the returned state.
 */
export async function resolveLookupIntent<Values, Field extends string, Missing extends string = Field>(
  formData: FormData,
  intent: string,
  adapter: LookupIntentAdapter<Values, Field, Missing>
): Promise<LookupIntentState<Values>> {
  const values = adapter.readValues(formData);

  const field = adapter.parseLookupIntent(intent);
  if (field) {
    const outcome = adapter.toRequest(field, values);
    if ('missing' in outcome) {
      return {
        values,
        errors: { [outcome.missing]: `Enter ${adapter.missingLabels[outcome.missing]} to look up` },
        status: 400,
      };
    }
    const { identifierType, searchParams } = outcome.request;
    return { values, lookup: await runLookup(adapter.entityType, identifierType, searchParams) };
  }

  const index = parseApplyIntent(intent);
  if (index !== undefined) {
    const result = parseLookupResults(formData.get(LOOKUP_RESULTS_FIELD))[index];
    if (!result) {
      return { values, formError: 'That result is no longer available. Look it up again.', status: 400 };
    }
    return { values: adapter.applyResult(values, result), formKey: crypto.randomUUID() };
  }

  // Closing the dialog, or an intent this form doesn't know.
  return { values };
}

/**
 * The `lookup` prop of a form page: whether lookup is offered and, while an outcome is open, the
 * dialog's props. The results travel back with the apply submission as form values, so applying
 * needs no second lookup.
 */
export function buildLookupProps<T extends Entity, Values>(options: {
  /** The media type the form is for; results of any other type are ignored. */
  entityType: T;
  available: boolean;
  itemLabel: string;
  lookup: LookupOutcome | undefined;
  /** Turns a looked-up entity into form values. */
  toValues: (result: LookupResultOf<T>) => Values;
  toSummary: (values: Values) => { title: string; details: string[]; imageUrl?: string };
}): { available: boolean; dialog: LookupDialogProps | undefined } {
  const { entityType, available, itemLabel, lookup, toValues, toSummary } = options;
  if (!lookup) return { available, dialog: undefined };
  const isResult = (result: LookupEntity): result is LookupResultOf<T> => result.type === entityType;
  const resultValues = lookup.status === 'results' ? lookup.results.filter(isResult).map(toValues) : [];
  return {
    available,
    dialog: {
      itemLabel,
      status: lookup.status,
      message: lookup.status === 'error' ? lookup.message : undefined,
      results: resultValues.map(toSummary),
      resultsField: LOOKUP_RESULTS_FIELD,
      resultsValue: JSON.stringify(resultValues),
      returnFocusId: 'title',
    },
  };
}

/** The submit button's `intent` (`save` when none was sent) and whether "remove the current image" is ticked. */
export function readEditSubmission(formData: FormData): { intent: string; removeImage: boolean } {
  const intent = formData.get('intent');
  return { intent: typeof intent === 'string' ? intent : 'save', removeImage: formData.get('removeImage') !== null };
}
