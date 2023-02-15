import { Handlers, PageProps } from "$fresh/server.ts";
import Layout from "../../components/Layout.tsx";

interface Book {
  id: number;
  title: string;
  publishDate: string;
  numberOfPages: number;
  isbn: string;
}

type BookRespone = Book | null;

export const handler: Handlers<BookRespone> = {
  async GET(_, context) {
    const response = await fetch(`http://localhost:5103/books/${context.params.id}`);

    if (response.status === 404) {
      return context.render(null);
    }

    const book: Book = await response.json();
    return context.render(book);
  }
};

export default function Book({ data: book, route, url }: PageProps<BookRespone>) {
  const title = book ? book.title : 'Not Found';

  const getDisplay = () => {
    if (book) {
      return <div>
        {book.title}
      </div>;
    } else {
      return <div>No book found</div>;
    }
  }

  return (
    <Layout route={route} title={title}>
      {getDisplay()}
    </Layout>
  );
}