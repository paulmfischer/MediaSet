import type { MetaFunction, ActionFunctionArgs } from "@remix-run/node";
import { Form, redirect, useNavigate } from "@remix-run/react";
import { addBook } from "~/book-data";

export const meta: MetaFunction = () => {
  return [
    { title: "Add a Book" },
    { name: "description", content: "Add a book" },
  ];
};

export const action = async ({ request }: ActionFunctionArgs) => {
  const formData = await request.formData();
  const formBook = Object.fromEntries(formData);
  const newBook = await addBook(formBook);

  return redirect(`/books/${newBook.id}`);
};

export default function Edit() {
  const navigate = useNavigate();

  return (
    <div className="flex flex-col">
      <div className="flex flex-row items-center justify-between">
        <div className="flex flex-row gap-4 items-end">
          <h2 className="text-2xl">Add a Book</h2>
        </div>
      </div>
      <div className="h-full mt-4">
        <div className="mt-4 flex flex-col gap-2">
          <Form id="edit-book" method="post" className="flex flex-col gap-2">
            <label htmlFor="title" className="dark:text-slate-400">Title</label>
            <input id="title" name="title" type="text" placeholder="Title" aria-label="Title" />
            <label htmlFor="subtitle" className="dark:text-slate-400">Subtitle</label>
            <input id="subtitle" name="subtitle" type="text" placeholder="Subtitle" aria-label="Subtitle" />
            <label htmlFor="format" className="dark:text-slate-400">Format</label>
            <input id="format" name="format" type="text" placeholder="Format" aria-label="Format" />
            <label htmlFor="pages" className="dark:text-slate-400">Pages</label>
            <input id="pages" name="pages" type="number" placeholder="Pages" aria-label="Pages" />
            <label htmlFor="publicationDate" className="dark:text-slate-400">Publication Date</label>
            <input id="publicationDate" name="publicationDate" type="text" placeholder="Publication Date" aria-label="Publication Date" />
            <label htmlFor="isbn" className="dark:text-slate-400">ISBN</label>
            <input id="isbn" name="isbn" type="text" placeholder="ISBN" aria-label="ISBN" />
            <label htmlFor="plot" className="dark:text-slate-400">Plot</label>
            <textarea id="plot" name="plot" placeholder="Plot" aria-label="Plot" />
            <div className="flex flex-row gap-2 mt-3">
              <button type="submit">Add</button>
              <button type="button" onClick={() => navigate(-1)}>Cancel</button>
            </div>
          </Form>
        </div>
      </div>
    </div>
  );
}