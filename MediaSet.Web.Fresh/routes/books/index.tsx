import { Handlers, PageProps } from "$fresh/server.ts";
import { Anchor } from "../../components/Anchor.tsx";
import Layout from "../../components/Layout.tsx";

interface Book {
  id: number;
  title: string;
  publishDate: string;
  numberOfPages: number;
  isbn: string;
}

export const handler: Handlers<Array<Book>> = {
  async GET(_, context) {
    const response = await fetch('http://localhost:5103/books');

    const books: Array<Book> = await response.json();
    return context.render(books);
  }
};

export default function Books(props: PageProps<Array<Book>>) {
  return (
    <Layout route={props.route} title="Books">
      Book collection
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
    </Layout>
  );
}
