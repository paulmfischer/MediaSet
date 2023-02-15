import { Handlers, PageProps } from "$fresh/server.ts";
import { Anchor } from "../../components/Anchor.tsx";
import Layout from "../../components/Layout.tsx";
import IconPlus from "tabler-icons/plus.tsx";
import { BookItem } from "../../models/book.ts";


export const handler: Handlers<Array<BookItem>> = {
  async GET(_, context) {
    const response = await fetch('http://localhost:5103/books');

    const books: Array<BookItem> = await response.json();
    return context.render(books);
  }
};

export default function Books(props: PageProps<Array<BookItem>>) {
  return (
    <Layout route={props.route} title="Books">
      <div class="flex flex-col">
        <Anchor href="/books/add" className="rounded border-1 border-gray-400">
          <IconPlus  />
        </Anchor>
        <div>
          {
            props.data.map(book => {
              return (
                <div>
                  <Anchor href={`/books/${book.id}`}>{book.id}</Anchor> {book.title} - {book.numberOfPages} - {book.publishDate}
                </div>
              );
            })
          }
        </div>
      </div>
    </Layout>
  );
}
