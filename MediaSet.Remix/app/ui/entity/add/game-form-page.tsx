import type { Handle } from 'remix/component';

import type { GameFormValues } from '../../../actions/entity/add/game-form.ts';
import type { Option } from '../../../models.ts';
import { errorId, errorProps, Field, inputClasses } from '../../forms/field.tsx';
import { LookupDialog, type LookupDialogProps } from '../../forms/public/lookup-dialog.tsx';
import { MultiSelectInput } from '../../forms/public/multi-select-input.tsx';
import { ImageInput } from '../../forms/public/image-input.tsx';
import { SaveButton } from '../../forms/public/save-button.tsx';
import { SingleSelectInput } from '../../forms/public/single-select-input.tsx';

export interface GameFormOptions {
  developers: Option[];
  publishers: Option[];
  genres: Option[];
  platforms: Option[];
  formats: Option[];
}

interface GameFormPageProps {
  heading: string;
  action: string;
  cancelHref: string;
  values: GameFormValues;
  /** First validation message per field name. */
  errors: Record<string, string>;
  /** A failure not tied to a field, such as the API rejecting the save. */
  formError?: string | undefined;
  options: GameFormOptions;
  /** The existing developers, publishers, genres, platforms or formats could not be loaded. */
  optionsUnavailable?: boolean;
  /** Browser URL of the game's saved cover, when editing a game that has one. */
  existingImageSrc?: string | undefined;
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
// able to trip the upload size limit before the game is saved.
const lookupButtonProps = (field: 'title' | 'barcode') =>
  ({
    type: 'submit',
    name: 'intent',
    value: `lookup:${field}`,
    class: 'secondary',
    formnovalidate: true,
    formenctype: 'application/x-www-form-urlencoded',
  }) as const;

export function GameFormPage(handle: Handle<GameFormPageProps>) {
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
      lookup,
      formKey,
    } = handle.props;
    const hasErrors = Object.keys(errors).length > 0 || Boolean(formError);

    return (
      <div class="entity-games flex flex-col gap-4 px-2 max-w-3xl mx-auto">
        <h1 class="text-2xl text-entity">{heading}</h1>
        {hasErrors ? (
          <p role="alert" class="rounded-md border border-red-500/50 bg-red-500/10 p-3 text-red-300">
            {formError ?? 'Please correct the highlighted fields.'}
          </p>
        ) : null}
        {optionsUnavailable ? (
          <p role="status" class="rounded-md border border-yellow-500/50 bg-yellow-500/10 p-3 text-yellow-200">
            Existing developers, publishers, genres, platforms and formats could not be loaded. Check before adding a
            value so you don't create a duplicate.
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
                <button {...lookupButtonProps('title')} aria-label="Look up by title">
                  Look up
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
                <button {...lookupButtonProps('barcode')} aria-label="Look up by barcode">
                  Look up
                </button>
              ) : null}
            </div>
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
          <Field name="platform" label="Platform" error={errors.platform}>
            <SingleSelectInput
              name="platform"
              placeholder="Select Platform..."
              addLabel="Add new Platform:"
              options={options.platforms}
              selectedValue={values.platform}
              invalid={Boolean(errors.platform)}
              describedBy={errors.platform ? errorId('platform') : undefined}
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
          <Field name="rating" label="Rating" error={errors.rating}>
            <input
              id="rating"
              name="rating"
              type="text"
              class={inputClasses}
              placeholder="Rating"
              value={values.rating}
            />
          </Field>
          <Field name="developers" label="Developers" error={errors.developers}>
            <MultiSelectInput
              name="developers"
              placeholder="Select Developers..."
              addLabel="Add new Developer:"
              options={options.developers}
              selectedValues={values.developers}
              invalid={Boolean(errors.developers)}
              describedBy={errors.developers ? errorId('developers') : undefined}
            />
          </Field>
          <Field name="publishers" label="Publishers" error={errors.publishers}>
            <MultiSelectInput
              name="publishers"
              placeholder="Select Publishers..."
              addLabel="Add new Publisher:"
              options={options.publishers}
              selectedValues={values.publishers}
              invalid={Boolean(errors.publishers)}
              describedBy={errors.publishers ? errorId('publishers') : undefined}
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
          <Field name="description" label="Description" error={errors.description}>
            <textarea
              id="description"
              name="description"
              class={`${inputClasses} min-h-[100px]`}
              placeholder="Description"
              value={values.description}
            />
          </Field>
          <ImageInput
            existingSrc={existingImageSrc}
            imageUrl={values.imageUrl}
            allowRemove={Boolean(existingImageSrc)}
            errors={{ coverImage: errors.coverImage, imageUrl: errors.imageUrl }}
          />
          <div class="flex gap-2 justify-end">
            <a href={cancelHref} class="px-3 py-1">
              Cancel
            </a>
            <SaveButton label="Save" pendingLabel="Saving…" />
          </div>
          {lookup?.dialog ? <LookupDialog {...lookup.dialog} /> : null}
        </form>
      </div>
    );
  };
}
