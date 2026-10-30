import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { deleteEntity, getEntity } from '../../../api/entity-data.ts';
import { Entity, type BookEntity } from '../../../models.ts';
import type { AppContext } from '../../../router.ts';
import { routes } from '../../../routes.ts';
import { Document } from '../../../ui/document.tsx';
import { DeletePage } from '../../../ui/entity/delete/delete-page.tsx';
import { UnavailablePage } from '../../../ui/entity/unavailable-page.tsx';
import { serverLogger } from '../../../utils/serverLogger.ts';

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

const notFound = (context: AppContext) =>
  renderUnavailable(context, 404, 'Book not found', "We couldn't find that book.");

function renderConfirm(context: AppContext, book: BookEntity, id: string, formError?: string, status = 200) {
  return context.render(
    <Document path={context.url.pathname} title={`Delete ${book.title ?? 'Book'} | MediaSet`}>
      <DeletePage
        singular="Book"
        title={book.title ?? 'this book'}
        action={routes.entityDelete.books.action.href({ id })}
        cancelHref={routes.entityDetail.books.href({ id })}
        formError={formError}
      />
    </Document>,
    { status }
  );
}

export default createController(routes.entityDelete.books, {
  actions: {
    async index(context) {
      const { id } = context.params;
      try {
        return renderConfirm(context, await getEntity<BookEntity>(Entity.Books, id), id);
      } catch (error) {
        if (error instanceof Response && error.status === 404) return notFound(context);
        serverLogger.error('Failed to load book for deletion', error, { operation: 'deleteBook', id });
        return renderUnavailable(
          context,
          500,
          "Couldn't load this book",
          'Something went wrong while loading it. Please try again.'
        );
      }
    },
    async action(context) {
      const { id } = context.params;
      try {
        await deleteEntity(Entity.Books, id);
      } catch (error) {
        if (error instanceof Response && error.status === 404) return notFound(context);
        serverLogger.error('Failed to delete book', error, { operation: 'deleteBook', id });
        // Show the confirmation again with the error; the title comes from a fresh lookup.
        try {
          const book = await getEntity<BookEntity>(Entity.Books, id);
          return renderConfirm(context, book, id, 'The book could not be deleted. Please try again.', 500);
        } catch {
          return renderUnavailable(
            context,
            500,
            "Couldn't delete this book",
            'Something went wrong. Please try again.'
          );
        }
      }
      return redirect(routes.entityLists.books.href(), 303);
    },
  },
});
