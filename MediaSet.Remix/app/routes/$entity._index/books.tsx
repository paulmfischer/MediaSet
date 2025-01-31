import { Form, Link } from "@remix-run/react";
import { Pencil, Trash2 } from "lucide-react";
import { BookEntity } from "~/models";

type BooksProps = {
  books: BookEntity[]
};

export default function Books({ books }: BooksProps) {
  return (
    <table className="text-left w-full">
      <thead className="dark:bg-zinc-700 border-b-2 border-slate-600">
        <tr>
          <th className="pl-2 p-1 border-r border-slate-800 underline">Title</th>
          <th className="pl-2 p-1 border-r border-slate-800 underline">Authors</th>
          <th className="hidden sm:table-cell pl-2 p-1 border-r border-slate-800 underline">Format</th>
          <th className="hidden sm:table-cell pl-2 p-1 border-r border-slate-800 underline">Pages</th>
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
              <td className="hidden sm:table-cell pl-2 p-1 border-r border-slate-800">{book.format}</td>
              <td className="hidden sm:table-cell pl-2 p-1 border-r border-slate-800">{book.pages}</td>
              <td className="flex flex-row gap-3 p-1 pt-2">
                <Link to={`/books/${book.id}/edit`} aria-label="Edit" title="Edit"><Pencil size={18} /></Link>
                <Form action={`/books/${book.id}/delete`} method="post" onSubmit={(event) => {
                  const response = confirm(`Are you sure you want to delete ${book.title}?`);
                  if (!response) {
                    event.preventDefault();
                  }
                }}>
                  <button className="link" type="submit" aria-label="Delete" title="Delete"><Trash2 size={18} /></button>
                </Form>
              </td>
            </tr>
          )
        })}
      </tbody>
    </table>
  )
}