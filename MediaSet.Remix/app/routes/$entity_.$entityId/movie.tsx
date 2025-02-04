import { Form, Link } from "@remix-run/react";
import { Pencil, Trash2 } from "lucide-react";
import { MovieEntity } from "~/models"

type MovieProps = {
  movie: MovieEntity;
};

export default function Movie({ movie }: MovieProps) {
  return (
    <div className="flex flex-col">
      <div className="flex flex-row items-center justify-between border-b-2 border-slate-600 pb-2">
        <div className="flex flex-col gap-2 items-baseline">
          <h2 className="text-2xl">{movie.title}</h2>
        </div>
        <div className="flex flex-row gap-2">
          <Link to={`/movies/${movie.id}/edit`} aria-label="Edit" title="Edit"><Pencil size={22} /></Link>
          <Form action={`/movies/${movie.id}/delete`} method="post" onSubmit={(event) => {
            const response = confirm(`Are you sure you want to delete ${movie.title}?`);
            if (!response) {
              event.preventDefault();
            }
          }}>
            <button className="link" type="submit" aria-label="Delete" title="Delete"><Trash2 size={22} /></button>
          </Form>
        </div>
      </div>
      <div className="h-full mt-4">
        <div className="flex flex-col md:flex-row mb-2 md:mb-0">
          <label htmlFor="format" className="basis-2/12 dark:text-slate-400">Format</label>
          <div id="format" className="grow">{movie.format}</div>
        </div>
        <div className="flex flex-col md:flex-row mb-2 md:mb-0">
          <label htmlFor="pages" className="basis-2/12 dark:text-slate-400">Runtime</label>
          <div id="pages" className="grow">{movie.runtime}</div>
        </div>
        <div className="flex flex-col md:flex-row mb-2 md:mb-0">
          <label htmlFor="authors" className="basis-2/12 dark:text-slate-400">Release Date</label>
          <div id="authors" className="grow">{movie.releaseDate}</div>
        </div>
        <div className="flex flex-col md:flex-row mb-2 md:mb-0">
          <label htmlFor="genres" className="basis-2/12 dark:text-slate-400">Genres</label>
          <div id="genres" className="grow">{movie.genres?.join(', ')}</div>
        </div>
        <div className="flex flex-col md:flex-row mb-2 md:mb-0">
          <label htmlFor="publisher" className="basis-2/12 dark:text-slate-400">Studios</label>
          <div id="publisher" className="grow">{movie.studios?.join(', ')}</div>
        </div>
        <div className="flex flex-col md:flex-row mb-2 md:mb-0">
          <label htmlFor="isbn" className="basis-2/12 dark:text-slate-400">ISBN</label>
          <div id="isbn" className="grow">{movie.isbn}</div>
        </div>
        <div className="flex flex-col md:flex-row mb-2 md:mb-0">
          <label htmlFor="plot" className="basis-2/12 dark:text-slate-400">Plot</label>
          <div id="plot" className="basis-3/4">{movie.plot}</div>
        </div>
      </div>
    </div>
  );
}