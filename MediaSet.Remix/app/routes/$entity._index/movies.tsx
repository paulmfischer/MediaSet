
import { Link } from "@remix-run/react";
import { Pencil, Trash2, Check } from "lucide-react";
import { useState } from "react";
import DeleteDialog from "~/components/delete-dialog";
import ImageDisplay from "~/components/image-display";
import { MovieEntity, Entity } from "~/models";

type MovieProps = {
  movies: MovieEntity[];
  apiUrl?: string;
};

export default function Movies({ movies, apiUrl }: MovieProps) {
  const [deleteDialogState, setDeleteDialogState] = useState<{ isOpen: boolean; movie: MovieEntity | null }>({
    isOpen: false,
    movie: null
  });

  return (
    <>
      <table className="text-left w-full">
        <thead className="dark:bg-zinc-700 border-b-2 border-slate-600">
          <tr>
            <th className="hidden md:table-cell pl-2 p-1 border-r border-slate-800 underline">Cover</th>
            <th className="pl-2 p-1 border-r border-slate-800 underline">Title</th>
            <th className="hidden md:table-cell pl-2 p-1 border-r border-slate-800 underline">Format</th>
            <th className="hidden lg:table-cell pl-2 p-1 border-r border-slate-800 underline">Runtime</th>
            <th className="hidden xl:table-cell pl-2 p-1 border-r border-slate-800 underline">TV Show</th>
            <th className="w-1"></th>
          </tr>
        </thead>
        <tbody>
          {movies.map(movie => {
            return (
              <tr className="border-b border-slate-700 dark:hover:bg-zinc-800" key={movie.id}>
                <td className="hidden md:table-cell w-16 pl-2 p-1 border-r border-slate-800">
                  {movie.coverImage && (
                    <ImageDisplay
                      imageData={movie.coverImage}
                      alt={`${movie.title} cover`}
                      entityType={Entity.Movies}
                      entityId={movie.id}
                      apiUrl={apiUrl}
                      size="xsmall"
                    />
                  )}
                </td>
                <td className="pl-2 p-1 border-r border-slate-800">
                  <Link to={`/movies/${movie.id}`}>{movie.title}</Link>
                </td>
                <td className="hidden md:table-cell pl-2 p-1 border-r border-slate-800">{movie.format}</td>
                <td className="hidden lg:table-cell pl-2 p-1 border-r border-slate-800">{movie.runtime ?? 0}</td>
                <td className="hidden xl:table-cell pl-2 p-1 border-r border-slate-800">
                  <div className="flex justify-center">
                    {movie.isTvSeries && <Check size={16} />}
                  </div>
                </td>
                <td className="flex flex-row gap-3 p-1 pt-2">
                  <Link to={`/movies/${movie.id}/edit`} aria-label="Edit" title="Edit"><Pencil size={18} /></Link>
                  <button
                    onClick={() => setDeleteDialogState({ isOpen: true, movie })}
                    className="link"
                    type="button"
                    aria-label="Delete"
                    title="Delete"
                  >
                    <Trash2 size={18} />
                  </button>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
      <DeleteDialog
        isOpen={deleteDialogState.isOpen}
        onClose={() => setDeleteDialogState({ isOpen: false, movie: null })}
        entityTitle={deleteDialogState.movie?.title}
        deleteAction={deleteDialogState.movie ? `/movies/${deleteDialogState.movie.id}/delete` : ""}
      />
    </>
  )
}