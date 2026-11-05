import { clientEntry, on, ref, type Handle } from 'remix/component';

import { Icon } from '../../icon.tsx';
import { ImagePlus } from '../../icons.ts';
import { errorId, errorProps, Field, inputClasses } from '../field.tsx';

interface ImageInputProps {
  /** Browser URL of the saved cover image (edit form); shown until another image is chosen. */
  existingSrc?: string | undefined;
  /** The image URL field's value. */
  imageUrl: string;
  /** Offer a "remove" checkbox (an item that already has an image). */
  allowRemove?: boolean;
  /** The remove checkbox starts ticked, so it survives a lookup re-render. */
  removeImage?: boolean | undefined;
  /** Validation messages for the `coverImage` and `imageUrl` fields. */
  errors: { coverImage?: string | undefined; imageUrl?: string | undefined };
}

/** How long typing in the URL field pauses before the preview loads it. */
const PREVIEW_DELAY_MS = 400;

const ACCEPTED_TYPES = ['image/jpeg', 'image/png'];
const MAX_BYTES = 5 * 1024 * 1024;

const isWebUrl = (value: string) => /^https?:\/\//i.test(value);

// Cover image inputs for any media type's form: choose a file or give a URL, with a preview and where
// the image comes from. Both inputs are native and submit without JavaScript; hydration adds the preview.
export const ImageInput = clientEntry(import.meta.url, function ImageInput(handle: Handle<ImageInputProps>) {
  let url = handle.props.imageUrl;
  // The URL the preview loads: it follows `url` after typing pauses, so partial URLs aren't requested.
  let previewUrl = url;
  let previewTimer: ReturnType<typeof setTimeout> | undefined;
  let fileName = '';
  let objectUrl = '';
  let failed = false;
  let dragging = false;
  let fileProblem: string | undefined;
  // Controlled, so a re-render keeps what was ticked; it starts from the server's value.
  let remove = handle.props.removeImage ?? false;

  handle.signal.addEventListener('abort', () => {
    clearTimeout(previewTimer);
    if (objectUrl) URL.revokeObjectURL(objectUrl);
  });

  const source = () => {
    if (objectUrl) return { src: objectUrl, origin: `Uploaded file: ${fileName}` };
    if (isWebUrl(previewUrl.trim())) return { src: previewUrl.trim(), origin: 'From URL' };
    const { existingSrc } = handle.props;
    return existingSrc ? { src: existingSrc, origin: 'Current cover image' } : undefined;
  };

  const setFailed = (value: boolean) => {
    if (failed === value) return;
    failed = value;
    handle.update();
  };

  // The same limits as the server and the old UI: JPEG or PNG, up to 5MB.
  const problemWith = (file: File): string | undefined => {
    if (!ACCEPTED_TYPES.includes(file.type)) return 'Only JPEG and PNG images are allowed';
    if (file.size > MAX_BYTES) return 'File size must be less than 5MB';
    return undefined;
  };

  const chooseFile = (input: HTMLInputElement) => {
    if (objectUrl) URL.revokeObjectURL(objectUrl);
    const file = input.files?.[0];
    fileProblem = file ? problemWith(file) : undefined;
    // A file that can't be used is dropped, so it is neither previewed nor submitted.
    if (fileProblem) input.value = '';
    const usable = file && !fileProblem ? file : undefined;
    objectUrl = usable ? URL.createObjectURL(usable) : '';
    fileName = usable?.name ?? '';
    failed = false;
    handle.update();
  };

  const setDragging = (event: DragEvent, value: boolean) => {
    event.preventDefault();
    // dragleave also fires when the pointer moves onto a child of the area; only leaving the area counts.
    if (!value && event.currentTarget instanceof Node && event.relatedTarget instanceof Node) {
      if (event.currentTarget.contains(event.relatedTarget)) return;
    }
    if (dragging === value) return;
    dragging = value;
    handle.update();
  };

  // A dropped file goes into the file input, so it is submitted like a chosen one. The input takes
  // one file and bypasses its accept filter for drops, so only the first file is used and it is checked here.
  const dropFile = (event: DragEvent) => {
    event.preventDefault();
    dragging = false;
    const input = document.getElementById('coverImage');
    const file = event.dataTransfer?.files[0];
    if (input instanceof HTMLInputElement && file) {
      const transfer = new DataTransfer();
      transfer.items.add(file);
      input.files = transfer.files;
      chooseFile(input);
    } else {
      handle.update();
    }
  };

  const showUrl = () => {
    clearTimeout(previewTimer);
    if (previewUrl === url) return;
    previewUrl = url;
    failed = false;
    handle.update();
  };

  const typeUrl = (input: HTMLInputElement) => {
    url = input.value;
    // The input is controlled: re-render so the typed value stays, while the preview waits for a pause.
    handle.update();
    clearTimeout(previewTimer);
    previewTimer = setTimeout(showUrl, PREVIEW_DELAY_MS);
  };

  // An image that failed before hydration never fired an error event the island could hear.
  const checkLoaded = (image: HTMLImageElement) => {
    if (image.complete && image.naturalWidth === 0) handle.queueTask(() => setFailed(true));
  };

  return () => {
    const { allowRemove, errors } = handle.props;
    const current = source();
    // A chosen file or the saved image fills the drop area; an image from the URL sits beside the URL field.
    const fromUrl = current?.origin === 'From URL';
    const preview = current ? (
      <figure class="flex flex-col items-center gap-1" data-testid="image-preview">
        {failed ? (
          <p role="alert" class="text-xs text-red-400">
            Unable to load image
          </p>
        ) : null}
        <img
          src={current.src}
          alt="Cover preview"
          class={failed ? 'hidden' : fromUrl ? 'h-24 w-auto object-contain' : 'mx-auto h-40 w-auto object-contain'}
          mix={[
            ref((node) => checkLoaded(node as HTMLImageElement)),
            on('error', () => setFailed(true)),
            on('load', () => setFailed(false)),
          ]}
        />
        <figcaption class="text-sm text-gray-300">{current.origin}</figcaption>
      </figure>
    ) : null;
    return (
      <fieldset class="flex flex-col gap-4">
        <legend class="block text-sm font-medium text-gray-200 mb-1">Cover Image</legend>
        <div>
          {/* The whole area is the file input's label, so clicking it opens the picker without JavaScript. */}
          <label
            for="coverImage"
            class={`relative flex cursor-pointer flex-col items-center justify-center gap-2 rounded-lg border-2 border-dashed p-6 text-center transition-colors has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-blue-400 ${
              dragging
                ? 'border-blue-400 bg-blue-900/30'
                : 'border-gray-600 bg-gray-800 hover:border-gray-500 hover:bg-gray-700'
            }`}
            mix={[
              on('dragenter', (event) => setDragging(event, true)),
              on('dragover', (event) => setDragging(event, true)),
              on('dragleave', (event) => setDragging(event, false)),
              on('drop', (event) => dropFile(event)),
            ]}
          >
            <input
              id="coverImage"
              name="coverImage"
              type="file"
              accept="image/jpeg,image/png"
              class="sr-only"
              aria-label="Upload cover image"
              {...errorProps('coverImage', errors.coverImage)}
              mix={on('change', (event) => chooseFile(event.currentTarget))}
            />
            {current && !fromUrl ? (
              preview
            ) : (
              <>
                <Icon icon={ImagePlus} size={48} class="text-gray-400" />
                <span class="text-sm text-gray-300">
                  <span class="block font-medium">Drag and drop your image here</span>
                  <span class="block text-xs text-gray-400">or click to select</span>
                </span>
                <span class="text-xs text-gray-400">PNG or JPEG up to 5MB</span>
              </>
            )}
          </label>
          {fileProblem ? (
            <p role="alert" class="mt-1 text-sm text-red-400">
              {fileProblem}
            </p>
          ) : null}
          {errors.coverImage ? (
            <p id={errorId('coverImage')} class="mt-1 text-sm text-red-400">
              {errors.coverImage}
            </p>
          ) : null}
        </div>
        <Field name="imageUrl" label="Image URL" error={errors.imageUrl}>
          <p id="imageUrl-help" class="text-xs text-gray-400 mb-2">
            Provide an image URL or upload a file above. If both are provided, the uploaded file takes precedence.
            Accepted: JPEG or PNG, up to 5MB.
          </p>
          <div class="flex items-start gap-2">
            <input
              id="imageUrl"
              name="imageUrl"
              type="url"
              class={`${inputClasses} flex-1`}
              placeholder="https://example.com/image.jpg"
              value={url}
              aria-describedby={errors.imageUrl ? `imageUrl-help ${errorId('imageUrl')}` : 'imageUrl-help'}
              aria-invalid={errors.imageUrl ? 'true' : undefined}
              mix={[on('input', (event) => typeUrl(event.currentTarget)), on('change', showUrl)]}
            />
            {fromUrl ? <div class="flex w-32 justify-center">{preview}</div> : null}
          </div>
        </Field>
        {allowRemove ? (
          <label class="flex items-center gap-2 text-sm text-gray-300">
            <input
              type="checkbox"
              name="removeImage"
              checked={remove}
              mix={on('change', (event) => {
                remove = event.currentTarget.checked;
                handle.update();
              })}
            />{' '}
            Remove the current image
          </label>
        ) : null}
      </fieldset>
    );
  };
});
