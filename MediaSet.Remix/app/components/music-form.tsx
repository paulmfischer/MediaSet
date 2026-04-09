import { useState, useEffect } from 'react';
import { Form } from '@remix-run/react';
import MultiselectInput from '~/components/multiselect-input';
import SingleselectInput from '~/components/singleselect-input';
import ImageUpload from '~/components/image-upload';
import ImageUrlPreview from '~/components/image-url-preview';
import ScanButton from '~/components/scan-button';
import { FormProps, MusicEntity, Disc } from '~/models';
import { millisecondsToMinutesSeconds } from '~/utils/helpers';

type Metadata = {
  label: string;
  value: string;
};

const inputClasses =
  'w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400';

export function MusicLookupSection({
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
        <span className="text-sm font-semibold text-gray-300">Music Lookup</span>
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
              <label htmlFor="lookup-artist" className="block text-sm font-medium text-gray-200 mb-1">
                Artist
              </label>
              <input
                id="lookup-artist"
                name="lookupParam.artist"
                type="text"
                className={inputClasses}
                placeholder="Artist"
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

type MusicFormProps = FormProps & {
  music?: MusicEntity;
  genres: Metadata[];
  formats: Metadata[];
  labels: Metadata[];
};

export default function MusicForm({ music, genres, formats, labels, isSubmitting }: MusicFormProps) {
  const [discList, setDiscList] = useState<Disc[]>(music?.discList ?? []);

  useEffect(() => {
    if (music?.discList) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setDiscList(music.discList);
    }
  }, [music]);

  const addDisc = () => {
    setDiscList([...discList, { trackNumber: discList.length + 1, title: '', duration: null }]);
  };

  const removeDisc = (index: number) => {
    setDiscList(discList.filter((_, i) => i !== index));
  };

  const updateDisc = (index: number, field: keyof Disc, value: string | number | null) => {
    const newDiscList = [...discList];
    newDiscList[index] = { ...newDiscList[index], [field]: value };
    setDiscList(newDiscList);
  };

  return (
    <fieldset disabled={isSubmitting} className="flex flex-col gap-4">
      <input id="id" name="id" type="hidden" defaultValue={music?.id} />

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
          defaultValue={music?.title}
        />
      </div>

      <div>
        <label htmlFor="artist" className="block text-sm font-medium text-gray-200 mb-1">
          Artist
        </label>
        <input
          id="artist"
          name="artist"
          type="text"
          className={inputClasses}
          placeholder="Artist"
          aria-label="Artist"
          defaultValue={music?.artist}
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
          defaultValue={music?.barcode}
          aria-label="Barcode"
        />
      </div>

      <ImageUpload name="coverImage" existingImage={music?.coverImage} isSubmitting={isSubmitting} />

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
            defaultValue={music?.imageUrl}
          />
          <ImageUrlPreview inputId="imageUrl" existingUrl={music?.imageUrl} />
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
          selectedValue={music?.format}
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
          defaultValue={music?.releaseDate}
          aria-label="Release Date"
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
          selectedValues={music?.genres}
        />
      </div>

      <div>
        <label htmlFor="duration" className="block text-sm font-medium text-gray-200 mb-1">
          Duration (MM:SS)
        </label>
        <input
          id="duration"
          name="duration"
          type="text"
          className={inputClasses}
          placeholder="MM:SS"
          aria-label="Duration"
          defaultValue={millisecondsToMinutesSeconds(music?.duration)}
        />
      </div>

      <div>
        <label htmlFor="label" className="block text-sm font-medium text-gray-200 mb-1">
          Label
        </label>
        <SingleselectInput
          name="label"
          placeholder="Select Label..."
          addLabel="Add new Label:"
          options={labels}
          selectedValue={music?.label}
        />
      </div>

      <div>
        <label htmlFor="tracks" className="block text-sm font-medium text-gray-200 mb-1">
          Tracks
        </label>
        <input
          id="tracks"
          name="tracks"
          type="number"
          className={inputClasses}
          placeholder="Tracks"
          aria-label="Tracks"
          defaultValue={music?.tracks}
        />
      </div>

      <div>
        <label htmlFor="discs" className="block text-sm font-medium text-gray-200 mb-1">
          Discs
        </label>
        <input
          id="discs"
          name="discs"
          type="number"
          className={inputClasses}
          placeholder="Discs"
          aria-label="Discs"
          defaultValue={music?.discs}
        />
      </div>

      {/* Disc List Section */}
      <div>
        <div className="flex justify-between items-center mb-2">
          <span className="block text-sm font-medium text-gray-200">Disc List</span>
          <button type="button" onClick={addDisc}>
            Add Track
          </button>
        </div>
        {discList.length > 0 && (
          <div className="space-y-2">
            {discList.map((disc, index) => (
              <div key={index} className="flex gap-2 items-start border border-gray-600 p-3 rounded-md bg-gray-900">
                <input type="hidden" name={`discList[${index}].trackNumber`} value={disc.trackNumber} />
                <div className="flex-1">
                  <label htmlFor={`disc-title-${index}`} className="block text-xs text-gray-400 mb-1">
                    Track {index + 1} - Title
                  </label>
                  <input
                    id={`disc-title-${index}`}
                    name={`discList[${index}].title`}
                    type="text"
                    className={inputClasses}
                    placeholder="Track Title"
                    value={disc.title}
                    onChange={(e) => updateDisc(index, 'title', e.target.value)}
                  />
                </div>
                <div className="w-32">
                  <label htmlFor={`disc-duration-${index}`} className="block text-xs text-gray-400 mb-1">
                    Duration
                  </label>
                  <input
                    id={`disc-duration-${index}`}
                    name={`discList[${index}].duration`}
                    type="text"
                    className={inputClasses}
                    placeholder="mm:ss"
                    value={millisecondsToMinutesSeconds(disc.duration)}
                    onChange={(e) => updateDisc(index, 'duration', e.target.value)}
                  />
                </div>
                <button
                  type="button"
                  onClick={() => removeDisc(index)}
                  className="mt-6 bg-red-600 text-white py-2 px-3 rounded-md hover:bg-red-700 text-sm"
                >
                  Remove
                </button>
              </div>
            ))}
          </div>
        )}
      </div>
    </fieldset>
  );
}
