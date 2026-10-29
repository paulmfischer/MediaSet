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
// The page tells the user the lists are missing so they don't re-enter existing values as new ones.
async function loadBookOptions(): Promise<{ options: BookFormOptions; unavailable: boolean }> {
  let unavailable = false;
  const orEmpty = (property: string, lookup: Promise<Option[]>): Promise<Option[]> =>
    lookup.catch((error) => {
      unavailable = true;
      serverLogger.error(`Failed to load book ${property} options`, error, { operation: 'loadBookOptions', property });
      return [];
    });
  const [authors, genres, publishers, formats] = await Promise.all([
    orEmpty('authors', getAuthors()),
    orEmpty('genres', getGenres(Entity.Books)),
    orEmpty('publishers', getPublishers()),
    orEmpty('formats', getFormats(Entity.Books)),
  ]);
  return { options: { authors, genres, publishers, formats }, unavailable };
}

async function renderBookForm(
  context: AppContext,
  state: { values?: BookFormValues; errors?: Record<string, string>; formError?: string; status?: number } = {}
) {
  const { values = emptyBookValues, errors = {}, formError, status = 200 } = state;
  const { options, unavailable } = await loadBookOptions();
  return context.render(
    <Document path={context.url.pathname} title="Add Book | MediaSet" description="Add a book to your collection">
      <BookFormPage
        action={routes.entityAdd.books.action.href()}
        cancelHref={routes.entityLists.books.href()}
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
      let saved: BookEntity;
      try {
        saved = await addEntity(book);
      } catch (error) {
        serverLogger.error('Failed to add book', error, { operation: 'addBook' });
        const values = submittedValues(context.formData, BOOK_FIELDS);
        // The API rejecting the book (4xx) is the user's to fix and carries its reason; anything else is ours.
        if (error instanceof Response && error.status >= 400 && error.status < 500) {
          return renderBookForm(context, { values, formError: await error.text(), status: 400 });
        }
        return renderBookForm(context, {
          values,
          formError: 'The book could not be saved. Please try again.',
          status: 500,
        });
      }

      return redirect(routes.entityDetail.books.href({ id: saved.id ?? '' }), 303);
    },
  },
});
