import * as s from 'remix/data-schema';
import * as f from 'remix/data-schema/form-data';

// Browsers submit '' for a blank optional input, and coerce.number() rejects '' — so blank must map to undefined first.
const optionalYear = s.optional(
  s
    .string()
    .transform((v) => (v.trim() === '' ? undefined : Number(v)))
    .refine((v) => v === undefined || (Number.isInteger(v) && v >= 1000), 'Year must be 4 digits')
);

// Replaces formToDto + hand-rolled checks in the Remix 2 add route.
export const itemForm = f.object({
  title: f.field(s.string().refine((v) => v.trim().length > 0, 'Title is required')),
  year: f.field(optionalYear),
  genres: f.fields(s.array(s.string())), // repeated <input name="genres"> from the multiselect
});
