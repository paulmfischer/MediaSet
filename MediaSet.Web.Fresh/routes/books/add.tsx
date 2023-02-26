import { Handlers, PageProps } from '$fresh/server.ts';
import { Button } from '../../components/Button.tsx';
import { FormInput } from '../../components/TextInput.tsx';
import Layout from '../../components/Layout.tsx';
import { BookItem, BookOperationProps } from '../../models/book.ts';
import { BadRequest } from '../../models/request.ts';
import { load } from 'std';

const env = await load();
const apiUrl = env['API_URL'];

export const handler: Handlers<BookOperationProps> = {
  async POST(req, context) {
    const formData = await req.formData();
    const newBook: { [key: string]: unknown } = {};
    formData.forEach((value, key) => newBook[key] = value);

    const response = await fetch(`${apiUrl}/books`, {
      body: JSON.stringify(newBook),
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
    });

    if (response.status === 201) {
      return Response.redirect(`${req.headers.get('origin')}/books`, 303);
    }

    const responseBody = await response.json() as BadRequest;
    return context.render({
      book: newBook as unknown as BookItem,
      errors: responseBody.errors,
    });
  },
};

export default function AddBook(props: PageProps<BookOperationProps>) {
  const book = props.data?.book;
  return (
    <Layout route={props.route} title='Add Book'>
      <div class='min-w-fit'>
        Add Book
        <form class='flex flex-col' method='post'>
          <FormInput inputLabel='Title' name='title' value={book?.title} error={props.data?.errors?.Title} />
          <FormInput inputLabel='Publish Date' name='publishDate' type='date' value={book?.publishDate} />
          <FormInput
            inputLabel='Number of Pages'
            name='numberOfPages'
            type='number'
            value={book?.numberOfPages || 0}
            min='0'
          />
          <FormInput inputLabel='ISBN' name='isbn' value={book?.isbn} />
          <Button type='submit' class='mt-2'>Add</Button>
        </form>
      </div>
    </Layout>
  );
}
