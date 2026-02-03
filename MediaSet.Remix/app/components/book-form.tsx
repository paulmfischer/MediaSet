import { useSubmit } from "@remix-run/react";
import ScanButton from "~/components/scan-button";
import MultiselectInput from "~/components/multiselect-input";
import SingleselectInput from "~/components/singleselect-input";
import ImageUpload from "~/components/image-upload";
import ImageUrlPreview from "~/components/image-url-preview";
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
  isbnLookupAvailable?: boolean;
};

export default function BookForm({ book, authors, genres, publishers, formats, isSubmitting, isbnLookupAvailable }: BookFormProps) {
  const submit = useSubmit();
  const inputClasses = "w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400";
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

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      handleLookup();
    }
  };

  return (
    <fieldset disabled={isSubmitting} className="flex flex-col gap-4">
      <input id="id" name="id" type="hidden" defaultValue={book?.id} />
      
      <div>
        <label htmlFor="title" className="block text-sm font-medium text-gray-200 mb-1">Title</label>
        <input id="title" name="title" type="text" className={inputClasses} placeholder="Title" aria-label="Title" defaultValue={book?.title} />
      </div>
      
      <ImageUpload name="coverImage" existingImage={book?.coverImage} isSubmitting={isSubmitting} />
      
      <div>
        <label htmlFor="imageUrl" className="block text-sm font-medium text-gray-200 mb-1">Image URL</label>
        <p className="text-xs text-gray-400 mb-2">Provide an image URL or upload a file above. If both are provided, the uploaded file takes precedence. Accepted: JPEG or PNG, up to 5MB.</p>
        <div className="flex items-start">
          <input id="imageUrl" name="imageUrl" type="url" className={`${inputClasses} flex-1`} placeholder="https://example.com/image.jpg" aria-label="Image URL" defaultValue={book?.imageUrl} />
          <ImageUrlPreview inputId="imageUrl" existingUrl={book?.imageUrl} />
        </div>
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
          <input id="isbn" name="isbn" type="text" className={inputClasses} placeholder="ISBN" defaultValue={book?.isbn} aria-label="ISBN" onKeyDown={handleKeyDown} />
          {isbnLookupAvailable && (
            <>
              <button
                type="button"
                onClick={handleLookup}
                disabled={isSubmitting}
              >
                Lookup
              </button>
              <ScanButton inputId="barcode" fieldName="barcode" disabled={isSubmitting} />
            </>
          )}
        </div>
      </div>
      
      <div>
        <label htmlFor="plot" className="block text-sm font-medium text-gray-200 mb-1">Plot</label>
        <textarea id="plot" name="plot" className={textareaClasses} placeholder="Plot" defaultValue={book?.plot} aria-label="Plot" />
      </div>
    </fieldset>
  );
}