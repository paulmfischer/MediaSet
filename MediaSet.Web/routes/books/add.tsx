import { Handlers, PageProps } from "$fresh/server.ts";
import BookForm from "../../components/BookForm.tsx";
import { MediaHeader } from "../../components/MediaHeader.tsx";
import { baseUrl } from "../../constants.ts";
import { Book } from "../../models.ts";

interface AddBookProps {
  message: string | null;
}

function getFormData(form: FormData, property: keyof Book): string | undefined {
  const value = form.get(property);
  if (value == null || value == '') {
    return undefined;
  }

  return value as string;
}

export const handler: Handlers<AddBookProps> = {
  async POST(req, ctx) {
    const form = await req.formData();
    const book: Book = {
      title: getFormData(form, 'title'),
      author: getFormData(form, 'author')?.split(','),
      format: getFormData(form, 'format'),
      genre: getFormData(form, 'genre')?.split(','),
      isbn: getFormData(form, 'isbn'),
      plot: getFormData(form, 'plot'),
      publicationDate: getFormData(form, 'publicationDate'),
      publisher:  getFormData(form, 'publisher')?.split(','),
      subTitle: getFormData(form, 'subTitle'),
      pages: Number(getFormData(form, 'pages')),
    };

    const response = await fetch(`${baseUrl}/books`, {
      method: 'POST',
      body: JSON.stringify(book),
      headers: {
        "Content-Type": "application/json",
      }
    });

    if (!response.ok) {
      console.log('failed to create a book', response);
      return ctx.render({ message: 'Failed to create a new book' });
    }

    // on success of upload, redirect to books list page
    return new Response('', {
      status: 303,
      headers: { Location: '/books' }
    });
  },
};

export default function Add(props: PageProps<AddBookProps>) {
  return (
    <>
      <MediaHeader title="Add a book" />
      {props.data?.message != null && <div>{props.data.message}</div>}
      <BookForm className="mt-2" submitText="Add" method="POST" />
    </>
  );
}