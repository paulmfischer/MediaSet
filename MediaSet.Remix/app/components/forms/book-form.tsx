import { useState } from 'react';
import { Form } from '@remix-run/react';
import MultiselectInput from '~/components/inputs/multiselect-input';
import SingleselectInput from '~/components/inputs/singleselect-input';
import ImageUpload from '~/components/images/image-upload';
import ImageUrlPreview from '~/components/images/image-url-preview';
import ScanButton from '~/components/scan/scan-button';
import { BookEntity, FormProps } from '~/models';

type Metadata = {
  label: string;
  value: string;
};

const inputClasses =
  'w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400';

export function BookLookupSection({
  isSubmitting,
  defaultOpen = false,
}: {
  isSubmitting?: boolean;
  defaultOpen?: boolean;
}) {
  const [isOpen, setIsOpen] = useState(defaultOpen);

  return (
    <div className="bg-entity/10 border border-entity/20 rounded-lg">
      <button
        type="button"
        className="image-button w-full flex items-center justify-between !p-3"
        onClick={() => setIsOpen((prev) => !prev)}
        aria-expanded={isOpen}
      >
        <span className="text-sm font-semibold text-gray-300">Book Lookup</span>
        <svg
          className={`w-4 h-4 text-gray-400 transition-transform duration-200 ${isOpen ? 'rotate-180' : ''}`}
          fill="none"
          stroke="currentColor"
          viewBox="0 0 24 24"
          aria-hidden="true"
        >
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
        </svg>
      </button>

      {isOpen && (
        <div className="px-4 pb-4 flex flex-col gap-4">
          <Form method="post" className="flex flex-col gap-3">
            <input type="hidden" name="intent" value="lookup" />
            <input type="hidden" name="fieldName" value="title" />
            <div>
              <label htmlFor="lookup-title" className="block text-sm font-medium text-gray-200 mb-1">
                Title
              </label>
              <input
                id="lookup-title"
                name="identifierValue"
                type="text"
                className={inputClasses}
                placeholder="Title"
                disabled={isSubmitting}
              />
            </div>
            <div>
              <label htmlFor="lookup-author" className="block text-sm font-medium text-gray-200 mb-1">
                Author
              </label>
              <input
                id="lookup-author"
                name="lookupParam.author"
                type="text"
                className={inputClasses}
                placeholder="Author"
                disabled={isSubmitting}
              />
            </div>
            <div className="flex justify-end">
              <button type="submit" disabled={isSubmitting}>
                Search
              </button>
            </div>
          </Form>

          <div className="flex items-center gap-3">
            <div className="flex-1 border-t border-gray-700" />
            <span className="text-xs text-gray-500">or</span>
            <div className="flex-1 border-t border-gray-700" />
          </div>

          <Form method="post" className="flex flex-col gap-3">
            <input type="hidden" name="intent" value="lookup" />
            <input type="hidden" name="fieldName" value="isbn" />
            <div>
              <label htmlFor="lookup-isbn" className="block text-sm font-medium text-gray-200 mb-1">
                ISBN
              </label>
              <div className="flex gap-2">
                <input
                  id="lookup-isbn"
                  name="identifierValue"
                  type="text"
                  inputMode="numeric"
                  className={inputClasses}
                  placeholder="ISBN"
                  disabled={isSubmitting}
                />
                <button type="submit" disabled={isSubmitting}>
                  Search
                </button>
                <ScanButton inputId="lookup-isbn" fieldName="isbn" disabled={isSubmitting} />
              </div>
            </div>
          </Form>
        </div>
      )}
    </div>
  );
}

type BookFormProps = FormProps & {
  book?: BookEntity;
  authors: Metadata[];
  genres: Metadata[];
  publishers: Metadata[];
  formats: Metadata[];
};

