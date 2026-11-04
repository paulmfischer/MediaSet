import * as s from 'remix/data-schema';
import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { getEntity, updateEntity } from '../../../api/entity-data.ts';
import { getLookupCapabilities, isLookupAvailable } from '../../../api/lookup-capabilities-data.ts';
import { clientApiUrl } from '../../../constants.server.ts';
import { Entity, type BookEntity } from '../../../models.ts';
import type { AppContext } from '../../../router.ts';
import { routes } from '../../../routes.ts';
import { Document } from '../../../ui/document.tsx';
import { BookFormPage } from '../../../ui/entity/add/book-form-page.tsx';
import { serverLogger } from '../../../utils/serverLogger.ts';
import { entityWithImage, readCoverImage } from '../../forms/cover-image.ts';
import { fieldErrors, submittedValues } from '../../forms/form-schema.ts';
import { buildLookupProps, readEditSubmission, type LookupOutcome } from '../../forms/lookup.ts';
import { entityErrorPage } from '../../render-error.tsx';
import { BOOK_FIELDS, bookForm, toBookUpdate, toBookValues, type BookFormValues } from '../add/book-form.ts';
import { resolveBookLookupIntent, toBookResultSummary } from '../add/book-lookup.ts';
import { loadBookOptions } from '../book-options.ts';

const isStatus = (error: unknown, predicate: (status: number) => boolean): error is Response =>
  error instanceof Response && predicate(error.status);

const renderUnavailable = entityErrorPage(routes.entityLists.books.href(), 'Back to Books');

async function renderEditForm(
  context: AppContext,
  id: string,
  book: BookEntity,
  state: {
    values?: BookFormValues;
    errors?: Record<string, string>;
    formError?: string;
    status?: number;
    /** The outcome of a lookup, shown in the results dialog. */
    lookup?: LookupOutcome;
    /** Makes the browser take `values` over what is typed in its inputs. */
    formKey?: string;
    /** The "remove the current image" box was ticked; kept across a re-render. */
    removeImage?: boolean;
  } = {}
) {
  const { values = toBookValues(book), errors = {}, formError, status = 200, lookup, formKey, removeImage } = state;
  const [{ options, unavailable }, capabilities] = await Promise.all([loadBookOptions(), getLookupCapabilities()]);
  return context.render(
    <Document
      path={context.url.pathname}
      title={`Edit ${book.title ?? 'Book'} | MediaSet`}
      description="Edit a book in your collection"
    >
      <BookFormPage
        heading="Edit Book"
        action={routes.entityEdit.books.action.href({ id })}
        cancelHref={routes.entityDetail.books.href({ id })}
        values={values}
        errors={errors}
        formError={formError}
        options={options}
        optionsUnavailable={unavailable}
        formKey={formKey}
        removeImage={removeImage}
        lookup={buildLookupProps({
          available: isLookupAvailable(capabilities, Entity.Books),
          itemLabel: 'book',
          lookup,
          toValues: (result) => toBookValues(result as BookEntity),
          toSummary: toBookResultSummary,
        })}
        existingImageSrc={
          book.coverImage
            ? `${clientApiUrl}/static/images/${book.coverImage.filePath.split('/').map(encodeURIComponent).join('/')}`
            : undefined
        }
      />
    </Document>,
    { status }
  );
}

/** Loads the book, or returns the 404/500 page to send instead. */
async function loadBook(context: AppContext, id: string): Promise<BookEntity | Response> {
  try {
    return await getEntity<BookEntity>(Entity.Books, id);
  } catch (error) {
    if (isStatus(error, (status) => status === 404)) {
      return renderUnavailable(context, 404, 'Book not found', "We couldn't find that book.");
    }
    serverLogger.error('Failed to load book for editing', error, { operation: 'editBook', id });
    return renderUnavailable(
      context,
      500,
      "Couldn't load this book",
      'Something went wrong while loading it. Please try again.',
      error
    );
  }
}

export default createController(routes.entityEdit.books, {
  actions: {
    async index(context) {
      const book = await loadBook(context, context.params.id);
      return book instanceof Response ? book : renderEditForm(context, context.params.id, book);
    },
    async action(context) {
      const { id } = context.params;
      const book = await loadBook(context, id);
      if (book instanceof Response) return book;

      // Lookup, apply and dismiss re-render the form with what the user has entered; they never save.
      const { intent, removeImage } = readEditSubmission(context.formData);
      if (intent !== 'save') {
        const state = await resolveBookLookupIntent(context.formData, intent);
        return renderEditForm(context, id, book, { ...state, removeImage });
      }

      const parsed = s.parseSafe(bookForm, context.formData);
      const image = readCoverImage(context.formData);
      if (!parsed.success || image.error) {
        return renderEditForm(context, id, book, {
          values: submittedValues(context.formData, BOOK_FIELDS),
          errors: {
            ...(image.error ? { coverImage: image.error } : {}),
            ...(parsed.success ? {} : fieldErrors(parsed.issues)),
          },
          status: 400,
          removeImage,
        });
      }

      try {
        const update = toBookUpdate(book, parsed.value, removeImage);
        // An uploaded file takes precedence over a URL, so the URL isn't sent with it.
        if (image.file) {
          delete update.imageUrl;
          await updateEntity(id, update, entityWithImage(update, image.file));
        } else {
          await updateEntity(id, update);
        }
      } catch (error) {
        serverLogger.error('Failed to update book', error, { operation: 'editBook', id });
        if (isStatus(error, (status) => status === 404)) {
          return renderUnavailable(context, 404, 'Book not found', "We couldn't find that book.");
        }
        const values = submittedValues(context.formData, BOOK_FIELDS);
        // The API rejecting the book (4xx) is the user's to fix and carries its reason; anything else is ours.
        if (isStatus(error, (status) => status >= 400 && status < 500)) {
          return renderEditForm(context, id, book, { values, formError: await error.text(), status: 400, removeImage });
        }
        return renderEditForm(context, id, book, {
          values,
          formError: 'The book could not be saved. Please try again.',
          status: 500,
          removeImage,
        });
      }
      return redirect(routes.entityDetail.books.href({ id }), 303);
    },
  },
});
