import { useState } from "react";
import MultiselectInput from "~/components/multiselect-input";
import { FormProps, MovieEntity } from "~/models";

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
  onLookup?: (barcode: string) => void;
  isLookingUp?: boolean;
};

export default function MovieForm({ movie, genres, studios, formats, isSubmitting, onLookup, isLookingUp }: MovieFormProps) {
  const [isTvSeries, setIsTvSeries] = useState(movie?.isTvSeries ?? false);
  const isTvSeriesChanged = () => setIsTvSeries(!isTvSeries);

  const inputClasses = "w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400";
  const selectClasses = "w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400";
  const textareaClasses = "w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400 resize-vertical min-h-[100px]";

  const handleLookupClick = () => {
    const barcodeInput = document.getElementById("barcode") as HTMLInputElement;
    if (barcodeInput && barcodeInput.value && onLookup) {
      onLookup(barcodeInput.value);
    }
  };

  return (
    <fieldset disabled={isSubmitting} className="flex flex-col gap-4">
      <input hidden id="id" name="id" type="text" defaultValue={movie?.id} />

      <div>
        <label htmlFor="title" className="block text-sm font-medium text-gray-200 mb-1">Title</label>
        <input id="title" name="title" type="text" className={inputClasses} placeholder="Title" aria-label="Title" defaultValue={movie?.title} />
      </div>

      <div>
        <label htmlFor="format" className="block text-sm font-medium text-gray-200 mb-1">Format</label>
        <select key={movie?.format ?? 'no-format'} id="format" name="format" className={selectClasses} defaultValue={movie?.format}>
          <option value="">Select Format...</option>
          {formats.map(format => <option key={format.value} value={format.value}>{format.label}</option>)}
        </select>
      </div>

      <div>
        <label htmlFor="runtime" className="block text-sm font-medium text-gray-200 mb-1">Runtime</label>
        <input id="runtime" name="runtime" type="number" className={inputClasses} placeholder="Runtime" aria-label="Runtime" defaultValue={movie?.runtime} />
      </div>

      <div>
        <label htmlFor="releaseDate" className="block text-sm font-medium text-gray-200 mb-1">Release Date</label>
        <input id="releaseDate" name="releaseDate" type="text" className={inputClasses} placeholder="Release Date" defaultValue={movie?.releaseDate} aria-label="Release Date" />
      </div>

      <div className="flex items-center gap-2">
        <input id="isTvSeries" name="isTvSeries" type="checkbox" value="true" checked={isTvSeries} onChange={isTvSeriesChanged} aria-label="Is TV Series" className="form-checkbox h-5 w-5 text-blue-400 bg-gray-800 border-gray-600 focus:ring-blue-400" />
        <label htmlFor="isTvSeries" className="block text-sm font-medium text-gray-200">Is TV Series</label>
      </div>

      <div>
        <label htmlFor="studios" className="block text-sm font-medium text-gray-200 mb-1">Studios</label>
        <MultiselectInput name="studios" selectText="Select Studios..." addLabel="Add new Studio:" options={studios} selectedValues={movie?.studios} />
      </div>

      <div>
        <label htmlFor="genres" className="block text-sm font-medium text-gray-200 mb-1">Genres</label>
        <MultiselectInput name="genres" selectText="Select Genres..." addLabel="Add new Genre" options={genres} selectedValues={movie?.genres} />
      </div>

      <div>
        <label htmlFor="barcode" className="block text-sm font-medium text-gray-200 mb-1">Barcode</label>
        <div className="flex gap-2">
          <input id="barcode" name="barcode" type="text" className={inputClasses} placeholder="Barcode" defaultValue={movie?.barcode} aria-label="Barcode" />
          {onLookup && (
            <button
              type="button"
              onClick={handleLookupClick}
              disabled={isSubmitting || isLookingUp}
              className="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-400 disabled:opacity-50 whitespace-nowrap"
            >
              {isLookingUp ? "Looking up..." : "Lookup"}
            </button>
          )}
        </div>
      </div>

      <div>
        <label htmlFor="plot" className="block text-sm font-medium text-gray-200 mb-1">Plot</label>
        <textarea id="plot" name="plot" className={textareaClasses} placeholder="Plot" defaultValue={movie?.plot} aria-label="Plot" />
      </div>
    </fieldset>
  );
}