import { Handlers, PageProps } from "$fresh/server.ts";
import { Anchor } from "../../components/Anchor.tsx";
import { Button } from "../../components/Button.tsx";
import { MediaHeader } from "../../components/MediaHeader.tsx";
import { baseUrl } from "../../constants.ts";
import { Book } from "../../models.ts";

interface BooksProps {
  searchText: string;
  orderBy: string;
  books?: Array<Book>;
  failed?: boolean;
}

export const handler: Handlers<BooksProps> = {
  async GET(req, ctx) {
    const url = new URL(req.url);
    const searchText = url.searchParams.get('searchText') || '';
    const orderBy = url.searchParams.get('orderBy') || '';

    const response = await fetch(
      `${baseUrl}/books/search?searchText=${searchText.toString()}&orderBy=${orderBy.toString()}`,
    );
    if (!response.ok) {
      return ctx.render({ failed: true, searchText, orderBy });
    }

    const books = await response.json();
    return ctx.render({ books, searchText: searchText.toString(), orderBy });
  }
};

export default function Books(props: PageProps<BooksProps>) {
  const books = props.data.books;
  const searchText = props.data.searchText;
  const orderBy = props.data.orderBy;
  const failed = props.data.failed;
  const orderByUrl = searchText ? `/books?searchText=${searchText}&orderBy={0}` : '/books?orderBy={0}';
  return (
    <div>
      <div className="flex items-center justify-between">
        <MediaHeader title="Books" />
        <Anchor
          href="/books/upload"
          className="text-3xl lg:text-lg text-blue-600 dark:text-blue-400"
        >
          Upload
        </Anchor>
      </div>
      <form className="flex gap-4 mt-2">
        <input
          name="searchText"
          value={searchText}
          className="dark:text-slate-800"
        />
        <input name="orderBy" value={orderBy} hidden="hidden" />
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
                    <th className="text-left">
                      {(orderBy == '' || orderBy.includes('title:asc'))
                      ? <Anchor href={orderByUrl.replace('{0}', 'title:desc')}>Title (asc)</Anchor>
                      : <Anchor href={orderByUrl.replace('{0}', 'title:asc')}>Title (desc)</Anchor>
                      }
                    </th>
                    <th className="text-left">Author</th>
                    <th className="text-left">
                      {(orderBy == '' || orderBy.includes('pages:asc'))
                      ? <Anchor href={orderByUrl.replace('{0}', 'pages:desc')}>Pages (asc)</Anchor>
                      : <Anchor href={orderByUrl.replace('{0}', 'pages:asc')}>Pages (desc)</Anchor>
                      }
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {books?.map((book: Book) => (
                    <tr>
                      <td>
                        <Anchor
                          href={`/books/${book.id}`}
                        >
                          {book.title}
                        </Anchor>
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
