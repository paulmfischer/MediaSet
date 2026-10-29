import type { Handle } from 'remix/component';

import type { BookFormValues } from '../../../actions/entity/add/book-form.ts';
import type { Option } from '../../../models.ts';
import { errorProps, Field, inputClasses } from '../../forms/field.tsx';
import { MultiSelectInput } from '../../forms/public/multi-select-input.tsx';
import { SaveButton } from '../../forms/public/save-button.tsx';
import { SingleSelectInput } from '../../forms/public/single-select-input.tsx';

export interface BookFormOptions {
  authors: Option[];
  genres: Option[];
  publishers: Option[];
  formats: Option[];
}

interface BookFormPageProps {
  action: string;
  cancelHref: string;
  values: BookFormValues;
  /** First validation message per field name. */
  errors: Record<string, string>;
  /** A failure not tied to a field, such as the API rejecting the save. */
  formError?: string | undefined;
  options: BookFormOptions;
}

export function BookFormPage(handle: Handle<BookFormPageProps>) {
  return () => {
    const { action, cancelHref, values, errors, formError, options } = handle.props;
    const hasErrors = Object.keys(errors).length > 0 || Boolean(formError);

    return (
      <div class="entity-books flex flex-col gap-4 px-2 max-w-3xl mx-auto">
        <h1 class="text-2xl text-entity">Add Book</h1>
        {hasErrors ? (
          <p role="alert" class="rounded-md border border-red-500/50 bg-red-500/10 p-3 text-red-300">
            {formError ?? 'Please correct the highlighted fields.'}
          </p>
        ) : null}
        <form method="post" action={action} class="flex flex-col gap-4">
          <Field name="title" label="Title" required error={errors.title}>
            <input
              id="title"
              name="title"
              type="text"
              class={inputClasses}
              placeholder="Title"
              value={values.title}
              {...errorProps('title', errors.title)}
            />
          </Field>
          <Field name="subtitle" label="Subtitle" error={errors.subtitle}>
            <input
              id="subtitle"
              name="subtitle"
              type="text"
              class={inputClasses}
              placeholder="Subtitle"
              value={values.subtitle}
            />
          </Field>
          <Field name="isbn" label="ISBN" error={errors.isbn}>
            <input
              id="isbn"
              name="isbn"
              type="text"
              inputMode="numeric"
              class={inputClasses}
              placeholder="ISBN"
              value={values.isbn}
            />
          </Field>
          <Field name="format" label="Format" error={errors.format}>
            <SingleSelectInput
              name="format"
              placeholder="Select Format..."
              addLabel="Add new Format:"
              options={options.formats}
              selectedValue={values.format}
            />
          </Field>
          <Field name="pages" label="Pages" error={errors.pages}>
            <input
              id="pages"
              name="pages"
              type="number"
              min="0"
              step="1"
              class={inputClasses}
              placeholder="Pages"
              value={values.pages}
              {...errorProps('pages', errors.pages)}
            />
          </Field>
          <Field name="publicationDate" label="Publication Date" error={errors.publicationDate}>
            <input
              id="publicationDate"
              name="publicationDate"
              type="text"
              class={inputClasses}
              placeholder="Publication Date"
              value={values.publicationDate}
            />
          </Field>
          <Field name="authors" label="Authors" error={errors.authors}>
            <MultiSelectInput
              name="authors"
              placeholder="Select Authors..."
              addLabel="Add new Author:"
              options={options.authors}
              selectedValues={values.authors}
            />
          </Field>
          <Field name="genres" label="Genres" error={errors.genres}>
            <MultiSelectInput
              name="genres"
              placeholder="Select Genres..."
              addLabel="Add new Genre:"
              options={options.genres}
              selectedValues={values.genres}
            />
          </Field>
          <Field name="publisher" label="Publisher" error={errors.publisher}>
            <SingleSelectInput
              name="publisher"
              placeholder="Select Publisher..."
              addLabel="Add new Publisher:"
              options={options.publishers}
              selectedValue={values.publisher}
            />
          </Field>
          <Field name="plot" label="Plot" error={errors.plot}>
            <textarea
              id="plot"
              name="plot"
              class={`${inputClasses} min-h-[100px]`}
              placeholder="Plot"
              value={values.plot}
            />
          </Field>
          <div class="flex gap-2 justify-end">
            <a href={cancelHref} class="px-3 py-1">
              Cancel
            </a>
            <SaveButton label="Save" pendingLabel="Saving…" />
          </div>
        </form>
      </div>
    );
  };
}
