import { Handlers, PageProps } from "$fresh/server.ts";
import { MediaHeader } from "../../components/MediaHeader.tsx";
import { baseUrl } from "../../constants.ts";
import { Book } from "../../models.ts";

export const handler: Handlers<Array<Book>> = {
  async GET(_req, ctx) {
    const response = await fetch(`${baseUrl}/books`, {
      headers: {
        accept: "application/json",
      },
    });
    // console.log('did we get books response?', response);
    if (!response) {
      return ctx.renderNotFound();
    }
    const books = await response.json();
    // console.log('what are the actual books?', books);
    return ctx.render(books);
  },
};

export default function Books(props: PageProps<Array<Book>>) {
  return (
    <div>
      <div className="flex items-center justify-between">
        <MediaHeader title="Books" />
        <a href="/books/upload" className="dark:text-slate-400">Upload</a>
      </div>
      You have {props.data.length} books.
      <div className="mt-2">
        <table className="table-auto">
          <thead>
            <tr>
              <th className="text-left">Title</th>
              <th className="text-left">Author</th>
              <th className="text-left">Pages</th>
            </tr>
          </thead>
          <tbody>
            {props.data.map((book: Book) => (
              <tr>
                <td>
                  <a
                    className="text-blue-600 dark:text-blue-400"
                    href={`/books/${book.id}`}
                  >
                    {book.title}
                  </a>
                </td>
                <td>{book.author}</td>
                <td>{book.pages}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
