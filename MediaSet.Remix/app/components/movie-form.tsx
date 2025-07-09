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
};

export default function MovieForm({ movie, genres, studios, formats, isSubmitting }: MovieFormProps) {
  const [isTvSeries, setIsTvSeries] = useState(movie?.isTvSeries ?? false);
  const isTvSeriesChanged = () => setIsTvSeries(!isTvSeries);

  return (
    <fieldset disabled={isSubmitting} className="flex flex-col gap-2">
      <input hidden id="id" name="id" type="text" defaultValue={movie?.id} />
      <label htmlFor="title" className="dark:text-slate-400">Title</label>
      <input id="title" name="title" type="text" placeholder="Title" defaultValue={movie?.title} aria-label="Title" />
      <label htmlFor="format" className="dark:text-slate-400">Format</label>
      <select id="format" name="format"defaultValue={movie?.format} >
        <option value="">Select Format...</option>
        {formats.map(format => <option key={format.value} value={format.value}>{format.label}</option>)}
      </select>
      <label htmlFor="runtime" className="dark:text-slate-400">Runtime</label>
      <input id="runtime" name="runtime" type="number" placeholder="Runtime" defaultValue={movie?.runtime} aria-label="Runtime" />
      <label htmlFor="releaseDate" className="dark:text-slate-400">Release Date</label>
      <input id="releaseDate" name="releaseDate" type="text" placeholder="Release Date" defaultValue={movie?.releaseDate} aria-label="Release Date" />
      <label htmlFor="isTvSeries" className="dark:text-slate-400">Is TV Series</label>
      <input id="isTvSeries" name="isTvSeries" type="checkbox" value="true" checked={isTvSeries} onChange={isTvSeriesChanged} aria-label="Is TV Series" />
      <label htmlFor="studios" className="dark:text-slate-400">Studios</label>
      <MultiselectInput name="studios" selectText="Select Studios..." addLabel="Add new Studio:" options={studios} selectedValues={movie?.studios} />
      <label htmlFor="genres" className="dark:text-slate-400">Genres</label>
      <MultiselectInput name="genres" selectText="Select Genres..." addLabel="Add new Genre" options={genres} selectedValues={movie?.genres} />
      <label htmlFor="barcode" className="dark:text-slate-400">Barcode</label>
      <input id="barcode" name="barcode" type="text" placeholder="Barcode" defaultValue={movie?.barcode} aria-label="Barcode" />
      <label htmlFor="plot" className="dark:text-slate-400">Plot</label>
      <textarea id="plot" name="plot" placeholder="Plot" defaultValue={movie?.plot} aria-label="Plot" />
    </fieldset>
  );
}