import { useState, useEffect } from "react";
import { useSubmit } from "@remix-run/react";
import MultiselectInput from "~/components/multiselect-input";
import SingleselectInput from "~/components/singleselect-input";
import ImageUpload from "~/components/image-upload";
import ImageUrlPreview from "~/components/image-url-preview";
import { FormProps, MusicEntity, Disc } from "~/models";
import { millisecondsToMinutesSeconds } from "~/utils/helpers";
import ScanButton from "~/components/scan-button";

type Metadata = {
  label: string;
  value: string;
};
type MusicFormProps = FormProps & {
  isSubmitting?: boolean;
  music?: MusicEntity;
  genres: Metadata[];
  formats: Metadata[];
  labels: Metadata[];
  barcodeLookupAvailable?: boolean;
};

export default function MusicForm({ music, genres, formats, labels, isSubmitting, barcodeLookupAvailable }: MusicFormProps) {
  const submit = useSubmit();
  const [discList, setDiscList] = useState<Disc[]>(music?.discList ?? []);

  // Update disc list when music data changes (e.g., after lookup)
  useEffect(() => {
    if (music?.discList) {
      setDiscList(music.discList);
    }
  }, [music]);

  const inputClasses = "w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400";

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

  const addDisc = () => {
    setDiscList([...discList, { trackNumber: discList.length + 1, title: "", duration: null }]);
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
        <label htmlFor="title" className="block text-sm font-medium text-gray-200 mb-1">Title</label>
        <input id="title" name="title" type="text" className={inputClasses} placeholder="Title" aria-label="Title" defaultValue={music?.title} />
      </div>

      <ImageUpload name="coverImage" existingImage={music?.coverImage} isSubmitting={isSubmitting} />

      <div>
        <label htmlFor="imageUrl" className="block text-sm font-medium text-gray-200 mb-1">Image URL</label>
        <p className="text-xs text-gray-400 mb-2">Provide an image URL or upload a file above. If both are provided, the uploaded file takes precedence. Accepted: JPEG or PNG, up to 5MB.</p>
        <div className="flex items-start">
          <input id="imageUrl" name="imageUrl" type="url" className={`${inputClasses} flex-1`} placeholder="https://example.com/image.jpg" aria-label="Image URL" defaultValue={music?.imageUrl} />
          <ImageUrlPreview inputId="imageUrl" existingUrl={music?.imageUrl} />
        </div>
      </div>

      <div>
        <label htmlFor="artist" className="block text-sm font-medium text-gray-200 mb-1">Artist</label>
        <input id="artist" name="artist" type="text" className={inputClasses} placeholder="Artist" aria-label="Artist" defaultValue={music?.artist} />
      </div>

      <div>
        <label htmlFor="format" className="block text-sm font-medium text-gray-200 mb-1">Format</label>
        <SingleselectInput name="format" placeholder="Select Format..." addLabel="Add new Format:" options={formats} selectedValue={music?.format} />
      </div>

      <div>
        <label htmlFor="releaseDate" className="block text-sm font-medium text-gray-200 mb-1">Release Date</label>
        <input id="releaseDate" name="releaseDate" type="text" className={inputClasses} placeholder="Release Date" defaultValue={music?.releaseDate} aria-label="Release Date" />
      </div>

      <div>
        <label htmlFor="genres" className="block text-sm font-medium text-gray-200 mb-1">Genres</label>
        <MultiselectInput name="genres" selectText="Select Genres..." addLabel="Add new Genre" options={genres} selectedValues={music?.genres} />
      </div>

      <div>
        <label htmlFor="duration" className="block text-sm font-medium text-gray-200 mb-1">Duration (MM:SS)</label>
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
        <label htmlFor="label" className="block text-sm font-medium text-gray-200 mb-1">Label</label>
        <SingleselectInput name="label" placeholder="Select Label..." addLabel="Add new Label:" options={labels} selectedValue={music?.label} />
      </div>

      <div>
        <label htmlFor="barcode" className="block text-sm font-medium text-gray-200 mb-1">Barcode</label>
        <div className="flex gap-2">
          <input id="barcode" name="barcode" type="text" inputMode="numeric" className={inputClasses} placeholder="Barcode" defaultValue={music?.barcode} aria-label="Barcode" onKeyDown={handleKeyDown} />
          {barcodeLookupAvailable && (
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
        <label htmlFor="tracks" className="block text-sm font-medium text-gray-200 mb-1">Tracks</label>
        <input id="tracks" name="tracks" type="number" className={inputClasses} placeholder="Tracks" aria-label="Tracks" defaultValue={music?.tracks} />
      </div>

      <div>
        <label htmlFor="discs" className="block text-sm font-medium text-gray-200 mb-1">Discs</label>
        <input id="discs" name="discs" type="number" className={inputClasses} placeholder="Discs" aria-label="Discs" defaultValue={music?.discs} />
      </div>

      {/* Disc List Section */}
      <div>
        <div className="flex justify-between items-center mb-2">
          <label className="block text-sm font-medium text-gray-200">Disc List</label>
          <button 
            type="button" 
            onClick={addDisc}
          >
            Add Track
          </button>
        </div>
        {discList.length > 0 && (
          <div className="space-y-2">
            {discList.map((disc, index) => (
              <div key={index} className="flex gap-2 items-start border border-gray-600 p-3 rounded-md bg-gray-900">
                <input type="hidden" name={`discList[${index}].trackNumber`} value={disc.trackNumber} />
                <div className="flex-1">
                  <label htmlFor={`disc-title-${index}`} className="block text-xs text-gray-400 mb-1">Track {index + 1} - Title</label>
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
                  <label htmlFor={`disc-duration-${index}`} className="block text-xs text-gray-400 mb-1">Duration</label>
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
