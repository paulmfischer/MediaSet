import type { MetaFunction, LoaderFunctionArgs } from "@remix-run/node";
import { json } from "@remix-run/node";
import invariant from "tiny-invariant";
import { getBook } from "~/book-data";
import { Form, Link, useLoaderData } from "@remix-run/react";
import { IconEdit, IconTrash } from "@tabler/icons-react";

export const meta: MetaFunction = () => {
  return [
    { title: "Book Detail" },
    { name: "description", content: "Book Detail" },
  ];
};

export const loader = async ({ params }: LoaderFunctionArgs) => {
  invariant(params.bookId, "Missing bookId param");
  const book = await getBook(params.bookId);
  return json({ book });
};

export default function Detail() {
  const { book } = useLoaderData<typeof loader>();

  return (
    <div className="flex flex-col">
      <div className="flex flex-row items-center justify-between">
        <div className="flex flex-row gap-4">
          <h2 className="text-2xl">{book.title}</h2>
        </div>
        <div className="flex flex-row gap-2">
          <Link to={`/books/${book.id}/edit`} aria-label="Edit" title="Edit"><IconEdit /></Link>
          <Form action={`/books/${book.id}/delete`} method="post" onSubmit={(event) => {
            const response = confirm(`Are you sure you want to delete ${book.title}?`);
            if (!response) {
              event.preventDefault();
            }
          }}>
            <button className="link" type="submit" aria-label="Delete" title="Delete"><IconTrash size={22} /></button>
          </Form>
        </div>
      </div>
      <div className="h-full mt-4">
        <div className="flex gap-4">
          <label htmlFor="subtitle" className="dark:text-slate-400">Subtitle</label>
          <div id="subtitle">{book.subtitle}</div>
        </div>
        <div className="flex gap-4">
          <label htmlFor="format" className="dark:text-slate-400">Format</label>
          <div id="format">{book.format}</div>
        </div>
        <div className="flex gap-4">
          <label htmlFor="pages" className="dark:text-slate-400">Pages</label>
          <div id="pages">{book.pages}</div>
        </div>
        <div className="flex gap-4">
          <label htmlFor="publicationDate" className="dark:text-slate-400">Publication Date</label>
          <div id="publicationDate">{book.publicationDate}</div>
        </div>
        <div className="flex gap-4">
          <label htmlFor="authors" className="dark:text-slate-400">Authors</label>
          <div id="authors">{book.authors?.join(', ')}</div>
        </div>
        <div className="flex gap-4">
          <label htmlFor="genres" className="dark:text-slate-400">Genres</label>
          <div id="genres">{book.genres?.join(', ')}</div>
        </div>
        <div className="flex gap-4">
          <label htmlFor="publisher" className="dark:text-slate-400">Publisher</label>
          <div id="publisher">{book.publisher}</div>
        </div>
        <div className="flex gap-4">
          <label htmlFor="isbn" className="dark:text-slate-400">ISBN</label>
          <div id="isbn">{book.isbn}</div>
        </div>
        <div className="flex gap-4">
          <label htmlFor="plot" className="dark:text-slate-400">Plot</label>
          <div id="plot">{book.plot}</div>
        </div>
      </div>
    </div>
  );
}