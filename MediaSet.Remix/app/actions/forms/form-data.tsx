import {
  MaxFilesExceededError,
  MaxFileSizeExceededError,
  MaxHeaderSizeExceededError,
  MaxPartsExceededError,
  MaxTotalSizeExceededError,
} from 'remix/form-data-parser';
import { formData } from 'remix/middleware/form-data';

import type { AppContext } from '../../router.ts';
import { renderError } from '../render-error.tsx';
import { COVER_IMAGE_MAX_BYTES } from './cover-image.ts';

// Each selected genre, author or track field is one part, and a metadata lookup can apply a hundred or
// more genres at once, so the cap is generous; the total size limit still bounds the request.
const MAX_FORM_PARTS = 1000;

const isLimitError = (error: unknown) =>
  error instanceof MaxFilesExceededError ||
  error instanceof MaxFileSizeExceededError ||
  error instanceof MaxHeaderSizeExceededError ||
  error instanceof MaxPartsExceededError ||
  error instanceof MaxTotalSizeExceededError;

/**
 * Parses form bodies once, with explicit multipart limits: one file of up to the cover image size, a
 * bounded number of parts, and a total just above the file limit for the other fields. A request over
 * a limit is answered with 413 and a page to go back to the form.
 */
export function boundedFormData() {
  const parse = formData({
    maxFiles: 1,
    maxFileSize: COVER_IMAGE_MAX_BYTES,
    maxParts: MAX_FORM_PARTS,
    maxTotalSize: COVER_IMAGE_MAX_BYTES + 256 * 1024,
  });

  const middleware: typeof parse = async (context, next) => {
    try {
      return await parse(context, next);
    } catch (error) {
      if (!isLimitError(error)) throw error;
      const app = context as unknown as AppContext;
      return renderError(app, 413, error, {
        heading: 'That upload is too large',
        message: 'Images must be JPEG or PNG and 5 MB or smaller. Go back and choose a smaller image.',
        title: 'Upload too large',
        backHref: app.url.pathname,
        backLabel: 'Back to the form',
        // The rest of the oversized body is never read, so close the connection rather than leave it half-open.
        headers: { Connection: 'close' },
      });
    }
  };
  return middleware;
}
