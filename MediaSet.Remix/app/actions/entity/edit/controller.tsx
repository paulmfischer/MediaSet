import * as s from 'remix/data-schema';
import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { getEntity, updateEntity } from '../../../api/entity-data.ts';
import { Entity, type BookEntity } from '../../../models.ts';
import type { AppContext } from '../../../router.ts';
import { routes } from '../../../routes.ts';
import { Document } from '../../../ui/document.tsx';
import { BookFormPage } from '../../../ui/entity/add/book-form-page.tsx';
import { UnavailablePage } from '../../../ui/entity/unavailable-page.tsx';
import { serverLogger } from '../../../utils/serverLogger.ts';
import { fieldErrors, submittedValues } from '../../forms/form-schema.ts';
import { BOOK_FIELDS, bookForm, toBookUpdate, toBookValues, type BookFormValues } from '../add/book-form.ts';
import { loadBookOptions } from '../book-options.ts';

const isStatus = (error: unknown, predicate: (status: number) => boolean): error is Response =>
  error instanceof Response && predicate(error.status);

function renderUnavailable(context: AppContext, status: number, heading: string, message: string) {
  return context.render(
    <Document path={context.url.pathname} title={`${heading} | MediaSet`}>
      <UnavailablePage
        heading={heading}
        message={message}
        backHref={routes.entityLists.books.href()}
        backLabel="Back to Books"
      />
    </Document>,
    { status }
  );
}

async function renderEditForm(
  context: AppContext,
  id: string,
  book: BookEntity,
  state: { values?: BookFormValues; errors?: Record<string, string>; formError?: string; status?: number } = {}
) {
  const { values = toBookValues(book), errors = {}, formError, status = 200 } = state;
  const { options, unavailable } = await loadBookOptions();
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
      'Something went wrong while loading it. Please try again.'
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

      const parsed = s.parseSafe(bookForm, context.formData);
      if (!parsed.success) {
        return renderEditForm(context, id, book, {
          values: submittedValues(context.formData, BOOK_FIELDS),
          errors: fieldErrors(parsed.issues),
          status: 400,
        });
      }

      try {
        await updateEntity(id, toBookUpdate(book, parsed.value));
      } catch (error) {
        serverLogger.error('Failed to update book', error, { operation: 'editBook', id });
        if (isStatus(error, (status) => status === 404)) {
          return renderUnavailable(context, 404, 'Book not found', "We couldn't find that book.");
        }
        const values = submittedValues(context.formData, BOOK_FIELDS);
        // The API rejecting the book (4xx) is the user's to fix and carries its reason; anything else is ours.
        if (isStatus(error, (status) => status >= 400 && status < 500)) {
          return renderEditForm(context, id, book, { values, formError: await error.text(), status: 400 });
        }
        return renderEditForm(context, id, book, {
          values,
          formError: 'The book could not be saved. Please try again.',
          status: 500,
        });
      }
      return redirect(routes.entityDetail.books.href({ id }), 303);
    },
  },
});
