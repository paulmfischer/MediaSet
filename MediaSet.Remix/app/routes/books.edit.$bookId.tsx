import type { MetaFunction, LoaderFunctionArgs, ActionFunctionArgs } from "@remix-run/node";
import { Form, redirect, useLoaderData, useNavigate } from "@remix-run/react";
import invariant from "tiny-invariant";
import { json } from "@remix-run/node";
import { getBook, updatebook } from "~/book-data";

export const meta: MetaFunction = () => {
  return [
    { title: "Book Edit" },
    { name: "description", content: "Edit a book" },
  ];
};

export const loader = async ({ params }: LoaderFunctionArgs) => {
  invariant(params.bookId, "Missing bookId param");
  const book = await getBook(params.bookId);
  return json({ book });
}

export const action = async ({ params, request }: ActionFunctionArgs) => {
  invariant(params.bookId, "Missing bookId param");
  const formData = await request.formData();
  const updates = Object.fromEntries(formData);
  updates.id = params.bookId;
  await updatebook(params.bookId, updates);
  console.log('redirecting....');

  return redirect(`/books/${params.bookId}`);
};

export default function Edit() {
  const { book } = useLoaderData<typeof loader>();
  const navigate = useNavigate();

  return (
    <div className="flex flex-col">
      <div className="flex flex-row items-center justify-between">
        <div className="flex flex-row gap-4 items-end">
          <h2 className="text-2xl">Editing {book.title}</h2>
        </div>
      </div>
      <div className="h-full mt-4">
        <div className="mt-4 flex flex-col gap-2">
          <Form key={book.id} id="edit-book" method="post" className="flex flex-col gap-2">
            <label htmlFor="title" className="dark:text-slate-400">Title</label>
            <input id="title" defaultValue={book.title} name="title" type="text" placeholder="Title" aria-label="Title" />
            <label htmlFor="subtitle" className="dark:text-slate-400">Subtitle</label>
            <input id="subtitle" defaultValue={book.subtitle} name="subtitle" type="text" placeholder="Subtitle" aria-label="Subtitle" />
            <label htmlFor="format" className="dark:text-slate-400">Format</label>
            <input id="format" defaultValue={book.format} name="format" type="text" placeholder="Format" aria-label="Format" />
            <label htmlFor="pages" className="dark:text-slate-400">Pages</label>
            <input id="pages" defaultValue={book.pages} name="pages" type="number" placeholder="Pages" aria-label="Pages" />
            <label htmlFor="publicationDate" className="dark:text-slate-400">Publication Date</label>
            <input id="publicationDate" defaultValue={book.publicationDate} name="publicationDate" type="text" placeholder="Publication Date" aria-label="Publication Date" />
            <label htmlFor="isbn" className="dark:text-slate-400">ISBN</label>
            <input id="isbn" defaultValue={book.isbn} name="isbn" type="text" placeholder="ISBN" aria-label="ISBN" />
            <label htmlFor="plot" className="dark:text-slate-400">Plot</label>
            <textarea id="plot" defaultValue={book.plot} name="plot" placeholder="Plot" aria-label="Plot" />
            <div className="flex flex-row gap-2 mt-3">
              <button type="submit">Update</button>
              <button type="button" onClick={() => navigate(-1)}>Cancel</button>
            </div>
          </Form>
        </div>
      </div>
    </div>
  );
}