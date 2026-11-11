import {
  COVER_IMAGE_MAX_BYTES,
  COVER_IMAGE_SIZE_ERROR,
  COVER_IMAGE_TYPES,
  COVER_IMAGE_TYPE_ERROR,
} from '../../ui/forms/public/cover-image-limits.ts';

// Cover image limits, shared with the browser form (see cover-image-limits.ts).
export { COVER_IMAGE_MAX_BYTES, COVER_IMAGE_TYPES };

/** The uploaded file in a form submission, or the message to show when it can't be used. */
export type CoverImageUpload = { file?: File; error?: string };

/** Reads the `coverImage` file input. A form submitted without choosing a file sends an empty file, which is ignored. */
export function readCoverImage(formData: FormData): CoverImageUpload {
  const value = formData.get('coverImage');
  if (!(value instanceof File) || value.size === 0) return {};
  if (!COVER_IMAGE_TYPES.includes(value.type)) return { error: COVER_IMAGE_TYPE_ERROR };
  if (value.size > COVER_IMAGE_MAX_BYTES) return { error: COVER_IMAGE_SIZE_ERROR };
  return { file: value };
}

/** The API's multipart shape for saving an entity with a cover image: the entity as JSON plus the file. */
export function entityWithImage(entity: object, file: File): FormData {
  const form = new FormData();
  form.set('entity', JSON.stringify(entity));
  form.set('coverImage', file);
  return form;
}

/**
 * Cover image failures the API reports (`imageError`) with the fixed text shown for each. Codes come from the
 * query string, so only these known ones map to text; anything else gets the generic message.
 */
export const COVER_IMAGE_ERROR_MESSAGES: Readonly<Record<string, string>> = {
  url_unreachable: 'The image URL could not be reached or returned an error.',
  not_an_image: 'The URL did not return a valid JPEG or PNG image.',
  download_too_large: 'The image at that URL is larger than the size limit.',
  unsupported_type: 'The file is not a supported image. Use a JPEG or PNG.',
  upload_too_large: 'The file is larger than the size limit.',
  pixel_limit_exceeded: 'The image has more pixels than the limit allows.',
  unknown: 'The image could not be processed.',
};

/** The detail page URL to redirect to after a save, carrying the cover image error code when there is one. */
export function withImageError(href: string, imageError: string | undefined): string {
  return imageError ? `${href}?imageError=${encodeURIComponent(imageError)}` : href;
}

/** The notice for an `imageError` code read from the URL: undefined when there is none, fixed text otherwise. */
export function coverImageNotice(code: string | null | undefined): string | undefined {
  if (!code) return undefined;
  const reason = Object.hasOwn(COVER_IMAGE_ERROR_MESSAGES, code)
    ? COVER_IMAGE_ERROR_MESSAGES[code]
    : COVER_IMAGE_ERROR_MESSAGES.unknown;
  return `Saved, but the cover image could not be stored: ${reason}`;
}
