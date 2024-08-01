import { Handlers, PageProps } from "$fresh/server.ts";
import { Button } from "../../components/Button.tsx";
import { MediaHeader } from "../../components/MediaHeader.tsx";
import { baseUrl } from "../../constants.ts";
import { Book } from "../../models.ts";

interface BooksProps {
  message?: string;
  searchText?: string;
  books?: Array<Book>;
  failed?: boolean;
}

export const handler: Handlers<BooksProps> = {
  async GET(_req, ctx) {
    const response = await fetch(`${baseUrl}/books`, {
      headers: {
        accept: "application/json",
      },
    });

    if (!response) {
      return ctx.renderNotFound();
    }

    const books = await response.json();
    return ctx.render({ books });
  },
  async POST(req, ctx) {
    const form = await req.formData();
    const searchText = form.get("searchText");
    if (!searchText) {
      return ctx.render({ message: "Search text required" });
    }

    const response = await fetch(
      `${baseUrl}/books/search?searchText=${searchText.toString()}`,
    );
    if (!response.ok) {
      return ctx.render({ failed: true });
    }

    const books = await response.json();
    return ctx.render({ books, searchText: searchText.toString() });
  },
};

export default function Books(props: PageProps<BooksProps>) {
  const books = props.data.books;
  const searchText = props.data.searchText;
  const message = props.data.message;
  const failed = props.data.failed;
  return (
    <div>
      <div className="flex items-center justify-between">
        <MediaHeader title="Books" />
        <a
          href="/books/upload"
          className="text-3xl lg:text-lg text-blue-600 dark:text-blue-400"
        >
          Upload
        </a>
      </div>
      <form method="post" className="flex gap-4 mt-2">
        <input
          name="searchText"
          value={searchText}
          className="dark:text-slate-800"
        />
        {message && <div>{message}</div>}
        <Button type="submit">Search</Button>
      </form>
      <div className="mt-2">
        {failed &&
          <div>Failed to load books, reload to try again.</div>}
        {!failed &&
          (
            <>
              You have {books?.length} books.
              <table className="table-auto border-spacing-x-2">
                <thead>
                  <tr>
                    <th className="text-left">Title</th>
                    <th className="text-left">Author</th>
                    <th className="text-left">Pages</th>
                  </tr>
                </thead>
                <tbody>
                  {books?.map((book: Book) => (
                    <tr>
                      <td>
                        <a
                          className="text-blue-600 dark:text-blue-400"
                          href={`/books/${book.id}`}
                        >
                          {book.title}
                        </a>
                      </td>
                      <td>{book.author.join(',')}</td>
                      <td>{book.pages}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </>
          )}
      </div>
    </div>
  );
}
