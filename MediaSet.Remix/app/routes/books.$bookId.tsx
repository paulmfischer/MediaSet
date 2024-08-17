import type { MetaFunction, LoaderFunctionArgs } from "@remix-run/node";
import { json } from "@remix-run/node";
import invariant from "tiny-invariant";
import { getBook } from "~/book-data";
import { Link, useLoaderData } from "@remix-run/react";
import { IconEdit } from "@tabler/icons-react";

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
}

export default function Detail() {
  const { book } = useLoaderData<typeof loader>();

  return (
    <div className="flex flex-col">
      <div className="flex flex-row items-center justify-between">
        <div className="flex flex-row gap-4">
          <h2 className="text-2xl">{book.title}</h2>
        </div>
        <Link to={`/books/${book.id}/edit`} aria-label="Edit" title="Edit"><IconEdit /></Link>
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
          <label htmlFor="author" className="dark:text-slate-400">Author</label>
          <div id="author">{book.author?.join(',')}</div>
        </div>
        <div className="flex gap-4">
          <label htmlFor="genre" className="dark:text-slate-400">Genre</label>
          <div id="genre">{book.genre?.join(',')}</div>
        </div>
        <div className="flex gap-4">
          <label htmlFor="publisher" className="dark:text-slate-400">Publisher</label>
          <div id="publisher">{book.publisher?.join(',')}</div>
        </div>
        <div className="flex gap-4">
          <label htmlFor="plot" className="dark:text-slate-400">Plot</label>
          <div id="plot">{book.plot}</div>
        </div>
      </div>
    </div>
  );
}