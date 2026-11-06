// The cover image limits and messages, shared by the server's upload checks (actions/forms/cover-image.ts)
// and the browser form's early check (image-input.tsx). It lives under public/ so the browser can load it.
export const COVER_IMAGE_MAX_BYTES = 5 * 1024 * 1024;
export const COVER_IMAGE_TYPES: readonly string[] = ['image/jpeg', 'image/png'];
export const COVER_IMAGE_TYPE_ERROR = 'Only JPEG and PNG images are allowed';
export const COVER_IMAGE_SIZE_ERROR = 'The image must be 5 MB or smaller';
