import { Form, useLoaderData } from "@remix-run/react";
import invariant from "tiny-invariant";
import { json } from "@remix-run/node";

import type { MetaFunction, LoaderFunctionArgs } from "@remix-run/node";
import { getBook } from "~/book-data";

export const meta: MetaFunction = () => {
  return [
    { title: "Book Edit" },
    { name: "description", content: "Edit a book" },
  ];
};

export const loader = async({ params }: LoaderFunctionArgs) => {
  invariant(params.bookId, "Missing bookId param");
  const book = await getBook(params.bookId);
  return json({ book });
}

export default function Edit() {
  const { book } = useLoaderData<typeof loader>();

  return (
    <div className="flex flex-col">
      <div className="flex flex-row items-center justify-between">
        <div className="flex flex-row gap-4 items-end">
          Book edit!
        </div>
        <div className="flex flex-row gap-4">
          <input placeholder="Search" className="p-1 pl-2 dark:text-slate-800" />
        </div>
      </div>
      <div className="h-full mt-4">
        <div className="mt-4 flex flex-col gap-2">
          <Form key={book.id} id="edit-book" method="post">
            <input defaultValue={book.title} aria-label="Title" name="title" type="text" placeholder="Title" />
          </Form>
        </div>
      </div>
    </div>
  );
}