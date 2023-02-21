import { Handlers, PageProps } from '$fresh/server.ts';
import { Button } from '../../components/Button.tsx';
import { FormInput } from '../../components/TextInput.tsx';
import Layout from '../../components/Layout.tsx';
import { BookItem } from '../../models/book.ts';

type BadRequestError = {
  [key: string]: string[];
};

type BadRequest = {
  type: string;
  title: string;
  status: number;
  errors: BadRequestError;
};

interface AddBookProps {
  newBook: BookItem;
  errors?: BadRequestError;
}

export const handler: Handlers<AddBookProps> = {
  async POST(req, context) {
    const formData = await req.formData();
    const newBook: { [key: string]: unknown } = {};
    formData.forEach((value, key) => newBook[key] = value);

    const response = await fetch('http://localhost:5103/books', {
      body: JSON.stringify(newBook),
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
    });

    if (response.status === 201) {
      return Response.redirect(`${req.headers.get('origin')}/books`);
    }

    const responseBody = await response.json() as BadRequest;
    return context.render({
      newBook: newBook as unknown as BookItem,
      errors: responseBody.errors,
    });
  },
};

export default function AddBook(props: PageProps<AddBookProps>) {
  const book = props.data?.newBook;
  return (
    <Layout route={props.route} title='Add Book'>
      <div class='min-w-fit'>
        Add Book
        <form class='flex flex-col' method='post'>
          <FormInput inputLabel='Title' name='title' required value={book?.title} error={props.data?.errors?.Title} />
          <FormInput inputLabel='Publish Date' name='publishDate' type='month' value={book?.publishDate} />
          <FormInput inputLabel='Number of Pages' name='numberOfPages' type='number' value={book?.numberOfPages} />
          <FormInput inputLabel='ISBN' name='isbn' value={book?.isbn} />
          <Button type='submit' class='mt-2'>Add</Button>
        </form>
      </div>
    </Layout>
  );
}
