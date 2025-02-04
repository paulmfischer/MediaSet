import MultiselectInput from "~/components/multiselect-input";
import { FormProps, MovieEntity } from "~/models";

type Metadata = {
  label: string;
  value: string;
};
type BookFormProps = FormProps & {
  isSubmitting?: boolean;
  movie?: MovieEntity;
  studios: Metadata[];
  genres: Metadata[];
  formats: Metadata[];
};

export default function MovieForm({ movie, genres, studios, formats, isSubmitting }: BookFormProps) {
  return (
    <fieldset disabled={isSubmitting} className="flex flex-col gap-2">
      <label htmlFor="title" className="dark:text-slate-400">Title</label>
      <input id="title" name="title" type="text" placeholder="Title" aria-label="Title" />
      <label htmlFor="format" className="dark:text-slate-400">Format</label>
      <select id="format" name="format">
        <option value="">Select Format...</option>
        {formats.map(format => <option key={format.value} value={format.value}>{format.label}</option>)}
      </select>
      <label htmlFor="runtime" className="dark:text-slate-400">Runtime</label>
      <input id="runtime" name="runtime" type="number" placeholder="Runtime" aria-label="Runtime" />
      <label htmlFor="releaseDate" className="dark:text-slate-400">Release Date</label>
      <input id="releaseDate" name="releaseDate" type="text" placeholder="Release Date" aria-label="Release Date" />
      <label htmlFor="studios" className="dark:text-slate-400">Studios</label>
      <MultiselectInput name="studios" selectText="Select Studios..." addLabel="Add new Studio:" options={studios} />
      <label htmlFor="genres" className="dark:text-slate-400">Genres</label>
      <MultiselectInput name="genres" selectText="Select Genres..." addLabel="Add new Genre" options={genres} />
      <label htmlFor="isbn" className="dark:text-slate-400">ISBN</label>
      <input id="isbn" name="isbn" type="text" placeholder="ISBN" aria-label="ISBN" />
      <label htmlFor="plot" className="dark:text-slate-400">Plot</label>
      <textarea id="plot" name="plot" placeholder="Plot" aria-label="Plot" />
    </fieldset>
  );
}