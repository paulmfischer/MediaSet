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
