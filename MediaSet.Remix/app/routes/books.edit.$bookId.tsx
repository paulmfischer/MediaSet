import type { MetaFunction, LoaderFunctionArgs, ActionFunctionArgs } from "@remix-run/node";
import { Form, redirect, useLoaderData } from "@remix-run/react";
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

  return (
    <div className="flex flex-col">
      <div className="flex flex-row items-center justify-between">
        <div className="flex flex-row gap-4 items-end">
          Editing {book.title}
        </div>
        <div className="flex flex-row gap-4">
          <input placeholder="Search" className="p-1 pl-2 dark:text-slate-800" />
        </div>
      </div>
      <div className="h-full mt-4">
        <div className="mt-4 flex flex-col gap-2">
          <Form key={book.id} id="edit-book" method="post" className="flex flex-col gap-2">
            <input defaultValue={book.title} name="title" type="text" placeholder="Title" aria-label="Title" />
            <input defaultValue={book.subTitle} name="subTitle" type="text" placeholder="Subtitle" aria-label="Subtitle" />
            <input defaultValue={book.format} name="format" type="text" placeholder="Format" aria-label="Format" />
            <input defaultValue={book.pages} name="pages" type="number" placeholder="Pages" aria-label="Pages" />
            <input defaultValue={book.publicationDate} name="publicationDate" type="text" placeholder="Publication Date" aria-label="Publication Date" />
            <input defaultValue={book.isbn} name="isbn" type="text" placeholder="ISBN" aria-label="ISBN" />
            <input defaultValue={book.plot} name="plot" type="text" placeholder="Plot" aria-label="Plot" />
            <div className="flex flex-row gap-2">
              <button type="submit">Update</button>
              <button type="button">Cancel</button>
            </div>
          </Form>
        </div>
      </div>
    </div>
  );
}