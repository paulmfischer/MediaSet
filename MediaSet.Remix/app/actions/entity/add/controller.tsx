import * as s from 'remix/data-schema';
import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { addEntity } from '../../../api/entity-data.ts';
import { getAuthors, getFormats, getGenres, getPublishers } from '../../../api/metadata-data.ts';
import { Entity, type BookEntity, type Option } from '../../../models.ts';
import type { AppContext } from '../../../router.ts';
import { routes } from '../../../routes.ts';
import { Document } from '../../../ui/document.tsx';
import { BookFormPage, type BookFormOptions } from '../../../ui/entity/add/book-form-page.tsx';
import { serverLogger } from '../../../utils/serverLogger.ts';
import { fieldErrors, submittedValues } from '../../forms/form-schema.ts';
import { BOOK_FIELDS, bookForm, emptyBookValues, toBookEntity, type BookFormValues } from './book-form.ts';

// A failed lookup leaves a select empty rather than blocking the form: new values can still be typed.
const orEmpty = (lookup: Promise<Option[]>): Promise<Option[]> => lookup.catch(() => []);

async function loadBookOptions(): Promise<BookFormOptions> {
  const [authors, genres, publishers, formats] = await Promise.all([
    orEmpty(getAuthors()),
    orEmpty(getGenres(Entity.Books)),
    orEmpty(getPublishers()),
    orEmpty(getFormats(Entity.Books)),
  ]);
  return { authors, genres, publishers, formats };
}

async function renderBookForm(
  context: AppContext,
  state: { values?: BookFormValues; errors?: Record<string, string>; formError?: string; status?: number } = {}
) {
  const { values = emptyBookValues, errors = {}, formError, status = 200 } = state;
  return context.render(
    <Document path={context.url.pathname} title="Add Book | MediaSet" description="Add a book to your collection">
      <BookFormPage
        action={routes.entityAdd.books.action.href()}
        cancelHref={routes.entityLists.books.href()}
        values={values}
        errors={errors}
        formError={formError}
        options={await loadBookOptions()}
      />
    </Document>,
    { status }
  );
}

export default createController(routes.entityAdd.books, {
  actions: {
    index: (context) => renderBookForm(context),
    async action(context) {
      const parsed = s.parseSafe(bookForm, context.formData);
      if (!parsed.success) {
        // Render here rather than redirecting to the GET page, which would lose the input and errors.
        return renderBookForm(context, {
          values: submittedValues(context.formData, BOOK_FIELDS),
          errors: fieldErrors(parsed.issues),
          status: 400,
        });
      }

      const book: BookEntity = toBookEntity(parsed.value);
      try {
        await addEntity(book);
      } catch (error) {
        serverLogger.error('Failed to add book', error, { operation: 'addBook' });
        return renderBookForm(context, {
          values: submittedValues(context.formData, BOOK_FIELDS),
          formError: 'The book could not be saved. Please try again.',
          status: 500,
        });
      }

      // TODO(#693): redirect to the new book's details page once item details pages exist.
      return redirect(routes.entityLists.books.href(), 303);
    },
  },
});
