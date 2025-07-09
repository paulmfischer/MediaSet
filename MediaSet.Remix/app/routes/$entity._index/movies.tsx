
import { Form, Link } from "@remix-run/react";
import { Pencil, Trash2, Check } from "lucide-react";
import { MovieEntity } from "~/models";

type MovieProps = {
  movies: MovieEntity[]
};

export default function Movies({ movies }: MovieProps) {
  console.log('movies', movies.filter(movie => movie.isTvSeries));
  return (
    <table className="text-left w-full">
      <thead className="dark:bg-zinc-700 border-b-2 border-slate-600">
        <tr>
          <th className="pl-2 p-1 border-r border-slate-800 underline">Title</th>
          <th className="hidden sm:table-cell pl-2 p-1 border-r border-slate-800 underline">Format</th>
          <th className="hidden sm:table-cell pl-2 p-1 border-r border-slate-800 underline">Runtime</th>
          <th className="hidden sm:table-cell pl-2 p-1 border-r border-slate-800 underline">TV Show</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        {movies.map(movie => {
          return (
            <tr className="border-b border-slate-700 dark:hover:bg-zinc-800" key={movie.id}>
              <td className="pl-2 p-1 border-r border-slate-800">
                <Link to={`/movies/${movie.id}`}>{movie.title}</Link>
              </td>
              <td className="hidden sm:table-cell pl-2 p-1 border-r border-slate-800">{movie.format}</td>
              <td className="hidden sm:table-cell pl-2 p-1 border-r border-slate-800">{movie.runtime ?? 0}</td>
              <td className="hidden sm:table-cell pl-2 p-1 border-r border-slate-800">
                <div className="flex justify-center">
                  {movie.isTvSeries && <Check size={18} />}
                </div>
              </td>
              <td className="flex flex-row gap-3 p-1 pt-2">
                <Link to={`/movies/${movie.id}/edit`} aria-label="Edit" title="Edit"><Pencil size={18} /></Link>
                <Form action={`/movies/${movie.id}/delete`} method="post" onSubmit={(event) => {
                  const response = confirm(`Are you sure you want to delete ${movie.title}?`);
                  if (!response) {
                    event.preventDefault();
                  }
                }}>
                  <button className="link" type="submit" aria-label="Delete" title="Delete"><Trash2 size={18} /></button>
                </Form>
              </td>
            </tr>
          )
        })}
      </tbody>
    </table>
  )
}