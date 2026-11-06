import * as s from 'remix/data-schema';
import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { addEntity } from '../../../api/entity-data.ts';
import { getLookupCapabilities, isLookupAvailable } from '../../../api/lookup-capabilities-data.ts';
import { Entity, type BookEntity } from '../../../models.ts';
import type { AppContext } from '../../../router.ts';
import { routes } from '../../../routes.ts';
import { Document } from '../../../ui/document.tsx';
import { BookFormPage } from '../../../ui/entity/add/book-form-page.tsx';
import { serverLogger } from '../../../utils/serverLogger.ts';
import { readCoverImage, entityWithImage } from '../../forms/cover-image.ts';
import { fieldErrors, submittedValues } from '../../forms/form-schema.ts';
import { buildLookupProps, type LookupOutcome } from '../../forms/lookup.ts';
import { loadBookOptions } from '../book-options.ts';
import {
  BOOK_FIELDS,
  bookForm,
  emptyBookValues,
  toBookEntity,
  toBookValues,
  type BookFormValues,
} from './book-form.ts';
import { resolveBookLookupIntent, toBookResultSummary } from './book-lookup.ts';

async function renderBookForm(
  context: AppContext,
  state: {
    values?: BookFormValues;
    errors?: Record<string, string>;
    formError?: string;
    status?: number;
    /** The outcome of a lookup, shown in the results dialog. */
    lookup?: LookupOutcome;
    /** Makes the browser take `values` over what is typed in its inputs. */
    formKey?: string;
  } = {}
) {
  const { values = emptyBookValues, errors = {}, formError, status = 200, lookup, formKey } = state;
  const [{ options, unavailable }, capabilities] = await Promise.all([loadBookOptions(), getLookupCapabilities()]);
  return context.render(
    <Document path={context.url.pathname} title="Add Book | MediaSet" description="Add a book to your collection">
      <BookFormPage
        heading="Add Book"
        action={routes.entityAdd.books.action.href()}
        cancelHref={routes.entityLists.books.href()}
        submitLabel="Add Book"
        values={values}
        errors={errors}
        formError={formError}
        options={options}
        optionsUnavailable={unavailable}
        formKey={formKey}
        lookup={buildLookupProps({
          entityType: Entity.Books,
          available: isLookupAvailable(capabilities, Entity.Books),
          itemLabel: 'book',
          lookup,
          toValues: toBookValues,
          toSummary: toBookResultSummary,
        })}
      />
    </Document>,
    { status }
  );
}

// The form's other submit buttons: look metadata up, apply a result to the form, or close the dialog.
// None of them saves; each re-renders the form with what the user has entered so far.
function handleLookupIntent(context: AppContext, intent: string) {
  return resolveBookLookupIntent(context.formData, intent).then((state) => renderBookForm(context, state));
}

export default createController(routes.entityAdd.books, {
  actions: {
    index: (context) => renderBookForm(context),
    async action(context) {
      const rawIntent = context.formData.get('intent');
      const intent = typeof rawIntent === 'string' ? rawIntent : 'save';
      if (intent !== 'save') return handleLookupIntent(context, intent);

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
