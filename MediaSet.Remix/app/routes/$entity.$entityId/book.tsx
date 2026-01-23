import { Link } from "@remix-run/react";
import { Pencil, Trash2 } from "lucide-react";
import { useState } from "react";
import DeleteDialog from "~/components/delete-dialog";
import ImageDisplay from "~/components/image-display";
import { BookEntity, Entity } from "~/models";

type BookProps = {
  book: BookEntity;
};

export default function Book({ book }: BookProps) {
  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);

  return (
    <div className="flex flex-col">
      <div className="flex flex-row items-center justify-between border-b-2 border-slate-600 pb-2">
        <div className="flex flex-col gap-2 items-baseline">
          <h2 className="text-2xl">{book.title}</h2>
          {book.subtitle && <h4 className="text-sm">{book.subtitle}</h4>}
        </div>
        <div className="flex flex-row gap-2">
          <Link to={`/books/${book.id}/edit`} aria-label="Edit" title="Edit">
            <Pencil size={22} />
          </Link>
          <button
            onClick={() => setIsDeleteDialogOpen(true)}
            className="link"
            type="button"
            aria-label="Delete"
            title="Delete"
          >
            <Trash2 size={22} />
          </button>
        </div>
      </div>
      <div className="h-full mt-4">
        <div className="flex flex-col lg:flex-row lg:gap-3">
          <div className="lg:basis-1/5 flex justify-center mb-4 lg:mb-0">
            <ImageDisplay
              imageData={book.coverImage}
              alt={book.title ?? "Book cover"}
              entityType={Entity.Books}
              entityId={book.id}
            />
          </div>
          <div className="lg:basis-4/5">
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="format" className="basis-2/12 dark:text-slate-400">
                Format
              </label>
              <div id="format" className="grow">
                {book.format}
              </div>
            </div>
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="pages" className="basis-2/12 dark:text-slate-400">
                Pages
              </label>
              <div id="pages" className="grow">
                {book.pages}
              </div>
            </div>
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="publicationDate" className="basis-2/12 dark:text-slate-400">
                Publication Date
              </label>
              <div id="publicationDate" className="grow">
                {book.publicationDate}
              </div>
            </div>
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="authors" className="basis-2/12 dark:text-slate-400">
                Authors
              </label>
              <div id="authors" className="grow">
                {book.authors?.join(", ")}
              </div>
            </div>
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="genres" className="basis-2/12 dark:text-slate-400">
                Genres
              </label>
              <div id="genres" className="grow">
                {book.genres?.join(", ")}
              </div>
            </div>
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="publisher" className="basis-2/12 dark:text-slate-400">
                Publisher
              </label>
              <div id="publisher" className="grow">
                {book.publisher}
              </div>
            </div>
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="isbn" className="basis-2/12 dark:text-slate-400">
                ISBN
              </label>
              <div id="isbn" className="grow">
                {book.isbn}
              </div>
            </div>
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="plot" className="basis-2/12 dark:text-slate-400">
                Plot
              </label>
              <div id="plot" className="basis-3/4">
                {book.plot}
              </div>
            </div>
          </div>
        </div>
      </div>
      <DeleteDialog
        isOpen={isDeleteDialogOpen}
        onClose={() => setIsDeleteDialogOpen(false)}
        entityTitle={book.title}
        deleteAction={`/books/${book.id}/delete`}
      />
    </div>
  );
}