import type { Handle } from 'remix/component';

import type { BookFormValues } from '../../../actions/entity/add/book-form.ts';
import type { Option } from '../../../models.ts';
import { errorId, errorProps, Field, inputClasses } from '../../forms/field.tsx';
import { LookupPanel } from '../../forms/lookup-panel.tsx';
import { LookupDialog, type LookupDialogProps } from '../../forms/public/lookup-dialog.tsx';
import { MultiSelectInput } from '../../forms/public/multi-select-input.tsx';
import { ImageInput } from '../../forms/public/image-input.tsx';
import { BarcodeScannerDialog } from '../../forms/public/barcode-scanner-dialog.tsx';
import { ScanButton } from '../../forms/public/scan-button.tsx';
import { CancelButton } from '../../forms/public/cancel-button.tsx';
import { SaveButton } from '../../forms/public/save-button.tsx';
import { SingleSelectInput } from '../../forms/public/single-select-input.tsx';

export interface BookFormOptions {
  authors: Option[];
  genres: Option[];
  publishers: Option[];
  formats: Option[];
}

interface BookFormPageProps {
  heading: string;
  action: string;
  cancelHref: string;
  /** The submit button's text, for example "Add Movie" or "Update". */
  submitLabel: string;
  values: BookFormValues;
  /** First validation message per field name. */
  errors: Record<string, string>;
  /** A failure not tied to a field, such as the API rejecting the save. */
  formError?: string | undefined;
  options: BookFormOptions;
  /** The existing authors, genres, publishers or formats could not be loaded. */
  optionsUnavailable?: boolean;
  /** Browser URL of the book's saved cover image, when editing a book that has one. */
  existingImageSrc?: string | undefined;
  /** The "remove the current image" box starts ticked (a re-render after it was ticked). */
  removeImage?: boolean | undefined;
  /** Metadata lookup: the form offers it when `available`, and shows `dialog` while results are open. */
  lookup?: { available: boolean; dialog?: LookupDialogProps | undefined } | undefined;
  /**
   * Changes when the values should replace what is in the browser's inputs. The client keeps the
   * text a user has typed when a page re-renders, so values that come from the server (an applied
   * lookup result) need a new key to make the form render fresh.
   */
  formKey?: string | undefined;
}

// A lookup sends the text fields only (urlencoded), so a chosen cover file is neither uploaded nor
// able to trip the upload size limit before the book is saved.
const lookupButtonProps = (field: 'title' | 'isbn') =>
  ({
    type: 'submit',
    name: 'intent',
    value: `lookup:${field}`,
    formnovalidate: true,
    formenctype: 'application/x-www-form-urlencoded',
  }) as const;

export function BookFormPage(handle: Handle<BookFormPageProps>) {
  return () => {
    const {
      heading,
      action,
      cancelHref,
      submitLabel,
      values,
      errors,
      formError,
      options,
      optionsUnavailable,
      existingImageSrc,
      removeImage,
      lookup,
      formKey,
    } = handle.props;
    const hasErrors = Object.keys(errors).length > 0 || Boolean(formError);

    return (
      <div class="entity-books flex flex-col gap-4 px-2 max-w-3xl mx-auto">
        <h1 class="text-2xl text-entity">{heading}</h1>
        {hasErrors ? (
          <p role="alert" class="rounded-md border border-red-500/50 bg-red-500/10 p-3 text-red-300">
            {formError ?? 'Please correct the highlighted fields.'}
          </p>
        ) : null}
        {optionsUnavailable ? (
          <p role="status" class="rounded-md border border-yellow-500/50 bg-yellow-500/10 p-3 text-yellow-200">
            Existing authors, genres, publishers and formats could not be loaded. Check before adding a value so you
            don't create a duplicate.
          </p>
        ) : null}
        <form
          method="post"
          action={action}
          enctype="multipart/form-data"
          class="flex flex-col gap-4"
          data-rmx-key={formKey}
        >
          {/* First submit button in the form, so Enter in a field saves rather than starting a lookup. */}
          <button type="submit" name="intent" value="save" class="sr-only" tabindex={-1} aria-hidden="true">
            Save
          </button>
          <LookupPanel label="Book Lookup" enabled={Boolean(lookup?.available)}>
            <Field name="title" label="Title" required error={errors.title}>
              <div class="flex gap-2">
                <input
                  id="title"
                  name="title"
                  type="text"
                  class={inputClasses}
                  placeholder="Title"
                  value={values.title}
                  {...errorProps('title', errors.title)}
                />
                {lookup?.available ? (
                  <button {...lookupButtonProps('title')} aria-label="Search by title">
                    Search
                  </button>
                ) : null}
              </div>
            </Field>
            <Field name="isbn" label="ISBN" error={errors.isbn}>
              <div class="flex gap-2">
                <input
                  id="isbn"
                  name="isbn"
                  type="text"
                  inputMode="numeric"
                  class={inputClasses}
                  placeholder="ISBN"
                  value={values.isbn}
                />
                {lookup?.available ? (
                  <button {...lookupButtonProps('isbn')} aria-label="Search by ISBN">
                    Search
                  </button>
                ) : null}
                {lookup?.available ? <ScanButton field="isbn" identifierLabel="ISBN" /> : null}
              </div>
            </Field>
          </LookupPanel>
          <ImageInput
            existingSrc={existingImageSrc}
            imageUrl={values.imageUrl}
            allowRemove={Boolean(existingImageSrc)}
            removeImage={removeImage}
            errors={{ coverImage: errors.coverImage, imageUrl: errors.imageUrl }}
          />
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
          <Field name="format" label="Format" error={errors.format}>
            <SingleSelectInput
              name="format"
              placeholder="Select Format..."
              addLabel="Add new Format:"
              options={options.formats}
              selectedValue={values.format}
              invalid={Boolean(errors.format)}
              describedBy={errors.format ? errorId('format') : undefined}
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
              invalid={Boolean(errors.authors)}
              describedBy={errors.authors ? errorId('authors') : undefined}
            />
          </Field>
          <Field name="genres" label="Genres" error={errors.genres}>
            <MultiSelectInput
              name="genres"
              placeholder="Select Genres..."
              addLabel="Add new Genre:"
              options={options.genres}
              selectedValues={values.genres}
              invalid={Boolean(errors.genres)}
              describedBy={errors.genres ? errorId('genres') : undefined}
            />
          </Field>
          <Field name="publisher" label="Publisher" error={errors.publisher}>
            <SingleSelectInput
              name="publisher"
              placeholder="Select Publisher..."
              addLabel="Add new Publisher:"
              options={options.publishers}
              selectedValue={values.publisher}
              invalid={Boolean(errors.publisher)}
              describedBy={errors.publisher ? errorId('publisher') : undefined}
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
          <div class="flex flex-row gap-2">
            <SaveButton label={submitLabel} pendingLabel={`${submitLabel}…`} />
            <CancelButton fallbackHref={cancelHref} />
          </div>
          {lookup?.dialog ? <LookupDialog {...lookup.dialog} /> : null}
          {lookup?.available ? <BarcodeScannerDialog field="isbn" identifierLabel="ISBN" /> : null}
        </form>
      </div>
    );
  };
}