export default function BookForm({ book, authors, genres, publishers, formats, isSubmitting }: BookFormProps) {
  const textareaClasses =
    'w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400 resize-vertical min-h-[100px]';

  return (
    <fieldset disabled={isSubmitting} className="flex flex-col gap-4">
      <input id="id" name="id" type="hidden" defaultValue={book?.id} />

      <div>
        <label htmlFor="title" className="block text-sm font-medium text-gray-200 mb-1">
          Title
        </label>
        <input
          id="title"
          name="title"
          type="text"
          className={inputClasses}
          placeholder="Title"
          aria-label="Title"
          defaultValue={book?.title}
        />
      </div>

      <div>
        <label htmlFor="isbn" className="block text-sm font-medium text-gray-200 mb-1">
          ISBN
        </label>
        <input
          id="isbn"
          name="isbn"
          type="text"
          inputMode="numeric"
          className={inputClasses}
          placeholder="ISBN"
          defaultValue={book?.isbn}
          aria-label="ISBN"
        />
      </div>

      <ImageUpload name="coverImage" existingImage={book?.coverImage} isSubmitting={isSubmitting} />

      <div>
        <label htmlFor="imageUrl" className="block text-sm font-medium text-gray-200 mb-1">
          Image URL
        </label>
        <p className="text-xs text-gray-400 mb-2">
          Provide an image URL or upload a file above. If both are provided, the uploaded file takes precedence.
          Accepted: JPEG or PNG, up to 5MB.
        </p>
        <div className="flex items-start">
          <input
            id="imageUrl"
            name="imageUrl"
            type="url"
            className={`${inputClasses} flex-1`}
            placeholder="https://example.com/image.jpg"
            aria-label="Image URL"
            defaultValue={book?.imageUrl}
          />
          <ImageUrlPreview inputId="imageUrl" existingUrl={book?.imageUrl} />
        </div>
      </div>

      <div>
        <label htmlFor="subtitle" className="block text-sm font-medium text-gray-200 mb-1">
          Subtitle
        </label>
        <input
          id="subtitle"
          name="subtitle"
          type="text"
          className={inputClasses}
          placeholder="Subtitle"
          aria-label="Subtitle"
          defaultValue={book?.subtitle}
        />
      </div>

      <div>
        <label htmlFor="format" className="block text-sm font-medium text-gray-200 mb-1">
          Format
        </label>
        <SingleselectInput
          name="format"
          placeholder="Select Format..."
          addLabel="Add new Format:"
          options={formats}
          selectedValue={book?.format}
        />
      </div>

      <div>
        <label htmlFor="pages" className="block text-sm font-medium text-gray-200 mb-1">
          Pages
        </label>
        <input
          id="pages"
          name="pages"
          type="number"
          className={inputClasses}
          placeholder="Pages"
          aria-label="Pages"
          defaultValue={book?.pages}
        />
      </div>

      <div>
        <label htmlFor="publicationDate" className="block text-sm font-medium text-gray-200 mb-1">
          Publication Date
        </label>
        <input
          id="publicationDate"
          name="publicationDate"
          type="text"
          className={inputClasses}
          placeholder="Publication Date"
          defaultValue={book?.publicationDate}
          aria-label="Publication Date"
        />
      </div>

      <div>
        <label htmlFor="authors" className="block text-sm font-medium text-gray-200 mb-1">
          Authors
        </label>
        <MultiselectInput
          name="authors"
          selectText="Select Authors..."
          addLabel="Add new Author:"
          options={authors}
          selectedValues={book?.authors}
        />
      </div>

      <div>
        <label htmlFor="genres" className="block text-sm font-medium text-gray-200 mb-1">
          Genres
        </label>
        <MultiselectInput
          name="genres"
          selectText="Select Genres..."
          addLabel="Add new Genre"
          options={genres}
          selectedValues={book?.genres}
        />
      </div>

      <div>
        <label htmlFor="publisher" className="block text-sm font-medium text-gray-200 mb-1">
          Publisher
        </label>
        <SingleselectInput
          name="publisher"
          placeholder="Select Publisher..."
          addLabel="Add new Publisher:"
          options={publishers}
          selectedValue={book?.publisher}
        />
      </div>

      <div>
        <label htmlFor="plot" className="block text-sm font-medium text-gray-200 mb-1">
          Plot
        </label>
        <textarea
          id="plot"
          name="plot"
          className={textareaClasses}
          placeholder="Plot"
          defaultValue={book?.plot}
          aria-label="Plot"
        />
      </div>
    </fieldset>
  );
}
