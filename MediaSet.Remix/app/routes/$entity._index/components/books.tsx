import { Link } from '@remix-run/react';
import { Pencil, Trash2 } from 'lucide-react';
import { useState } from 'react';
import DeleteDialog from '~/components/delete-dialog';
import ImageDisplay from '~/components/image-display';
import SortableColumnHeader from '~/components/sortable-column-header';
import { BookEntity, Entity } from '~/models';

type BooksProps = {
  books: BookEntity[];
  apiUrl?: string;
  orderBy?: string;
  searchText?: string | null;
};

export default function Books({ books, apiUrl, orderBy = 'title:asc', searchText }: BooksProps) {
  const [deleteDialogState, setDeleteDialogState] = useState<{ isOpen: boolean; book: BookEntity | null }>({
    isOpen: false,
    book: null,
  });

  return (
    <>
      <div className="md:hidden flex flex-col divide-y divide-slate-700">
        {books.map((book) => (
          <div key={book.id} className="flex flex-row items-center gap-3 py-2 px-2 dark:hover:bg-zinc-800">
            {book.coverImage && (
              <div className="flex-shrink-0">
                <ImageDisplay
                  imageData={book.coverImage}
                  alt={`${book.title} cover`}
                  entityType={Entity.Books}
                  entityId={book.id}
                  apiUrl={apiUrl}
                  size="xsmall"
                />
              </div>
            )}
            <div className="flex-1 min-w-0">
              <Link to={`/books/${book.id}`} className="font-medium block truncate">
                {book.title}
                {book.subtitle && `: ${book.subtitle}`}
              </Link>
              <div className="text-xs text-gray-400 truncate">
                {[book.authors?.map((a) => a.trimEnd()).join(', '), book.format].filter(Boolean).join(' · ')}
              </div>
            </div>
            <div className="flex flex-row gap-3 flex-shrink-0">
              <Link to={`/books/${book.id}/edit`} aria-label="Edit" title="Edit">
                <Pencil size={18} />
              </Link>
              <button
                onClick={() => setDeleteDialogState({ isOpen: true, book })}
                className="link"
                type="button"
                aria-label="Delete"
                title="Delete"
              >
                <Trash2 size={18} />
              </button>
            </div>
          </div>
        ))}
      </div>
      <table className="hidden md:table text-left w-full">
        <thead className="dark:bg-zinc-700 border-b-2 border-slate-600">
          <tr>
            <th className="hidden md:table-cell pl-2 p-1 border-r border-slate-800">Cover</th>
            <SortableColumnHeader label="Title" field="title" currentOrderBy={orderBy} searchText={searchText} />
            <SortableColumnHeader
              label="Authors"
              field="authors"
              currentOrderBy={orderBy}
              searchText={searchText}
              className="hidden xs:table-cell"
            />
            <SortableColumnHeader
              label="Format"
              field="format"
              currentOrderBy={orderBy}
              searchText={searchText}
              className="hidden md:table-cell"
            />
            <SortableColumnHeader
              label="Pages"
              field="pages"
              currentOrderBy={orderBy}
              searchText={searchText}
              className="hidden lg:table-cell"
            />
            <th className="w-1"></th>
          </tr>
        </thead>
        <tbody>
          {books.map((book) => {
            return (
              <tr className="border-b border-slate-700 dark:hover:bg-zinc-800" key={book.id}>
                <td className="hidden md:table-cell w-16 pl-2 p-1 border-r border-slate-800">
                  {book.coverImage && (
                    <ImageDisplay
                      imageData={book.coverImage}
                      alt={`${book.title} cover`}
                      entityType={Entity.Books}
                      entityId={book.id}
                      apiUrl={apiUrl}
                      size="xsmall"
                    />
                  )}
                </td>
                <td className="pl-2 p-1 border-r border-slate-800">
                  <Link to={`/books/${book.id}`}>
                    {book.title}
                    {book.subtitle && `: ${book.subtitle}`}
                  </Link>
                </td>
                <td className="hidden xs:table-cell pl-2 p-1 border-r border-slate-800">
                  {book.authors?.map((auth) => auth.trimEnd()).join(',')}
                </td>
                <td className="hidden md:table-cell pl-2 p-1 border-r border-slate-800">{book.format}</td>
                <td className="hidden lg:table-cell pl-2 p-1 border-r border-slate-800">{book.pages}</td>
                <td className="flex flex-row gap-3 p-1 pt-2">
                  <Link to={`/books/${book.id}/edit`} aria-label="Edit" title="Edit">
                    <Pencil size={18} />
                  </Link>
                  <button
                    onClick={() => setDeleteDialogState({ isOpen: true, book })}
                    className="link"
                    type="button"
                    aria-label="Delete"
                    title="Delete"
                  >
                    <Trash2 size={18} />
                  </button>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
      <DeleteDialog
        isOpen={deleteDialogState.isOpen}
        onClose={() => setDeleteDialogState({ isOpen: false, book: null })}
        entityTitle={deleteDialogState.book?.title}
        deleteAction={deleteDialogState.book ? `/books/${deleteDialogState.book.id}/delete` : ''}
      />
    </>
  );
}
