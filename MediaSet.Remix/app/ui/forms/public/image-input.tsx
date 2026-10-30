import { clientEntry, on, type Handle } from 'remix/component';

import { errorId, errorProps, inputClasses } from '../field.tsx';

interface ImageInputProps {
  /** Browser URL of the saved cover image (edit form); shown until another image is chosen. */
  existingSrc?: string | undefined;
  /** The image URL field's value. */
  imageUrl: string;
  /** Offer a "remove" checkbox (an item that already has an image). */
  allowRemove?: boolean;
  /** Validation messages for the `coverImage` and `imageUrl` fields. */
  errors: { coverImage?: string | undefined; imageUrl?: string | undefined };
}

const isWebUrl = (value: string) => /^https?:\/\//i.test(value);

// Cover image inputs for any media type's form: choose a file or give a URL, with a preview and where
// the image comes from. Both inputs are native and submit without JavaScript; hydration adds the preview.
export const ImageInput = clientEntry(import.meta.url, function ImageInput(handle: Handle<ImageInputProps>) {
  let url = handle.props.imageUrl;
  let fileName = '';
  let objectUrl = '';
  let failed = false;

  handle.signal.addEventListener('abort', () => objectUrl && URL.revokeObjectURL(objectUrl));

  const source = () => {
    if (objectUrl) return { src: objectUrl, origin: `Uploaded file: ${fileName}` };
    if (isWebUrl(url.trim())) return { src: url.trim(), origin: 'From URL' };
    const { existingSrc } = handle.props;
    return existingSrc ? { src: existingSrc, origin: 'Current cover image' } : undefined;
  };

  const setFailed = (value: boolean) => {
    if (failed === value) return;
    failed = value;
    handle.update();
  };

  const chooseFile = (input: HTMLInputElement) => {
    if (objectUrl) URL.revokeObjectURL(objectUrl);
    const file = input.files?.[0];
    objectUrl = file ? URL.createObjectURL(file) : '';
    fileName = file?.name ?? '';
    failed = false;
    handle.update();
  };

  const typeUrl = (input: HTMLInputElement) => {
    url = input.value;
    failed = false;
    handle.update();
  };

  return () => {
    const { allowRemove, errors } = handle.props;
    const current = source();
    return (
      <fieldset class="flex flex-col gap-3">
        <legend class="block text-sm font-medium text-gray-200 mb-1">Cover Image</legend>
        {current ? (
          <figure class="flex flex-col items-start gap-1" data-testid="image-preview">
            {failed ? (
              <p role="alert" class="text-sm text-red-400">
                Unable to load image
              </p>
            ) : null}
            <img
              src={current.src}
              alt="Cover preview"
              class={failed ? 'hidden' : 'h-40 w-auto rounded-md object-contain'}
              mix={[on('error', () => setFailed(true)), on('load', () => setFailed(false))]}
            />
            <figcaption class="text-xs text-gray-400">{current.origin}</figcaption>
          </figure>
        ) : null}
        <div>
          <label class="block text-sm text-gray-300 mb-1">
            <span class="block mb-1">Upload an image (JPEG or PNG, up to 5 MB)</span>
            <input
              id="coverImage"
              name="coverImage"
              type="file"
              accept="image/jpeg,image/png"
              class={inputClasses}
              {...errorProps('coverImage', errors.coverImage)}
              mix={on('change', (event) => chooseFile(event.currentTarget))}
            />
          </label>
          {errors.coverImage ? (
            <p id={errorId('coverImage')} class="mt-1 text-sm text-red-400">
              {errors.coverImage}
            </p>
          ) : null}
        </div>
        <div>
          <label class="block text-sm text-gray-300 mb-1">
            <span class="block mb-1">Or an image URL (an uploaded file takes precedence)</span>
            <input
              id="imageUrl"
              name="imageUrl"
              type="url"
              class={inputClasses}
              placeholder="https://example.com/cover.jpg"
              value={url}
              {...errorProps('imageUrl', errors.imageUrl)}
              mix={on('input', (event) => typeUrl(event.currentTarget))}
            />
          </label>
          {errors.imageUrl ? (
            <p id={errorId('imageUrl')} class="mt-1 text-sm text-red-400">
              {errors.imageUrl}
            </p>
          ) : null}
        </div>
        {allowRemove ? (
          <label class="flex items-center gap-2 text-sm text-gray-300">
            <input type="checkbox" name="removeImage" /> Remove the current image
          </label>
        ) : null}
      </fieldset>
    );
  };
});
