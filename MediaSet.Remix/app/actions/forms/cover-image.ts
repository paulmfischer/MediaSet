/** Cover image limits, shared by the upload middleware and the form's own checks. */
export const COVER_IMAGE_MAX_BYTES = 5 * 1024 * 1024;
export const COVER_IMAGE_TYPES = ['image/jpeg', 'image/png'];

/** The uploaded file in a form submission, or the message to show when it can't be used. */
export type CoverImageUpload = { file?: File; error?: string };

/** Reads the `coverImage` file input. A form submitted without choosing a file sends an empty file, which is ignored. */
export function readCoverImage(formData: FormData): CoverImageUpload {
  const value = formData.get('coverImage');
  if (!(value instanceof File) || value.size === 0) return {};
  if (!COVER_IMAGE_TYPES.includes(value.type)) return { error: 'Only JPEG and PNG images are allowed' };
  if (value.size > COVER_IMAGE_MAX_BYTES) return { error: 'The image must be 5 MB or smaller' };
  return { file: value };
}

/** The API's multipart shape for saving an entity with a cover image: the entity as JSON plus the file. */
export function entityWithImage(entity: object, file: File): FormData {
  const form = new FormData();
  form.set('entity', JSON.stringify(entity));
  form.set('coverImage', file);
  return form;
}
