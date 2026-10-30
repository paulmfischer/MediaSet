import * as s from 'remix/data-schema';
import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { addEntity } from '../../../api/entity-data.ts';
import type { BookEntity } from '../../../models.ts';
import type { AppContext } from '../../../router.ts';
import { routes } from '../../../routes.ts';
import { Document } from '../../../ui/document.tsx';
import { BookFormPage } from '../../../ui/entity/add/book-form-page.tsx';
import { serverLogger } from '../../../utils/serverLogger.ts';
import { readCoverImage, entityWithImage } from '../../forms/cover-image.ts';
import { fieldErrors, submittedValues } from '../../forms/form-schema.ts';
import { loadBookOptions } from '../book-options.ts';
import { BOOK_FIELDS, bookForm, emptyBookValues, toBookEntity, type BookFormValues } from './book-form.ts';

async function renderBookForm(
  context: AppContext,
  state: { values?: BookFormValues; errors?: Record<string, string>; formError?: string; status?: number } = {}
) {
  const { values = emptyBookValues, errors = {}, formError, status = 200 } = state;
  const { options, unavailable } = await loadBookOptions();
  return context.render(
    <Document path={context.url.pathname} title="Add Book | MediaSet" description="Add a book to your collection">
      <BookFormPage
        heading="Add Book"
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
      const image = readCoverImage(context.formData);
      if (!parsed.success || image.error) {
        // Render here rather than redirecting to the GET page, which would lose the input and errors.
        return renderBookForm(context, {
          values: submittedValues(context.formData, BOOK_FIELDS),
          errors: {
            ...(image.error ? { coverImage: image.error } : {}),
            ...(parsed.success ? {} : fieldErrors(parsed.issues)),
          },
          status: 400,
        });
      }

      const book: BookEntity = toBookEntity(parsed.value);
      let saved: BookEntity;
      try {
        // An uploaded file takes precedence over a URL, so the URL isn't sent with it.
        if (image.file) {
          delete book.imageUrl;
          saved = await addEntity(book, entityWithImage(book, image.file));
        } else {
          saved = await addEntity(book);
        }
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

      // The API always returns the new id; without it the book can't be opened, so say so rather than redirect blind.
      if (!saved.id) {
        serverLogger.error('Saved book has no id', undefined, { operation: 'addBook' });
        return renderBookForm(context, {
          values: submittedValues(context.formData, BOOK_FIELDS),
          formError: 'The book was saved, but it could not be opened. Check your Books list before trying again.',
          status: 500,
        });
      }
      return redirect(routes.entityDetail.books.href({ id: saved.id }), 303);
    },
  },
});
