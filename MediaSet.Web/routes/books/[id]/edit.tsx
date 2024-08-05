import { Handlers, PageProps } from "$fresh/server.ts";
import BookForm from "../../../components/BookForm.tsx";
import { MediaHeader } from "../../../components/MediaHeader.tsx";
import { baseUrl } from "../../../constants.ts";
import { getFormData } from "../../../helpers.ts";
import { Book } from "../../../models.ts";

interface EditBookProps {
  book: Book;
  message?: string;
}

export const handler: Handlers<EditBookProps> = {
  async GET(_req, ctx) {
    const response = await fetch(`${baseUrl}/books/${ctx.params.id}`);
    if (!response.ok) {
      return ctx.renderNotFound();
    }
    const book = await response.json();
    return ctx.render({ book });
  },
  async POST(req, ctx) {
    const form = await req.formData();
    const id = getFormData(form, 'id');
    const book: Book = {
      id,
      title: getFormData(form, 'title'),
      author: getFormData(form, 'author')?.split(','),
      format: getFormData(form, 'format'),
      genre: getFormData(form, 'genre')?.split(','),
      isbn: getFormData(form, 'isbn'),
      plot: getFormData(form, 'plot'),
      publicationDate: getFormData(form, 'publicationDate'),
      publisher: getFormData(form, 'publisher')?.split(','),
      subtitle: getFormData(form, 'subtitle'),
      pages: Number(getFormData(form, 'pages')),
    };

    const response = await fetch(`${baseUrl}/books/${id}`, {
      method: 'PUT',
      body: JSON.stringify(book),
      headers: {
        "Content-Type": "application/json",
      }
    });

    if (!response.ok) {
      console.log('failed to update a book', response);
      return ctx.render({ message: 'Failed to create a new book', book });
    }

    // on success of upload, redirect to books list page
    return new Response('', {
      status: 303,
      headers: { Location: `/books/${id}` }
    });
  },
};

export default function Edit(props: PageProps<EditBookProps>) {
  return (
    <>
      <MediaHeader title="Edit a book" />
      {props.data?.message != null && <div>{props.data.message}</div>}
      <BookForm className="mt-2" submitText="Update" method="POST" book={props.data.book} />
    </>
  );
}