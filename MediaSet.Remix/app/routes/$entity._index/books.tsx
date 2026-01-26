import { Link } from "@remix-run/react";
import { Pencil, Trash2 } from "lucide-react";
import { useState } from "react";
import DeleteDialog from "~/components/delete-dialog";
import ImageDisplay from "~/components/image-display";
import { BookEntity, Entity } from "~/models";

type BooksProps = {
  books: BookEntity[];
  apiUrl?: string;
};

export default function Books({ books, apiUrl }: BooksProps) {
  const [deleteDialogState, setDeleteDialogState] = useState<{ isOpen: boolean; book: BookEntity | null }>({
    isOpen: false,
    book: null
  });

  return (
    <>
      <table className="text-left w-full">
        <thead className="dark:bg-zinc-700 border-b-2 border-slate-600">
          <tr>
            <th className="hidden md:table-cell pl-2 p-1 border-r border-slate-800 underline">Cover</th>
            <th className="pl-2 p-1 border-r border-slate-800 underline">Title</th>
            <th className="hidden xs:table-cell pl-2 p-1 border-r border-slate-800 underline">Authors</th>
            <th className="hidden md:table-cell pl-2 p-1 border-r border-slate-800 underline">Format</th>
            <th className="hidden lg:table-cell pl-2 p-1 border-r border-slate-800 underline">Pages</th>
            <th className="w-1"></th>
          </tr>
        </thead>
        <tbody>
          {books.map(book => {
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
                  <Link to={`/books/${book.id}`}>{book.title}{book.subtitle && `: ${book.subtitle}`}</Link>
                </td>
                <td className="hidden xs:table-cell pl-2 p-1 border-r border-slate-800">{book.authors?.map(auth => auth.trimEnd()).join(',')}</td>
                <td className="hidden md:table-cell pl-2 p-1 border-r border-slate-800">{book.format}</td>
                <td className="hidden lg:table-cell pl-2 p-1 border-r border-slate-800">{book.pages}</td>
                <td className="flex flex-row gap-3 p-1 pt-2">
                  <Link to={`/books/${book.id}/edit`} aria-label="Edit" title="Edit"><Pencil size={18} /></Link>
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
            )
          })}
        </tbody>
      </table>
      <DeleteDialog
        isOpen={deleteDialogState.isOpen}
        onClose={() => setDeleteDialogState({ isOpen: false, book: null })}
        entityTitle={deleteDialogState.book?.title}
        deleteAction={deleteDialogState.book ? `/books/${deleteDialogState.book.id}/delete` : ""}
      />
    </>
  )
}