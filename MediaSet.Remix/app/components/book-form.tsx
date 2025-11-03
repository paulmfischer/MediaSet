import { useSubmit } from "@remix-run/react";
import MultiselectInput from "~/components/multiselect-input";
import SingleselectInput from "~/components/singleselect-input";
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
  const submit = useSubmit();
  const inputClasses = "w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400";
  const selectClasses = "w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400";
  const textareaClasses = "w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400 resize-vertical min-h-[100px]";

  const handleLookup = () => {
    const isbnInput = document.getElementById('isbn') as HTMLInputElement;
    const isbnValue = isbnInput?.value;
    
    if (!isbnValue) {
      return;
    }
    
    const formData = new FormData();
    formData.append('intent', 'lookup');
    formData.append('fieldName', 'isbn');
    formData.append('identifierValue', isbnValue);
    
    submit(formData, { method: 'post' });
  };

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
        <SingleselectInput name="format" placeholder="Select Format..." addLabel="Add new Format:" options={formats} selectedValue={book?.format} />
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
        <SingleselectInput name="publisher" placeholder="Select Publisher..." addLabel="Add new Publisher:" options={publishers} selectedValue={book?.publisher} />
      </div>
      
      <div>
        <label htmlFor="isbn" className="block text-sm font-medium text-gray-200 mb-1">ISBN</label>
        <div className="flex gap-2">
          <input id="isbn" name="isbn" type="text" className={inputClasses} placeholder="ISBN" defaultValue={book?.isbn} aria-label="ISBN" />
          <button
            type="button"
            onClick={handleLookup}
            disabled={isSubmitting}
            className="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-400 disabled:opacity-50 whitespace-nowrap"
          >
            Lookup
          </button>
        </div>
      </div>
      
      <div>
        <label htmlFor="plot" className="block text-sm font-medium text-gray-200 mb-1">Plot</label>
        <textarea id="plot" name="plot" className={textareaClasses} placeholder="Plot" defaultValue={book?.plot} aria-label="Plot" />
      </div>
    </fieldset>
  );
}