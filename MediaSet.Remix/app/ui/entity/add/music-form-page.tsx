import type { Handle } from 'remix/component';

import type { MusicFormValues } from '../../../actions/entity/add/music-form.ts';
import type { Option } from '../../../models.ts';
import { errorId, errorProps, Field, inputClasses } from '../../forms/field.tsx';
import { LookupPanel } from '../../forms/lookup-panel.tsx';
import { LookupDialog, type LookupDialogProps } from '../../forms/public/lookup-dialog.tsx';
import { MultiSelectInput } from '../../forms/public/multi-select-input.tsx';
import { ImageInput } from '../../forms/public/image-input.tsx';
import { BarcodeScannerDialog } from '../../forms/public/barcode-scanner-dialog.tsx';
import { ScanButton } from '../../forms/public/scan-button.tsx';
import { SaveButton } from '../../forms/public/save-button.tsx';
import { TrackListInput } from '../../forms/public/track-list-input.tsx';
import { SingleSelectInput } from '../../forms/public/single-select-input.tsx';

export interface MusicFormOptions {
  artists: Option[];
  labels: Option[];
  genres: Option[];
  formats: Option[];
}

interface MusicFormPageProps {
  heading: string;
  action: string;
  cancelHref: string;
  values: MusicFormValues;
  /** First validation message per field name. */
  errors: Record<string, string>;
  /** A failure not tied to a field, such as the API rejecting the save. */
  formError?: string | undefined;
  options: MusicFormOptions;
  /** The existing artists, labels, genres or formats could not be loaded. */
  optionsUnavailable?: boolean;
  /** Browser URL of the album's saved cover, when editing an album that has one. */
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
// able to trip the upload size limit before the album is saved.
const lookupButtonProps = (field: 'title' | 'barcode') =>
  ({
    type: 'submit',
    name: 'intent',
    value: `lookup:${field}`,
    formnovalidate: true,
    formenctype: 'application/x-www-form-urlencoded',
  }) as const;

export function MusicFormPage(handle: Handle<MusicFormPageProps>) {
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
      <div class="entity-musics flex flex-col gap-4 px-2 max-w-3xl mx-auto">
        <h1 class="text-2xl text-entity">{heading}</h1>
        {hasErrors ? (
          <p role="alert" class="rounded-md border border-red-500/50 bg-red-500/10 p-3 text-red-300">
            {formError ?? 'Please correct the highlighted fields.'}
          </p>
        ) : null}
        {optionsUnavailable ? (
          <p role="status" class="rounded-md border border-yellow-500/50 bg-yellow-500/10 p-3 text-yellow-200">
            Existing artists, labels, genres and formats could not be loaded. Check before adding a value so you don't
            create a duplicate.
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
          <LookupPanel label="Music Lookup" enabled={Boolean(lookup?.available)}>
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
                  <button {...lookupButtonProps('title')} aria-label="Search by title and artist">
                    Search
                  </button>
                ) : null}
              </div>
            </Field>
            <Field name="artist" label="Artist" required error={errors.artist}>
              <SingleSelectInput
                name="artist"
                placeholder="Select Artist..."
                addLabel="Add new Artist:"
                options={options.artists}
                selectedValue={values.artist}
                invalid={Boolean(errors.artist)}
                describedBy={errors.artist ? errorId('artist') : undefined}
              />
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
          <ImageInput
            existingSrc={existingImageSrc}
            imageUrl={values.imageUrl}
            allowRemove={Boolean(existingImageSrc)}
            removeImage={removeImage}
            errors={{ coverImage: errors.coverImage, imageUrl: errors.imageUrl }}
          />
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
          <Field name="label" label="Label" error={errors.label}>
            <SingleSelectInput
              name="label"
              placeholder="Select Label..."
              addLabel="Add new Label:"
              options={options.labels}
              selectedValue={values.label}
              invalid={Boolean(errors.label)}
              describedBy={errors.label ? errorId('label') : undefined}
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
          <div class="grid grid-cols-2 gap-4">
            <Field name="tracks" label="Tracks" error={errors.tracks}>
              <input
                id="tracks"
                name="tracks"
                type="number"
                min="0"
                step="1"
                class={inputClasses}
                placeholder="Tracks"
                value={values.tracks}
                {...errorProps('tracks', errors.tracks)}
              />
            </Field>
            <Field name="discs" label="Discs" error={errors.discs}>
              <input
                id="discs"
                name="discs"
                type="number"
                min="0"
                step="1"
                class={inputClasses}
                placeholder="Discs"
                value={values.discs}
                {...errorProps('discs', errors.discs)}
              />
            </Field>
          </div>
          <div class="grid grid-cols-2 gap-4">
            <Field name="durationMinutes" label="Duration (minutes)" error={errors.durationMinutes}>
              <input
                id="durationMinutes"
                name="durationMinutes"
                type="number"
                min="0"
                step="1"
                class={inputClasses}
                placeholder="Minutes"
                value={values.durationMinutes}
                {...errorProps('durationMinutes', errors.durationMinutes)}
              />
            </Field>
            <Field name="durationSeconds" label="Duration (seconds)" error={errors.durationSeconds}>
              <input
                id="durationSeconds"
                name="durationSeconds"
                type="number"
                min="0"
                max="59"
                step="1"
                class={inputClasses}
                placeholder="Seconds"
                value={values.durationSeconds}
                {...errorProps('durationSeconds', errors.durationSeconds)}
              />
            </Field>
          </div>
          <fieldset class="flex flex-col gap-2">
            <legend class="block text-sm font-medium text-gray-200 mb-1">Track list</legend>
            <TrackListInput rows={values.trackList} errors={errors} />
          </fieldset>
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
