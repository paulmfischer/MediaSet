import { json } from "@remix-run/node";
import { searchBooks } from "~/book-data";
import { Link, useLoaderData } from "@remix-run/react";

import type { MetaFunction } from "@remix-run/node";

export const meta: MetaFunction = () => {
  return [
    { title: "Books" },
    { name: "description", content: "List of books" },
  ];
};

export async function loader() {
  return json(await searchBooks());
}

export default function Index() {
  const books = useLoaderData<typeof loader>();

  return (
    <div className="flex flex-col">
      <div className="flex flex-row items-center justify-between">
        <div className="flex flex-row gap-4 items-end">
          Books!
        </div>
        <div className="flex flex-row gap-4">
          <input placeholder="Search" className="p-1 pl-2 dark:text-slate-800" />
        </div>
      </div>
      <div className="h-full mt-4">
        <table className="text-left w-full">
          <thead className="dark:bg-zinc-700 border-b-2 border-slate-600">
            <tr>
              <th className="pl-2 p-1 border-r border-slate-800 underline">Title</th>
              <th className="pl-2 p-1 border-r border-slate-800 underline">Subtitle</th>
              <th className="pl-2 p-1 border-r border-slate-800 underline">Format</th>
              <th className="pl-2 p-1 border-r border-slate-800 underline">Authors</th>
              <th className="pl-2 p-1 border-r border-slate-800 underline">Pages</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {books.map(book => {
              return (
                <tr className="border-b border-slate-700 dark:hover:bg-zinc-800" key={book.id}>
                  <td className="pl-2 p-1 border-r border-slate-800">{book.title}</td>
                  <td className="pl-2 p-1 border-r border-slate-800">{book.subTitle}</td>
                  <td className="pl-2 p-1 border-r border-slate-800">{book.format}</td>
                  <td className="pl-2 p-1 border-r border-slate-800">{book.author?.join(',')}</td>
                  <td className="pl-2 p-1 border-r border-slate-800">{book.pages}</td>
                  <td className="flex flex-row gap-2 p-1 ">
                    <Link to="/edit">Edit</Link>
                    <Link to="/delete">Delete</Link>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
};