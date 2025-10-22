import { useState } from "react";
import MultiselectInput from "~/components/multiselect-input";
import { FormProps, MusicEntity, Disc } from "~/models";

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
};

export default function MusicForm({ music, genres, formats, labels, isSubmitting }: MusicFormProps) {
  const [discList, setDiscList] = useState<Disc[]>(music?.discList ?? []);

  const inputClasses = "w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400";
  const selectClasses = "w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400";

  const addDisc = () => {
    setDiscList([...discList, { trackNumber: discList.length + 1, title: "", duration: "" }]);
  };

  const removeDisc = (index: number) => {
    setDiscList(discList.filter((_, i) => i !== index));
  };

  const updateDisc = (index: number, field: keyof Disc, value: string | number) => {
    const newDiscList = [...discList];
    newDiscList[index] = { ...newDiscList[index], [field]: value };
    setDiscList(newDiscList);
  };

  return (
    <fieldset disabled={isSubmitting} className="flex flex-col gap-4">
      <input hidden id="id" name="id" type="text" defaultValue={music?.id} />

      <div>
        <label htmlFor="title" className="block text-sm font-medium text-gray-200 mb-1">Title</label>
        <input id="title" name="title" type="text" className={inputClasses} placeholder="Title" aria-label="Title" defaultValue={music?.title} />
      </div>

      <div>
        <label htmlFor="artist" className="block text-sm font-medium text-gray-200 mb-1">Artist</label>
        <input id="artist" name="artist" type="text" className={inputClasses} placeholder="Artist" aria-label="Artist" defaultValue={music?.artist} />
      </div>

      <div>
        <label htmlFor="format" className="block text-sm font-medium text-gray-200 mb-1">Format</label>
        <select key={music?.format ?? 'no-format'} id="format" name="format" className={selectClasses} defaultValue={music?.format}>
          <option value="">Select Format...</option>
          {formats.map(format => <option key={format.value} value={format.value}>{format.label}</option>)}
        </select>
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
        <label htmlFor="duration" className="block text-sm font-medium text-gray-200 mb-1">Duration (minutes)</label>
        <input id="duration" name="duration" type="number" className={inputClasses} placeholder="Duration" aria-label="Duration" defaultValue={music?.duration} />
      </div>

      <div>
        <label htmlFor="label" className="block text-sm font-medium text-gray-200 mb-1">Label</label>
        <select key={music?.label ?? 'no-label'} id="label" name="label" className={selectClasses} defaultValue={music?.label}>
          <option value="">Select Label...</option>
          {labels.map(label => <option key={label.value} value={label.value}>{label.label}</option>)}
        </select>
      </div>

      <div>
        <label htmlFor="barcode" className="block text-sm font-medium text-gray-200 mb-1">Barcode</label>
        <input id="barcode" name="barcode" type="text" className={inputClasses} placeholder="Barcode" defaultValue={music?.barcode} aria-label="Barcode" />
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
            className="bg-blue-600 text-white py-1 px-3 rounded-md hover:bg-blue-700 text-sm"
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
                    value={disc.duration}
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
