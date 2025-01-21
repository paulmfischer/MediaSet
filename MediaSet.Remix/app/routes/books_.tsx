import type { MetaFunction, LoaderFunctionArgs } from "@remix-run/node";
import { json } from "@remix-run/node";
import { searchBooks } from "~/book-data";
import { Form, Link, useLoaderData, useSubmit } from "@remix-run/react";
import { useEffect } from "react";
import { IconPlus, IconEdit, IconTrash, IconX } from "@tabler/icons-react";

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
          <h2 className="text-2xl">Books</h2>
        </div>
        <div className="flex flex-row gap-6 items-center">
          <Link to="/books/add" className="flex gap-1 items-center"><IconPlus size={22} />Add</Link>
          <Form id="search-form" role="search" className="flex gap-2">
            <div className="flex gap-2 z-20 bg-white rounded-sm">
              <input
                id="search"
                type="search"
                defaultValue={searchText || ''}
                placeholder="Search Books"
                aria-label="Search Books"
                name="searchText"
              />
              {searchText && 
                <button className="text-icon"
                  onClick={() => {
                    const searchEl = document.getElementById('search') as HTMLInputElement;
                    if (searchEl) {
                      searchEl.value = '';
                      submit(searchEl);
                    }
                  }}
                >
                  <IconX />
                </button>
              }
            </div>
            <button type="submit" aria-label="Search">Search</button>
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
                    <Link to={`/books/${book.id}`}>{book.title}{book.subtitle && `: ${book.subtitle}`}</Link>
                  </td>
                  <td className="pl-2 p-1 border-r border-slate-800">{book.authors?.map(auth => auth.trimEnd()).join(',')}</td>
                  <td className="pl-2 p-1 border-r border-slate-800">{book.format}</td>
                  <td className="pl-2 p-1 border-r border-slate-800">{book.pages}</td>
                  <td className="flex flex-row gap-3 p-1">
                    <Link to={`/books/${book.id}/edit`} aria-label="Edit" title="Edit"><IconEdit size={22} /></Link>
                    <Form action={`/books/${book.id}/delete`} method="post" onSubmit={(event) => {
                      const response = confirm(`Are you sure you want to delete ${book.title}?`);
                      if (!response) {
                        event.preventDefault();
                      }
                    }}>
                      <button className="link" type="submit" aria-label="Delete" title="Delete"><IconTrash size={22} /></button>
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