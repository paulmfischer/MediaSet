import { Link } from '@remix-run/react';
import { Pencil, Trash2, Check } from 'lucide-react';
import { useState } from 'react';
import DeleteDialog from '~/components/delete-dialog';
import ImageDisplay from '~/components/image-display';
import SortableColumnHeader from '~/components/sortable-column-header';
import { MovieEntity, Entity } from '~/models';

type MovieProps = {
  movies: MovieEntity[];
  apiUrl?: string;
  orderBy?: string;
  searchText?: string | null;
};

export default function Movies({ movies, apiUrl, orderBy = 'title:asc', searchText }: MovieProps) {
  const [deleteDialogState, setDeleteDialogState] = useState<{ isOpen: boolean; movie: MovieEntity | null }>({
    isOpen: false,
    movie: null,
  });

  return (
    <>
      <div className="md:hidden flex flex-col divide-y divide-slate-700">
        {movies.map((movie) => (
          <div key={movie.id} className="flex flex-row items-center gap-3 py-2 px-2 dark:hover:bg-zinc-800">
            {movie.coverImage && (
              <div className="flex-shrink-0">
                <ImageDisplay
                  imageData={movie.coverImage}
                  alt={`${movie.title} cover`}
                  entityType={Entity.Movies}
                  entityId={movie.id}
                  apiUrl={apiUrl}
                  size="xsmall"
                />
              </div>
            )}
            <div className="flex-1 min-w-0">
              <Link to={`/movies/${movie.id}`} className="font-medium block truncate">
                {movie.title}
              </Link>
              <div className="text-xs text-gray-400 truncate">
                {[movie.format, movie.runtime ? `${movie.runtime} min` : null, movie.isTvSeries ? 'TV Show' : null]
                  .filter(Boolean)
                  .join(' · ')}
              </div>
            </div>
            <div className="flex flex-row gap-3 flex-shrink-0">
              <Link to={`/movies/${movie.id}/edit`} aria-label="Edit" title="Edit">
                <Pencil size={18} />
              </Link>
              <button
                onClick={() => setDeleteDialogState({ isOpen: true, movie })}
                className="link"
                type="button"
                aria-label="Delete"
                title="Delete"
              >
                <Trash2 size={18} />
              </button>
            </div>
          </div>
        ))}
      </div>
      <table className="hidden md:table text-left w-full">
        <thead className="dark:bg-zinc-700 border-b-2 border-slate-600">
          <tr>
            <th className="hidden md:table-cell pl-2 p-1 border-r border-slate-800">Cover</th>
            <SortableColumnHeader label="Title" field="title" currentOrderBy={orderBy} searchText={searchText} />
            <SortableColumnHeader
              label="Format"
              field="format"
              currentOrderBy={orderBy}
              searchText={searchText}
              className="hidden md:table-cell"
            />
            <SortableColumnHeader
              label="Runtime"
              field="runtime"
              currentOrderBy={orderBy}
              searchText={searchText}
              className="hidden lg:table-cell"
            />
            <SortableColumnHeader
              label="TV Show"
              field="isTvSeries"
              currentOrderBy={orderBy}
              searchText={searchText}
              className="hidden xl:table-cell"
            />
            <th className="w-1"></th>
          </tr>
        </thead>
        <tbody>
          {movies.map((movie) => {
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
                  <div className="flex justify-center">{movie.isTvSeries && <Check size={16} />}</div>
                </td>
                <td className="flex flex-row gap-3 p-1 pt-2">
                  <Link to={`/movies/${movie.id}/edit`} aria-label="Edit" title="Edit">
                    <Pencil size={18} />
                  </Link>
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
            );
          })}
        </tbody>
      </table>
      <DeleteDialog
        isOpen={deleteDialogState.isOpen}
        onClose={() => setDeleteDialogState({ isOpen: false, movie: null })}
        entityTitle={deleteDialogState.movie?.title}
        deleteAction={deleteDialogState.movie ? `/movies/${deleteDialogState.movie.id}/delete` : ''}
      />
    </>
  );
}
