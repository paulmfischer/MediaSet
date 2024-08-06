import { Handlers, PageProps } from "$fresh/server.ts";
import { Anchor } from "../../components/Anchor.tsx";
import { Button } from "../../components/Button.tsx";
import { MediaHeader } from "../../components/MediaHeader.tsx";
import { baseUrl } from "../../constants.ts";
import { Book } from "../../models.ts";
import { TbPlus, TbUpload, TbEdit, TbTrash, TbArrowUp, TbArrowDown } from '@preact-icons/tb';

interface BooksProps {
  searchText: string;
  orderBy: string;
  books?: Array<Book>;
  failed?: boolean;
}

export const handler: Handlers<BooksProps> = {
  async GET(req, ctx) {
    const url = new URL(req.url);
    const searchText = url.searchParams.get("searchText") || "";
    const orderBy = url.searchParams.get("orderBy") || "";

    const response = await fetch(
      `${baseUrl}/books/search?searchText=${searchText.toString()}&orderBy=${orderBy.toString()}`,
    );
    if (!response.ok) {
      return ctx.render({ failed: true, searchText, orderBy });
    }

    const books = await response.json();
    return ctx.render({ books, searchText: searchText.toString(), orderBy });
  },
  async POST(req, ctx) {
    const formData = await req.formData();
    const id = formData.get("id") ?? "";
    const url = new URL(req.url);
    const searchText = url.searchParams.get("searchText") || "";
    const orderBy = url.searchParams.get("orderBy") || "";
    console.log("book to delete", id);

    const response = await fetch(`${baseUrl}/books/${id}`, {
      method: "DELETE",
    });
    if (!response.ok) {
      return ctx.render({ failed: true, searchText, orderBy });
    }

    let location = "/books";
    if (searchText != "") {
      location += `?searchText=${searchText}`;
    }
    if (orderBy != "") {
      location += location.includes("?")
        ? `&orderBy=${orderBy}`
        : `?orderBy=${orderBy}`;
    }

    // on success of delete, reload books page to refresh data
    return new Response("", {
      status: 303,
      headers: { Location: location },
    });
  },
};

export default function Books(props: PageProps<BooksProps>) {
  const books = props.data.books;
  const searchText = props.data.searchText;
  const orderBy = props.data.orderBy;
  const failed = props.data.failed;
  const orderByUrl = searchText
    ? `/books?searchText=${searchText}&orderBy={0}`
    : "/books?orderBy={0}";
  return (
    <div>
      <div className="flex items-center justify-between border-b dark:border-slate-300 pb-2">
        <MediaHeader title="Books" />
        <form className="flex gap-4 leading-4">
          <input
            name="searchText"
            value={searchText}
            className="dark:text-slate-800"
          />
          <input name="orderBy" value={orderBy} hidden="hidden" />
          <Button type="submit">Search</Button>
        </form>
        <div className="flex gap-4">
          <Anchor href="/books/add" className="flex items-center">
            <TbPlus size={24} />
            <span className="ml-2">Add</span>
          </Anchor>
          <Anchor href="/books/upload" className="flex items-center">
            <TbUpload size={24} />
            <span className="ml-2">
              Upload
            </span>
          </Anchor>
        </div>
      </div>
      <div className="mt-2">
        {failed &&
          <div>Failed to load books, reload to try again.</div>}
        {!failed &&
          (
            <>
              {books?.length != null && books.length == 0 &&
                (
                  <>
                    You don't have any books,{" "}
                    <Anchor href="/books/upload">Upload</Anchor>{" "}
                    a CSV file of books or{" "}
                    <Anchor href="/books/add">Add</Anchor> a single one.
                  </>
                )}
              {books?.length != null && books?.length > 0 &&
                (
                  <>
                    You have {books?.length} books.
                    <table className="table-auto border-spacing-x-2">
                      <thead>
                        <tr>
                          <th className="text-left underline">
                            {(orderBy == "" || orderBy.includes("title:asc"))
                              ? (
                                <Anchor
                                  href={orderByUrl.replace("{0}", "title:desc")}
                                  className="flex gap-2"
                                >
                                  Title <TbArrowUp size={24} alt="asc" title="asc" />
                                </Anchor>
                              )
                              : (
                                <Anchor
                                  href={orderByUrl.replace("{0}", "title:asc")}
                                  className="flex gap-2"
                                >
                                  Title <TbArrowDown size={24} alt="desc" title="desc" />
                                </Anchor>
                              )}
                          </th>
                          <th className="text-left underline">Subtitle</th>
                          <th className="text-left underline">Author</th>
                          <th className="text-left underline">
                            {(orderBy == "" || orderBy.includes("pages:asc"))
                              ? (
                                <Anchor
                                  href={orderByUrl.replace("{0}", "pages:desc")}
                                >
                              Pages <TbArrowDown size={24} alt="asc" title="asc" />
                                </Anchor>
                              )
                              : (
                                <Anchor
                                  href={orderByUrl.replace("{0}", "pages:asc")}
                                >
                              Pages <TbArrowUp size={24} alt="desc" title="desc" />
                                </Anchor>
                              )}
                          </th>
                          <th className="text-left"></th>
                        </tr>
                      </thead>
                      <tbody>
                        {books?.map((book: Book) => (
                          <tr className="dark:hover:bg-slate-800">
                            <td>
                              <Anchor
                                href={`/books/${book.id}`}
                                className="mr-2"
                              >
                                {book.title}
                              </Anchor>
                            </td>
                            <td>{book.subtitle}</td>
                            <td>{book.author?.join(",")}</td>
                            <td>{book.pages}</td>
                            <td>
                              <div class="flex gap-2 mr-2">
                                <Anchor href={`/books/${book.id}/edit`}>
                                  <TbEdit size={24} alt="Edit" title="Edit" />
                                </Anchor>
                                <form method="POST">
                                  <input
                                    hidden="hidden"
                                    value={book.id}
                                    name="id"
                                  />
                                  <Button type="submit" displayStyle="link">
                                    <TbTrash alt="Delete" title="Delete" size={24} />
                                  </Button>
                                </form>
                              </div>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </>
                )}
            </>
          )}
      </div>
    </div>
  );
}
