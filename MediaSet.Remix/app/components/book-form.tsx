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
  const inputClasses = "w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400";
  const selectClasses = "w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400";
  const textareaClasses = "w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400 resize-vertical min-h-[100px]";

  return (
    <fieldset disabled={isSubmitting} className="flex flex-col gap-4">
      <input hidden id="id" name="id" type="text" defaultValue={book?.id} />
      
      <div>
        <label htmlFor="title" className="block text-sm font-medium text-gray-200 mb-1">Title</label>
        <input id="title" name="title" type="text" className={inputClasses} placeholder="Title" aria-label="Title" defaultValue={book?.title} />
      </div>
      
      <div>
        <label htmlFor="subtitle" className="block text-sm font-medium text-gray-200 mb-1">Subtitle</label>
        <input id="subtitle" name="subtitle" type="text" className={inputClasses} placeholder="Subtitle" aria-label="Subtitle" defaultValue={book?.subtitle} />
      </div>
      
      <div>
        <label htmlFor="format" className="block text-sm font-medium text-gray-200 mb-1">Format</label>
        <select id="format" name="format" className={selectClasses} value={book?.format}>
          <option value="">Select Format...</option>
          {formats.map(format => <option key={format.value} value={format.value}>{format.label}</option>)}
        </select>
      </div>
      
      <div>
        <label htmlFor="pages" className="block text-sm font-medium text-gray-200 mb-1">Pages</label>
        <input id="pages" name="pages" type="number" className={inputClasses} placeholder="Pages" aria-label="Pages" defaultValue={book?.pages} />
      </div>
      
      <div>
        <label htmlFor="publicationDate" className="block text-sm font-medium text-gray-200 mb-1">Publication Date</label>
        <input id="publicationDate" name="publicationDate" type="text" className={inputClasses} placeholder="Publication Date" defaultValue={book?.publicationDate} aria-label="Publication Date" />
      </div>
      
      <div>
        <label htmlFor="authors" className="block text-sm font-medium text-gray-200 mb-1">Authors</label>
        <MultiselectInput name="authors" selectText="Select Authors..." addLabel="Add new Author:" options={authors} selectedValues={book?.authors} />
      </div>

      <div>
        <label htmlFor="genres" className="block text-sm font-medium text-gray-200 mb-1">Genres</label>
        <MultiselectInput name="genres" selectText="Select Genres..." addLabel="Add new Genre" options={genres} selectedValues={book?.genres} />
      </div>

      <div>
        <label htmlFor="publisher" className="block text-sm font-medium text-gray-200 mb-1">Publisher</label>
        <select id="publisher" name="publisher" className={selectClasses} value={book?.publisher}>
          <option value="">Select Publisher...</option>
          {(() => {
            const allPublishers = [...publishers];
            // Add the book's publisher if it's not in the existing list
            if (book?.publisher && !publishers.find(p => p.value === book.publisher)) {
              allPublishers.push({ value: book.publisher, label: book.publisher });
            }
            return allPublishers.map(publisher => (
              <option key={publisher.value} value={publisher.value}>{publisher.label}</option>
            ));
          })()}
        </select>
      </div>
      
      <div>
        <label htmlFor="isbn" className="block text-sm font-medium text-gray-200 mb-1">ISBN</label>
        <input id="isbn" name="isbn" type="text" className={inputClasses} placeholder="ISBN" defaultValue={book?.isbn} aria-label="ISBN" />
      </div>
      
      <div>
        <label htmlFor="plot" className="block text-sm font-medium text-gray-200 mb-1">Plot</label>
        <textarea id="plot" name="plot" className={textareaClasses} placeholder="Plot" defaultValue={book?.plot} aria-label="Plot" />
      </div>
    </fieldset>
  );
}