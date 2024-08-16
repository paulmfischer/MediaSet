import type { MetaFunction, LoaderFunctionArgs } from "@remix-run/node";
import { json } from "@remix-run/node";
import { searchBooks } from "~/book-data";
import { Form, Link, useLoaderData, useSubmit } from "@remix-run/react";
import { useEffect } from "react";

export const meta: MetaFunction = () => {
  return [
    { title: "Books" },
    { name: "description", content: "List of books" },
  ];
};

export const loader = async ({ request }: LoaderFunctionArgs) => {
  const url = new URL(request.url);
  const searchText = url.searchParams.get("searchText");
  const books = await searchBooks(searchText);
  return json({ books, searchText });
};

export default function Index() {
  const { books, searchText } = useLoaderData<typeof loader>();
  const submit = useSubmit();

  useEffect(() => {
    const searchField = document.getElementById("search");
    if (searchField instanceof HTMLInputElement) {
      searchField.value = searchText || '';
    }
  }, [searchText]);

  return (
    <div className="flex flex-col">
      <div className="flex flex-row items-center justify-between">
        <div className="flex flex-row gap-4 items-end">
          Books!
        </div>
        <div className="flex flex-row gap-4 items-center">
          <Link to="/books/add">Add</Link>
          <Form id="search-form" role="search" onChange={(event) => {
            const isFirstSearch = searchText === null;
            submit(event.currentTarget, { replace: !isFirstSearch });
          }}>
            <input id="search" defaultValue={searchText || ''} placeholder="Search Books" aria-label="Search Books" type="search" name="searchText" />
          </Form>
        </div>
      </div>
      <div className="h-full mt-4">
        <table className="text-left w-full">
          <thead className="dark:bg-zinc-700 border-b-2 border-slate-600">
            <tr>
              <th className="pl-2 p-1 border-r border-slate-800 underline">Title</th>
              <th className="pl-2 p-1 border-r border-slate-800 underline">Authors</th>
              <th className="pl-2 p-1 border-r border-slate-800 underline">Format</th>
              <th className="pl-2 p-1 border-r border-slate-800 underline">Pages</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {books.map(book => {
              return (
                <tr className="border-b border-slate-700 dark:hover:bg-zinc-800" key={book.id}>
                  <td className="pl-2 p-1 border-r border-slate-800">
                    <Link to={`/books/${book.id}`}>{book.title}</Link>{book.subtitle && `: ${book.subtitle}`}
                  </td>
                  <td className="pl-2 p-1 border-r border-slate-800">{book.author?.map(auth => auth.trimEnd()).join(',')}</td>
                  <td className="pl-2 p-1 border-r border-slate-800">{book.format}</td>
                  <td className="pl-2 p-1 border-r border-slate-800">{book.pages}</td>
                  <td className="flex flex-row gap-2 p-1 ">
                    <Link to={`/books/edit/${book.id}`}>Edit</Link>
                    <Form action={`destroy/${book.id}`} method="post" onSubmit={(event) => {
                      const response = confirm(`Are you sure you want to delete ${book.title}?`);
                      if (!response) {
                        event.preventDefault();
                      }
                    }}>
                      <button className="link" type="submit">Delete</button>
                    </Form>
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