import { useState } from 'react';
import { Form } from '@remix-run/react';
import MultiselectInput from '~/components/multiselect-input';
import SingleselectInput from '~/components/singleselect-input';
import ImageUpload from '~/components/image-upload';
import ImageUrlPreview from '~/components/image-url-preview';
import ScanButton from '~/components/scan-button';
import { FormProps, MovieEntity } from '~/models';

type Metadata = {
  label: string;
  value: string;
};

const inputClasses =
  'w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400';

export function MovieLookupSection({
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
        <span className="text-sm font-semibold text-gray-300">Movie Lookup</span>
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
            <input type="hidden" name="fieldName" value="barcode" />
            <div>
              <label htmlFor="lookup-barcode" className="block text-sm font-medium text-gray-200 mb-1">
                Barcode
              </label>
              <div className="flex gap-2">
                <input
                  id="lookup-barcode"
                  name="identifierValue"
                  type="text"
                  inputMode="numeric"
                  className={inputClasses}
                  placeholder="Barcode"
                  disabled={isSubmitting}
                />
                <button type="submit" disabled={isSubmitting}>
                  Search
                </button>
                <ScanButton inputId="lookup-barcode" fieldName="barcode" disabled={isSubmitting} />
              </div>
            </div>
          </Form>
        </div>
      )}
    </div>
  );
}

type MovieFormProps = FormProps & {
  movie?: MovieEntity;
  studios: Metadata[];
  genres: Metadata[];
  formats: Metadata[];
};

export default function MovieForm({ movie, genres, studios, formats, isSubmitting }: MovieFormProps) {
  const [isTvSeries, setIsTvSeries] = useState(movie?.isTvSeries ?? false);
  const isTvSeriesChanged = () => setIsTvSeries(!isTvSeries);

  const textareaClasses =
    'w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400 resize-vertical min-h-[100px]';

  return (
    <fieldset disabled={isSubmitting} className="flex flex-col gap-4">
      <input id="id" name="id" type="hidden" defaultValue={movie?.id} />

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
          defaultValue={movie?.title}
        />
      </div>

      <div>
        <label htmlFor="barcode" className="block text-sm font-medium text-gray-200 mb-1">
          Barcode
        </label>
        <input
          id="barcode"
          name="barcode"
          type="text"
          inputMode="numeric"
          className={inputClasses}
          placeholder="Barcode"
          defaultValue={movie?.barcode}
          aria-label="Barcode"
        />
      </div>

      <ImageUpload name="coverImage" existingImage={movie?.coverImage} isSubmitting={isSubmitting} />

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
            defaultValue={movie?.imageUrl}
          />
          <ImageUrlPreview inputId="imageUrl" existingUrl={movie?.imageUrl} />
        </div>
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
          selectedValue={movie?.format}
        />
      </div>

      <div>
        <label htmlFor="runtime" className="block text-sm font-medium text-gray-200 mb-1">
          Runtime
        </label>
        <input
          id="runtime"
          name="runtime"
          type="number"
          className={inputClasses}
          placeholder="Runtime"
          aria-label="Runtime"
          defaultValue={movie?.runtime}
        />
      </div>

      <div>
        <label htmlFor="releaseDate" className="block text-sm font-medium text-gray-200 mb-1">
          Release Date
        </label>
        <input
          id="releaseDate"
          name="releaseDate"
          type="text"
          className={inputClasses}
          placeholder="Release Date"
          defaultValue={movie?.releaseDate}
          aria-label="Release Date"
        />
      </div>

      <div className="flex items-center gap-2">
        <input
          id="isTvSeries"
          name="isTvSeries"
          type="checkbox"
          value="true"
          checked={isTvSeries}
          onChange={isTvSeriesChanged}
          aria-label="Is TV Series"
          className="form-checkbox h-5 w-5 text-blue-400 bg-gray-800 border-gray-600 focus:ring-blue-400"
        />
        <label htmlFor="isTvSeries" className="block text-sm font-medium text-gray-200">
          Is TV Series
        </label>
      </div>

      <div>
        <label htmlFor="studios" className="block text-sm font-medium text-gray-200 mb-1">
          Studios
        </label>
        <MultiselectInput
          name="studios"
          selectText="Select Studios..."
          addLabel="Add new Studio:"
          options={studios}
          selectedValues={movie?.studios}
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
          selectedValues={movie?.genres}
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
          defaultValue={movie?.plot}
          aria-label="Plot"
        />
      </div>
    </fieldset>
  );
}
