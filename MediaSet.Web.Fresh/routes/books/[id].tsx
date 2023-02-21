import { Handlers, PageProps } from "$fresh/server.ts";
import Layout from "../../components/Layout.tsx";
import { BookItem } from "../../models/book.ts";

type BookRespone = BookItem | null;

export const handler: Handlers<BookRespone> = {
  async GET(_, context) {
    const response = await fetch(`http://localhost:5103/books/${context.params.id}`);

    if (response.status === 404) {
      return context.renderNotFound();
    }

    const book: BookItem = await response.json();
    return context.render(book);
  }
};

export default function Book({ data: book, route }: PageProps<BookItem>) {
  return (
    <Layout route={route} title={book.title}>
      <div>
        {book.title}
      </div>
    </Layout>
  );
}