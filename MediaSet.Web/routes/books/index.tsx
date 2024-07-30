import { Handlers, PageProps } from "$fresh/server.ts";
import { MediaHeader } from "../../components/MediaHeader.tsx";
import { baseUrl } from "../../constants.ts";
import { Book } from "../../models.ts";

export const handler: Handlers<Array<Book>> = {
  async GET(_req, ctx) {
    const response = await fetch(`${baseUrl}/books`, {
        headers: {
            accept: "application/json",
        }
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
          <ul>
              {props.data.map((book: Book) => (
                  <li key={book.title} className="flex gap-4 text-3xl lg:text-lg">
                      <div className="text-blue-600 dark:text-blue-400">
                          {/* <a href={`/books/${book.id}`}> */}
                          {book.title}
                          {/* </a> */}
                      </div>
                      <div>{book.author}</div>
                  </li>
              ))}
          </ul>
      </div>
    </div>
  );
}