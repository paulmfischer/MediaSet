import { Handlers, PageProps } from '$fresh/server.ts';
import { Button } from '../../../components/Button.tsx';
import { FormInput } from '../../../components/TextInput.tsx';
import Layout from '../../../components/Layout.tsx';
import { BookItem, BookOperationProps } from '../../../models/book.ts';
import { Input } from '../../../components/Input.tsx';
import { BadRequest } from "../../../models/request.ts";
import { load } from "https://deno.land/std/dotenv/mod.ts";

const env = await load();
const apiUrl = env['API_URL'];

export const handler: Handlers<BookOperationProps> = {
  async GET(_, context) {
    const response = await fetch(`http://localhost:${port}/books/${context.params.id}`);

    if (response.status === 404) {
      return context.renderNotFound();
    }

    const book: BookItem = await response.json();
    return context.render({ book });
  },

  async POST(req, context) {
    const formData = await req.formData();
    const editBook: { [key: string]: unknown } = {};
    formData.forEach((value, key) => editBook[key] = value);
    const book = editBook as unknown as BookItem;

    const response = await fetch(`${port}/books/${book.id}`, {
      body: JSON.stringify(book),
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
      },
    });

    if (response.status === 200) {
      return Response.redirect(`${req.headers.get('origin')}/books`);
    }

    const responseBody = await response.json() as BadRequest;
    return context.render({
      book,
      errors: responseBody.errors,
    });
  },
};

export default function AddBook(props: PageProps<BookOperationProps>) {
  const book = props.data.book;
  return (
    <Layout route={props.route} title={`Edit Book - ${book.id}`}>
      <div class='min-w-fit'>
        Edit Book
        <form class='flex flex-col' method='post'>
          <FormInput inputLabel='Title' name='title' value={book.title} error={props.data?.errors?.Title} />
          <FormInput inputLabel='Publish Date' name='publishDate' type='month' value={book.publishDate} />
          <FormInput inputLabel='Number of Pages' name='numberOfPages' type='number' value={book.numberOfPages} min="0" />
          <FormInput inputLabel='ISBN' name='isbn' value={book.isbn} />
          <Input type='hidden' name='id' value={book.id} />
          <Button type='submit' class='mt-2'>Edit</Button>
        </form>
      </div>
    </Layout>
  );
}
