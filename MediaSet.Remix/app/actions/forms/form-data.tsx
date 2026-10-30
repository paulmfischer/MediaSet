import {
  MaxFilesExceededError,
  MaxFileSizeExceededError,
  MaxHeaderSizeExceededError,
  MaxPartsExceededError,
  MaxTotalSizeExceededError,
} from 'remix/form-data-parser';
import { formData } from 'remix/middleware/form-data';

import type { AppContext } from '../../router.ts';
import { Document } from '../../ui/document.tsx';
import { UnavailablePage } from '../../ui/entity/unavailable-page.tsx';
import { COVER_IMAGE_MAX_BYTES } from './cover-image.ts';

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
    maxParts: 100,
    maxTotalSize: COVER_IMAGE_MAX_BYTES + 256 * 1024,
  });

  const middleware: typeof parse = async (context, next) => {
    try {
      return await parse(context, next);
    } catch (error) {
      if (!isLimitError(error)) throw error;
      const app = context as unknown as AppContext;
      const { url } = app;
      return app.render(
        <Document path={url.pathname} title="Upload too large | MediaSet">
          <UnavailablePage
            heading="That upload is too large"
            message="Images must be JPEG or PNG and 5 MB or smaller. Go back and choose a smaller image."
            backHref={url.pathname}
            backLabel="Back to the form"
          />
        </Document>,
        { status: 413 }
      );
    }
  };
  return middleware;
}
