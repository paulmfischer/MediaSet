import MultiselectInput from "~/components/multiselect-input";
import { BookEntity, FormProps } from "~/models";

type Metadata = {
  label: string;
  value: string;
};
type BookFormProps = FormProps & {
  book?: BookEntity;
  authors: Metadata[];
  genres: Metadata[];
  publishers: Metadata[];
  formats: Metadata[];
};

export default function BookForm({ book, authors, genres, publishers, formats, isSubmitting }: BookFormProps) {
  return (
    <fieldset disabled={isSubmitting} className="flex flex-col gap-2">
      <input hidden id="id" name="id" type="text" defaultValue={book?.id} />
      <label htmlFor="title" className="dark:text-slate-400">Title</label>
      <input id="title" name="title" type="text" placeholder="Title" aria-label="Title" defaultValue={book?.title} />
      <label htmlFor="subtitle" className="dark:text-slate-400">Subtitle</label>
      <input id="subtitle" name="subtitle" type="text" placeholder="Subtitle" aria-label="Subtitle" defaultValue={book?.subtitle} />
      <label htmlFor="format" className="dark:text-slate-400">Format</label>
      <select id="format" name="format" defaultValue={book?.format}>
        <option value="">Select Format...</option>
        {formats.map(format => <option key={format.value} value={format.value}>{format.label}</option>)}
      </select>
      <label htmlFor="pages" className="dark:text-slate-400">Pages</label>
      <input id="pages" name="pages" type="number" placeholder="Pages" aria-label="Pages" defaultValue={book?.pages} />
      <label htmlFor="publicationDate" className="dark:text-slate-400">Publication Date</label>
      <input id="publicationDate" name="publicationDate" type="text" placeholder="Publication Date" defaultValue={book?.publicationDate} aria-label="Publication Date" />
      <label htmlFor="authors" className="dark:text-slate-400">Authors</label>
      <MultiselectInput name="authors" selectText="Select Authors..." addLabel="Add new Author:" options={authors} selectedValues={book?.authors} />
      <label htmlFor="genres" className="dark:text-slate-400">Genres</label>
      <MultiselectInput name="genres" selectText="Select Genres..." addLabel="Add new Genre" options={genres} selectedValues={book?.genres} />
      <label htmlFor="publisher" className="dark:text-slate-400">Publisher</label>
      <select id="publisher" name="publisher" defaultValue={book?.publisher}>
        <option value="">Select Publisher...</option>
        {publishers.map(publisher => <option key={publisher.value} value={publisher.value}>{publisher.label}</option>)}
      </select>
      <label htmlFor="isbn" className="dark:text-slate-400">ISBN</label>
      <input id="isbn" name="isbn" type="text" placeholder="ISBN" defaultValue={book?.isbn} aria-label="ISBN" />
      <label htmlFor="plot" className="dark:text-slate-400">Plot</label>
      <textarea id="plot" name="plot" placeholder="Plot" defaultValue={book?.plot} aria-label="Plot" />
    </fieldset>
  );
}