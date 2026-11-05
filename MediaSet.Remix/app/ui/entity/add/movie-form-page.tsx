import type { Handle } from 'remix/component';

import type { MovieFormValues } from '../../../actions/entity/add/movie-form.ts';
import type { Option } from '../../../models.ts';
import { errorId, errorProps, Field, inputClasses } from '../../forms/field.tsx';
import { LookupPanel } from '../../forms/lookup-panel.tsx';
import { LookupDialog, type LookupDialogProps } from '../../forms/public/lookup-dialog.tsx';
import { MultiSelectInput } from '../../forms/public/multi-select-input.tsx';
import { ImageInput } from '../../forms/public/image-input.tsx';
import { BarcodeScannerDialog } from '../../forms/public/barcode-scanner-dialog.tsx';
import { ScanButton } from '../../forms/public/scan-button.tsx';
import { SaveButton } from '../../forms/public/save-button.tsx';
import { SingleSelectInput } from '../../forms/public/single-select-input.tsx';

export interface MovieFormOptions {
  studios: Option[];
  genres: Option[];
  formats: Option[];
}

interface MovieFormPageProps {
  heading: string;
  action: string;
  cancelHref: string;
  values: MovieFormValues;
  /** First validation message per field name. */
  errors: Record<string, string>;
  /** A failure not tied to a field, such as the API rejecting the save. */
  formError?: string | undefined;
  options: MovieFormOptions;
  /** The existing studios, genres or formats could not be loaded. */
  optionsUnavailable?: boolean;
  /** Browser URL of the movie's saved poster, when editing a movie that has one. */
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
// able to trip the upload size limit before the movie is saved.
const lookupButtonProps = (field: 'title' | 'barcode') =>
  ({
    type: 'submit',
    name: 'intent',
    value: `lookup:${field}`,
    formnovalidate: true,
    formenctype: 'application/x-www-form-urlencoded',
  }) as const;

export function MovieFormPage(handle: Handle<MovieFormPageProps>) {
  return () => {
    const {
      heading,
      action,
      cancelHref,
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
      <div class="entity-movies flex flex-col gap-4 px-2 max-w-3xl mx-auto">
        <h1 class="text-2xl text-entity">{heading}</h1>
        {hasErrors ? (
          <p role="alert" class="rounded-md border border-red-500/50 bg-red-500/10 p-3 text-red-300">
            {formError ?? 'Please correct the highlighted fields.'}
          </p>
        ) : null}
        {optionsUnavailable ? (
          <p role="status" class="rounded-md border border-yellow-500/50 bg-yellow-500/10 p-3 text-yellow-200">
            Existing studios, genres and formats could not be loaded. Check before adding a value so you don't create a
            duplicate.
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
          <LookupPanel label="Movie Lookup" enabled={Boolean(lookup?.available)}>
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
            <Field name="barcode" label="Barcode" error={errors.barcode}>
              <div class="flex gap-2">
                <input
                  id="barcode"
                  name="barcode"
                  type="text"
                  inputMode="numeric"
                  class={inputClasses}
                  placeholder="Barcode"
                  value={values.barcode}
                  {...errorProps('barcode', errors.barcode)}
                />
                {lookup?.available ? (
                  <button {...lookupButtonProps('barcode')} aria-label="Search by barcode">
                    Search
                  </button>
                ) : null}
                {lookup?.available ? <ScanButton field="barcode" identifierLabel="barcode" /> : null}
              </div>
            </Field>
          </LookupPanel>
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
          <Field name="runtime" label="Runtime (minutes)" error={errors.runtime}>
            <input
              id="runtime"
              name="runtime"
              type="number"
              min="0"
              step="1"
              class={inputClasses}
              placeholder="Runtime"
              value={values.runtime}
              {...errorProps('runtime', errors.runtime)}
            />
          </Field>
          <Field name="releaseDate" label="Release Date" error={errors.releaseDate}>
            <input
              id="releaseDate"
              name="releaseDate"
              type="text"
              class={inputClasses}
              placeholder="Release Date"
              value={values.releaseDate}
            />
          </Field>
          <label class="flex items-center gap-2 text-sm font-medium">
            <input
              id="isTvSeries"
              name="isTvSeries"
              type="checkbox"
              value="true"
              checked={values.isTvSeries === 'true'}
              class="h-5 w-5"
            />
            Is TV Series
          </label>
          <Field name="studios" label="Studios" error={errors.studios}>
            <MultiSelectInput
              name="studios"
              placeholder="Select Studios..."
              addLabel="Add new Studio:"
              options={options.studios}
              selectedValues={values.studios}
              invalid={Boolean(errors.studios)}
              describedBy={errors.studios ? errorId('studios') : undefined}
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
          <Field name="plot" label="Plot" error={errors.plot}>
            <textarea
              id="plot"
              name="plot"
              class={`${inputClasses} min-h-[100px]`}
              placeholder="Plot"
              value={values.plot}
            />
          </Field>
          <ImageInput
            existingSrc={existingImageSrc}
            imageUrl={values.imageUrl}
            allowRemove={Boolean(existingImageSrc)}
            removeImage={removeImage}
            errors={{ coverImage: errors.coverImage, imageUrl: errors.imageUrl }}
          />
          <div class="flex gap-2 justify-end">
            <a href={cancelHref} class="px-3 py-1">
              Cancel
            </a>
            <SaveButton label="Save" pendingLabel="Saving…" />
          </div>
          {lookup?.dialog ? <LookupDialog {...lookup.dialog} /> : null}
          {lookup?.available ? <BarcodeScannerDialog field="barcode" identifierLabel="barcode" /> : null}
        </form>
      </div>
    );
  };
}
