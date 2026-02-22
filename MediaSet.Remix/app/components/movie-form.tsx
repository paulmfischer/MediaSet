import { useState } from 'react';
import { useSubmit } from '@remix-run/react';
import ScanButton from '~/components/scan-button';
import MultiselectInput from '~/components/multiselect-input';
import SingleselectInput from '~/components/singleselect-input';
import ImageUpload from '~/components/image-upload';
import ImageUrlPreview from '~/components/image-url-preview';
import { FormProps, MovieEntity } from '~/models';

type Metadata = {
  label: string;
  value: string;
};
type MovieFormProps = FormProps & {
  isSubmitting?: boolean;
  movie?: MovieEntity;
  studios: Metadata[];
  genres: Metadata[];
  formats: Metadata[];
  barcodeLookupAvailable?: boolean;
};

export default function MovieForm({
  movie,
  genres,
  studios,
  formats,
  isSubmitting,
  barcodeLookupAvailable,
}: MovieFormProps) {
  const submit = useSubmit();
  const [isTvSeries, setIsTvSeries] = useState(movie?.isTvSeries ?? false);
  const isTvSeriesChanged = () => setIsTvSeries(!isTvSeries);

  const inputClasses =
    'w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400';
  const textareaClasses =
    'w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400 resize-vertical min-h-[100px]';

  const handleTitleLookup = () => {
    const titleInput = document.getElementById('title') as HTMLInputElement;
    const titleValue = titleInput?.value;

    if (!titleValue) {
      return;
    }

    const formData = new FormData();
    formData.append('intent', 'lookup');
    formData.append('fieldName', 'title');
    formData.append('identifierValue', titleValue);

    submit(formData, { method: 'post' });
  };

  const handleLookup = () => {
    const barcodeInput = document.getElementById('barcode') as HTMLInputElement;
    const barcodeValue = barcodeInput?.value;

    if (!barcodeValue) {
      return;
    }

    const formData = new FormData();
    formData.append('intent', 'lookup');
    formData.append('fieldName', 'barcode');
    formData.append('identifierValue', barcodeValue);

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
      <input id="id" name="id" type="hidden" defaultValue={movie?.id} />

      <div>
        <label htmlFor="title" className="block text-sm font-medium text-gray-200 mb-1">
          Title
        </label>
        <div className="flex gap-2">
          <input
            id="title"
            name="title"
            type="text"
            className={inputClasses}
            placeholder="Title"
            aria-label="Title"
            defaultValue={movie?.title}
          />
          {barcodeLookupAvailable && (
            <button type="button" onClick={handleTitleLookup} disabled={isSubmitting}>
              Lookup
            </button>
          )}
        </div>
      </div>

      <div>
        <label htmlFor="barcode" className="block text-sm font-medium text-gray-200 mb-1">
          Barcode
        </label>
        <div className="flex gap-2">
          <input
            id="barcode"
            name="barcode"
            type="text"
            inputMode="numeric"
            className={inputClasses}
            placeholder="Barcode"
            defaultValue={movie?.barcode}
            aria-label="Barcode"
            onKeyDown={handleKeyDown}
          />
          {barcodeLookupAvailable && (
            <>
              <button type="button" onClick={handleLookup} disabled={isSubmitting}>
                Lookup
              </button>
              <ScanButton inputId="barcode" fieldName="barcode" disabled={isSubmitting} />
            </>
          )}
        </div>
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
