import type { MetaFunction, LoaderFunctionArgs, ActionFunctionArgs } from "@remix-run/node";
import { Form, redirect, useLoaderData, useNavigate, useNavigation } from "@remix-run/react";
import invariant from "tiny-invariant";
import { json } from "@remix-run/node";
import Spinner from "~/components/spinner";
import { getAuthors, getFormats, getGenres, getPublishers } from "~/metadata-data";
import MultiselectInput from "~/components/multiselect-input";
import { get, update } from "~/entity-data";
import { BookEntity } from "~/models";
import { Entities } from "~/constants";
import { bookFormToData } from "~/helpers";

export const meta: MetaFunction = () => {
  return [
    { title: "Edit a Book" },
    { name: "description", content: "Edit a book" },
  ];
};

export const loader = async ({ params }: LoaderFunctionArgs) => {
  invariant(params.bookId, "Missing bookId param");
  const [book, authors, genres, publishers, formats] = await Promise.all(
    [get<BookEntity>(Entities.Books, params.bookId), getAuthors(), getGenres(), getPublishers(), getFormats()]
  );

  return json({ book, authors, genres, publishers, formats });
}

export const action = async ({ params, request }: ActionFunctionArgs) => {
  invariant(params.bookId, "Missing bookId param");
  const formData = await request.formData();
  const updates = bookFormToData(formData);
  updates.id = params.bookId;
  await update(params.bookId, updates);

  return redirect(`/books/${params.bookId}`);
};

export default function Edit() {
  const { book, authors, genres, publishers, formats } = useLoaderData<typeof loader>();
  const navigate = useNavigate();
  const navigation = useNavigation();
  const isSubmitting = navigation.location?.pathname === `/books/${book.id}/edit`;

  return (
    <div className="flex flex-col">
      <div className="flex flex-row items-center justify-between">
        <div className="flex flex-row gap-4 items-end">
          <h2 className="text-2xl">Editing {book.title}</h2>
        </div>
      </div>
      <div className="h-full mt-4">
        <div className="mt-4 flex flex-col gap-2">
          <Form key={book.id} id="edit-book" method="post" action={`/books/${book.id}/edit`}>
            <fieldset disabled={isSubmitting} className="flex flex-col gap-2">
              <label htmlFor="title" className="dark:text-slate-400">Title</label>
              <input id="title" defaultValue={book.title} name="title" type="text" placeholder="Title" aria-label="Title" />
              <label htmlFor="subtitle" className="dark:text-slate-400">Subtitle</label>
              <input id="subtitle" defaultValue={book.subtitle} name="subtitle" type="text" placeholder="Subtitle" aria-label="Subtitle" />
              <label htmlFor="format" className="dark:text-slate-400">Format</label>
              <select id="format" name="format">
                <option value="">Select Format...</option>
                {formats.map(format => <option key={format.value} value={format.value}>{format.label}</option>)}
              </select>
              <label htmlFor="pages" className="dark:text-slate-400">Pages</label>
              <input id="pages" defaultValue={book.pages} name="pages" type="number" placeholder="Pages" aria-label="Pages" />
              <label htmlFor="publicationDate" className="dark:text-slate-400">Publication Date</label>
              <input id="publicationDate" defaultValue={book.publicationDate} name="publicationDate" type="text" placeholder="Publication Date" aria-label="Publication Date" />
              <label htmlFor="authors" className="dark:text-slate-400">Authors</label>
              <MultiselectInput name="authors" selectText="Select Authors..." addLabel="Add new Author:" options={authors} selectedValues={book.authors} />
              <label htmlFor="genres" className="dark:text-slate-400">Genres</label>
              <MultiselectInput name="genres" selectText="Select Genres..." addLabel="Add new Genre" options={genres} selectedValues={book.genres} />
              <label htmlFor="publisher" className="dark:text-slate-400">Publisher</label>
              <select id="publisher" name="publisher" defaultValue={book.publisher}>
                <option value="">Select Publisher...</option>
                {publishers.map(publisher => <option key={publisher.value} value={publisher.value}>{publisher.label}</option>)}
              </select>
              <label htmlFor="isbn" className="dark:text-slate-400">ISBN</label>
              <input id="isbn" defaultValue={book.isbn} name="isbn" type="text" placeholder="ISBN" aria-label="ISBN" />
              <label htmlFor="plot" className="dark:text-slate-400">Plot</label>
              <textarea id="plot" defaultValue={book.plot} name="plot" placeholder="Plot" aria-label="Plot" />
              <div className="flex flex-row gap-2 mt-3">
                <button type="submit" className="flex gap-2">
                  {isSubmitting ? <div className="flex items-center"><Spinner /></div> : null}
                  Update
                </button>
                <button type="button" onClick={() => navigate(-1)} className="secondary">Cancel</button>
              </div>
            </fieldset>
          </Form>
        </div>
      </div>
    </div>
  );
}