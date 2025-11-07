import { Link } from "@remix-run/react";
import { Pencil, Trash2, Check } from "lucide-react";
import { useState } from "react";
import DeleteDialog from "~/components/delete-dialog";
import { MovieEntity } from "~/models"

type MovieProps = {
  movie: MovieEntity;
};

export default function Movie({ movie }: MovieProps) {
  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);

  return (
    <div className="flex flex-col">
      <div className="flex flex-row items-center justify-between border-b-2 border-slate-600 pb-2">
        <div className="flex flex-col gap-2 items-baseline">
          <h2 className="text-2xl">{movie.title}</h2>
        </div>
        <div className="flex flex-row gap-2">
          <Link to={`/movies/${movie.id}/edit`} aria-label="Edit" title="Edit"><Pencil size={22} /></Link>
          <button
            onClick={() => setIsDeleteDialogOpen(true)}
            className="link"
            type="button"
            aria-label="Delete"
            title="Delete"
          >
            <Trash2 size={22} />
          </button>
        </div>
      </div>
      <div className="h-full mt-4">
        <div className="flex flex-col md:flex-row mb-2 md:mb-0">
          <label htmlFor="format" className="basis-2/12 dark:text-slate-400">Format</label>
          <div id="format" className="grow">{movie.format}</div>
        </div>
        <div className="flex flex-col md:flex-row mb-2 md:mb-0">
          <label htmlFor="runtime" className="basis-2/12 dark:text-slate-400">Runtime</label>
          <div id="runtime" className="grow">{movie.runtime}</div>
        </div>
        <div className="flex flex-col md:flex-row mb-2 md:mb-0">
          <label htmlFor="releaseDate" className="basis-2/12 dark:text-slate-400">Release Date</label>
          <div id="releaseDate" className="grow">{movie.releaseDate}</div>
        </div>
        <div className="flex flex-col md:flex-row mb-2 md:mb-0">
          <label htmlFor="tvShow" className="basis-2/12 dark:text-slate-400">Is TV Show</label>
          <div id="tvShow" className="grow">{movie.isTvSeries && <Check size={18} />}</div>
        </div>
        <div className="flex flex-col md:flex-row mb-2 md:mb-0">
          <label htmlFor="genres" className="basis-2/12 dark:text-slate-400">Genres</label>
          <div id="genres" className="grow">{movie.genres?.join(', ')}</div>
        </div>
        <div className="flex flex-col md:flex-row mb-2 md:mb-0">
          <label htmlFor="studios" className="basis-2/12 dark:text-slate-400">Studios</label>
          <div id="studios" className="grow">{movie.studios?.join(', ')}</div>
        </div>
        <div className="flex flex-col md:flex-row mb-2 md:mb-0">
          <label htmlFor="barcode" className="basis-2/12 dark:text-slate-400">Barcode</label>
          <div id="barcode" className="grow">{movie.barcode}</div>
        </div>
        <div className="flex flex-col md:flex-row mb-2 md:mb-0">
          <label htmlFor="plot" className="basis-2/12 dark:text-slate-400">Plot</label>
          <div id="plot" className="basis-3/4">{movie.plot}</div>
        </div>
      </div>
      <DeleteDialog
        isOpen={isDeleteDialogOpen}
        onClose={() => setIsDeleteDialogOpen(false)}
        entityTitle={movie.title}
        deleteAction={`/movies/${movie.id}/delete`}
      />
    </div>
  );
}