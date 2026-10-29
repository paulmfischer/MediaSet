import * as s from 'remix/data-schema';
import type { Issue, Schema } from 'remix/data-schema';
import * as f from 'remix/data-schema/form-data';

/** Blank (or missing) input becomes an empty string; otherwise the trimmed text. */
export const text = (): Schema<unknown, string> => s.defaulted(s.string(), '').transform((value) => value.trim());

/** Like `text()`, but a blank value is an issue with `message`. */
export const requiredText = (message: string): Schema<unknown, string> =>
  text().refine((value) => value.length > 0, message);

/** A blank optional numeric input means "no value" (`undefined`), not `0` or `NaN`. */
export const optionalWholeNumber = (message: string): Schema<unknown, number | undefined> =>
  text()
    .transform((value) => (value === '' ? undefined : Number(value)))
    .refine((value) => value === undefined || (Number.isInteger(value) && value >= 0), message);

/** Repeated fields (multi-selects): trims each value and drops blanks and duplicates. */
export const textList = (name: string) =>
  f.fields(
    s.array(s.string()).transform((values) => [...new Set(values.map((value) => value.trim()).filter(Boolean))]),
    { name }
  );

/** The first issue message for each top-level field, keyed by field name. */
export function fieldErrors(issues: readonly Issue[]): Record<string, string> {
  const errors: Record<string, string> = {};
  for (const issue of issues) {
    const field = issue.path?.[0];
    if (typeof field === 'string' && !(field in errors)) errors[field] = issue.message;
  }
  return errors;
}

/**
 * Reads the submitted strings back out of the form data so a failed submission can re-render what
 * the user typed. `single` fields give their first text value, `repeated` fields all of them.
 * These values are only for display; saves use the schema's parsed output.
 */
export function submittedValues<Single extends string, Repeated extends string>(
  formData: FormData,
  fields: { single: readonly Single[]; repeated: readonly Repeated[] }
): Record<Single, string> & Record<Repeated, string[]> {
  const textOf = (value: FormDataEntryValue | null) => (typeof value === 'string' ? value : '');
  const values: Record<string, string | string[]> = {};
  for (const name of fields.single) values[name] = textOf(formData.get(name));
  for (const name of fields.repeated) {
    values[name] = formData.getAll(name).filter((value): value is string => typeof value === 'string');
  }
  return values as Record<Single, string> & Record<Repeated, string[]>;
}
