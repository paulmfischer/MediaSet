import MultiselectInput from "~/components/multiselect-input";
import { useSubmit } from "@remix-run/react";
import { FormProps, GameEntity } from "~/models";

type Metadata = {
  label: string;
  value: string;
};
type GameFormProps = FormProps & {
  game?: GameEntity;
  developers: Metadata[];
  publishers: Metadata[];
  genres: Metadata[];
  formats: Metadata[];
};

export default function GameForm({ game, developers, publishers, genres, formats, isSubmitting }: GameFormProps) {
  const submit = useSubmit();
  const inputClasses = "w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400";
  const selectClasses = "w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400";
  const textareaClasses = "w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400 resize-vertical min-h-[100px]";

  const handleLookup = () => {
    const barcodeInput = document.getElementById('barcode') as HTMLInputElement;
    const barcodeValue = barcodeInput?.value;
    if (!barcodeValue) return;
    const formData = new FormData();
    formData.append('intent', 'lookup');
    formData.append('fieldName', 'barcode');
    formData.append('identifierValue', barcodeValue);
    submit(formData, { method: 'post' });
  };

  return (
    <fieldset disabled={isSubmitting} className="flex flex-col gap-4">
      <input hidden id="id" name="id" type="text" defaultValue={game?.id} />
      
      <div>
        <label htmlFor="title" className="block text-sm font-medium text-gray-200 mb-1">Title</label>
        <input id="title" name="title" type="text" className={inputClasses} placeholder="Title" aria-label="Title" defaultValue={game?.title} />
      </div>
      
      <div>
        <label htmlFor="platform" className="block text-sm font-medium text-gray-200 mb-1">Platform</label>
        <input id="platform" name="platform" type="text" className={inputClasses} placeholder="Platform" aria-label="Platform" defaultValue={game?.platform} />
      </div>
      
      <div>
        <label htmlFor="format" className="block text-sm font-medium text-gray-200 mb-1">Format</label>
        <select key={game?.format ?? 'no-format'} id="format" name="format" className={selectClasses} defaultValue={game?.format}>
          <option value="">Select Format...</option>
          {formats.map(format => <option key={format.value} value={format.value}>{format.label}</option>)}
        </select>
      </div>
      
      <div>
        <label htmlFor="releaseDate" className="block text-sm font-medium text-gray-200 mb-1">Release Date</label>
        <input id="releaseDate" name="releaseDate" type="text" className={inputClasses} placeholder="Release Date" defaultValue={game?.releaseDate} aria-label="Release Date" />
      </div>
      
      <div>
        <label htmlFor="rating" className="block text-sm font-medium text-gray-200 mb-1">Age Rating</label>
        <input id="rating" name="rating" type="text" className={inputClasses} placeholder="Age Rating (e.g., E, T, M)" defaultValue={game?.rating} aria-label="Age Rating" />
      </div>
      
      <div>
        <label htmlFor="developers" className="block text-sm font-medium text-gray-200 mb-1">Developers</label>
        <MultiselectInput name="developers" selectText="Select Developers..." addLabel="Add new Developer:" options={developers} selectedValues={game?.developers} />
      </div>

      <div>
        <label htmlFor="publishers" className="block text-sm font-medium text-gray-200 mb-1">Publishers</label>
        <MultiselectInput name="publishers" selectText="Select Publishers..." addLabel="Add new Publisher:" options={publishers} selectedValues={game?.publishers} />
      </div>

      <div>
        <label htmlFor="genres" className="block text-sm font-medium text-gray-200 mb-1">Genres</label>
        <MultiselectInput name="genres" selectText="Select Genres..." addLabel="Add new Genre" options={genres} selectedValues={game?.genres} />
      </div>
      
      <div>
        <label htmlFor="barcode" className="block text-sm font-medium text-gray-200 mb-1">Barcode</label>
        <div className="flex gap-2">
          <input id="barcode" name="barcode" type="text" className={inputClasses} placeholder="Barcode" defaultValue={game?.barcode} aria-label="Barcode" />
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
        <label htmlFor="description" className="block text-sm font-medium text-gray-200 mb-1">Description</label>
        <textarea id="description" name="description" className={textareaClasses} placeholder="Description" defaultValue={game?.description} aria-label="Description" />
      </div>
    </fieldset>
  );
}
