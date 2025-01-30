import type { MetaFunction, LoaderFunctionArgs } from "@remix-run/node";
import { json } from "@remix-run/node";
import invariant from "tiny-invariant";
import { Form, Link, useLoaderData } from "@remix-run/react";
import { Pencil, Trash2 } from "lucide-react";
import { get } from "~/entity-data";
import { Entities } from "~/constants";
import { BookEntity } from "~/models";

export const meta: MetaFunction = () => {
  return [
    { title: "Book Detail" },
    { name: "description", content: "Book Detail" },
  ];
};

export const loader = async ({ params }: LoaderFunctionArgs) => {
  invariant(params.bookId, "Missing bookId param");
  const book = await get<BookEntity>(Entities.Books, params.bookId);
  return json({ book });
};

export default function Detail() {
  const { book } = useLoaderData<typeof loader>();

  return (
    <div className="flex flex-col">
      <div className="flex flex-row items-center justify-between border-b-2 border-slate-600 pb-2">
        <div className="flex flex-col gap-2 items-baseline">
          <h2 className="text-2xl">{book.title}</h2>
          {book.subtitle && <h4 className="text-sm">{book.subtitle}</h4>}
        </div>
        <div className="flex flex-row gap-2">
          <Link to={`/books/${book.id}/edit`} aria-label="Edit" title="Edit"><Pencil size={22} /></Link>
          <Form action={`/books/${book.id}/delete`} method="post" onSubmit={(event) => {
            const response = confirm(`Are you sure you want to delete ${book.title}?`);
            if (!response) {
              event.preventDefault();
            }
          }}>
            <button className="link" type="submit" aria-label="Delete" title="Delete"><Trash2 size={22} /></button>
          </Form>
        </div>
      </div>
      <div className="h-full mt-4">
        <div className="flex flex-col md:flex-row mb-2 md:mb-0">
          <label htmlFor="format" className="basis-2/12 dark:text-slate-400">Format</label>
          <div id="format" className="grow">{book.format}</div>
        </div>
        <div className="flex flex-col md:flex-row mb-2 md:mb-0">
          <label htmlFor="pages" className="basis-2/12 dark:text-slate-400">Pages</label>
          <div id="pages" className="grow">{book.pages}</div>
        </div>
        <div className="flex flex-col md:flex-row mb-2 md:mb-0">
          <label htmlFor="publicationDate" className="basis-2/12 dark:text-slate-400">Publication Date</label>
          <div id="publicationDate" className="grow">{book.publicationDate}</div>
        </div>
        <div className="flex flex-col md:flex-row mb-2 md:mb-0">
          <label htmlFor="authors" className="basis-2/12 dark:text-slate-400">Authors</label>
          <div id="authors" className="grow">{book.authors?.join(', ')}</div>
        </div>
        <div className="flex flex-col md:flex-row mb-2 md:mb-0">
          <label htmlFor="genres" className="basis-2/12 dark:text-slate-400">Genres</label>
          <div id="genres" className="grow">{book.genres?.join(', ')}</div>
        </div>
        <div className="flex flex-col md:flex-row mb-2 md:mb-0">
          <label htmlFor="publisher" className="basis-2/12 dark:text-slate-400">Publisher</label>
          <div id="publisher" className="grow">{book.publisher}</div>
        </div>
        <div className="flex flex-col md:flex-row mb-2 md:mb-0">
          <label htmlFor="isbn" className="basis-2/12 dark:text-slate-400">ISBN</label>
          <div id="isbn" className="grow">{book.isbn}</div>
        </div>
        <div className="flex flex-col md:flex-row mb-2 md:mb-0">
          <label htmlFor="plot" className="basis-2/12 dark:text-slate-400">Plot</label>
          <div id="plot" className="basis-3/4">{book.plot}</div>
        </div>
      </div>
    </div>
  );
}