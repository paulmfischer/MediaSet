import type { MetaFunction, ActionFunctionArgs } from "@remix-run/node";
import { Form, redirect, useLoaderData, useNavigate, useNavigation } from "@remix-run/react";
import { json } from "@remix-run/node";
import { add } from "~/entity-data";
import MultiselectInput from "~/components/multiselect-input";
import Spinner from "~/components/spinner";
import { getAuthors, getFormats, getGenres, getPublishers } from "~/metadata-data";
import { bookFormToData } from "~/helpers";

export const meta: MetaFunction = () => {
  return [
    { title: "Add a Book" },
    { name: "description", content: "Add a book" },
  ];
};

export const loader = async () => {
  const [authors, genres, publishers, formats] = await Promise.all([getAuthors(), getGenres(), getPublishers(), getFormats()]);
  return json({ authors, genres, publishers, formats });
}

export const action = async ({ request }: ActionFunctionArgs) => {
  const formData = await request.formData();
  const book = bookFormToData(formData);
  const newBook = await add(book);

  return redirect(`/books/${newBook.id}`);
};

export default function Add() {
  const { authors, genres, publishers, formats } = useLoaderData<typeof loader>();
  const navigate = useNavigate();
  const navigation = useNavigation();
  const isSubmitting = navigation.location?.pathname === '/books/add';

  return (
    <div className="flex flex-col">
      <div className="flex flex-row items-center justify-between">
        <div className="flex flex-row gap-4 items-end">
          <h2 className="text-2xl">Add a Book</h2>
        </div>
      </div>
      <div className="h-full mt-4">
        <div className="mt-4 flex flex-col gap-2">
          <Form id="edit-book" method="post" action="/books/add">
            <fieldset disabled={isSubmitting} className="flex flex-col gap-2">
              <label htmlFor="title" className="dark:text-slate-400">Title</label>
              <input id="title" name="title" type="text" placeholder="Title" aria-label="Title" />
              <label htmlFor="subtitle" className="dark:text-slate-400">Subtitle</label>
              <input id="subtitle" name="subtitle" type="text" placeholder="Subtitle" aria-label="Subtitle" />
              <label htmlFor="format" className="dark:text-slate-400">Format</label>
              <select id="format" name="format">
                <option value="">Select Format...</option>
                {formats.map(format => <option value={format.value}>{format.label}</option>)}
              </select>
              <label htmlFor="pages" className="dark:text-slate-400">Pages</label>
              <input id="pages" name="pages" type="number" placeholder="Pages" aria-label="Pages" />
              <label htmlFor="publicationDate" className="dark:text-slate-400">Publication Date</label>
              <input id="publicationDate" name="publicationDate" type="text" placeholder="Publication Date" aria-label="Publication Date" />
              <label htmlFor="authors" className="dark:text-slate-400">Authors</label>
              <MultiselectInput name="authors" selectText="Select Authors..." addLabel="Add new Author:" options={authors} />
              <label htmlFor="genres" className="dark:text-slate-400">Genres</label>
              <MultiselectInput name="genres" selectText="Select Genres..." addLabel="Add new Genre" options={genres} />
              <label htmlFor="publisher" className="dark:text-slate-400">Publisher</label>
              <select id="publisher" name="publisher">
                <option value="">Select Publisher...</option>
                {publishers.map(publisher => <option value={publisher.value}>{publisher.label}</option>)}
              </select>
              <label htmlFor="isbn" className="dark:text-slate-400">ISBN</label>
              <input id="isbn" name="isbn" type="text" placeholder="ISBN" aria-label="ISBN" />
              <label htmlFor="plot" className="dark:text-slate-400">Plot</label>
              <textarea id="plot" name="plot" placeholder="Plot" aria-label="Plot" />
              <div className="flex flex-row gap-2 mt-3">
                <button type="submit">
                  {isSubmitting ? <div className="flex items-center"><Spinner /></div> : null}
                  Add
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