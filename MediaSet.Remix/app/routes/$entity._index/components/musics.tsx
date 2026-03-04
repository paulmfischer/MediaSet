import { Link } from '@remix-run/react';
import { Pencil, Trash2 } from 'lucide-react';
import { useState } from 'react';
import DeleteDialog from '~/components/delete-dialog';
import ImageDisplay from '~/components/image-display';
import SortableColumnHeader from '~/components/sortable-column-header';
import { MusicEntity, Entity } from '~/models';

type MusicsProps = {
  musics: MusicEntity[];
  apiUrl?: string;
  orderBy?: string;
  searchText?: string | null;
};

export default function Musics({ musics, apiUrl, orderBy = 'title:asc', searchText }: MusicsProps) {
  const [deleteDialogState, setDeleteDialogState] = useState<{ isOpen: boolean; music: MusicEntity | null }>({
    isOpen: false,
    music: null,
  });

  return (
    <>
      <div className="md:hidden flex flex-col divide-y divide-slate-700">
        {musics.map((music) => (
          <div key={music.id} className="flex flex-row items-center gap-3 py-2 px-2 dark:hover:bg-zinc-800">
            {music.coverImage && (
              <div className="flex-shrink-0">
                <ImageDisplay
                  imageData={music.coverImage}
                  alt={`${music.title} cover`}
                  entityType={Entity.Musics}
                  entityId={music.id}
                  apiUrl={apiUrl}
                  size="xsmall"
                />
              </div>
            )}
            <div className="flex-1 min-w-0">
              <Link to={`/musics/${music.id}`} className="font-medium block truncate">
                {music.title}
              </Link>
              <div className="text-xs text-gray-400 truncate">
                {[music.artist, music.format].filter(Boolean).join(' · ')}
              </div>
            </div>
            <div className="flex flex-row gap-3 flex-shrink-0">
              <Link to={`/musics/${music.id}/edit`} aria-label="Edit" title="Edit">
                <Pencil size={18} />
              </Link>
              <button
                onClick={() => setDeleteDialogState({ isOpen: true, music })}
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
              label="Artist"
              field="artist"
              currentOrderBy={orderBy}
              searchText={searchText}
              className="hidden md:table-cell"
            />
            <SortableColumnHeader
              label="Format"
              field="format"
              currentOrderBy={orderBy}
              searchText={searchText}
              className="hidden lg:table-cell"
            />
            <SortableColumnHeader
              label="Tracks"
              field="tracks"
              currentOrderBy={orderBy}
              searchText={searchText}
              className="hidden xl:table-cell"
            />
            <th className="w-1"></th>
          </tr>
        </thead>
        <tbody>
          {musics.map((music) => {
            return (
              <tr className="border-b border-slate-700 dark:hover:bg-zinc-800" key={music.id}>
                <td className="hidden md:table-cell w-16 pl-2 p-1 border-r border-slate-800">
                  {music.coverImage && (
                    <ImageDisplay
                      imageData={music.coverImage}
                      alt={`${music.title} cover`}
                      entityType={Entity.Musics}
                      entityId={music.id}
                      apiUrl={apiUrl}
                      size="xsmall"
                    />
                  )}
                </td>
                <td className="pl-2 p-1 border-r border-slate-800">
                  <Link to={`/musics/${music.id}`}>{music.title}</Link>
                </td>
                <td className="hidden md:table-cell pl-2 p-1 border-r border-slate-800">{music.artist}</td>
                <td className="hidden lg:table-cell pl-2 p-1 border-r border-slate-800">{music.format}</td>
                <td className="hidden xl:table-cell pl-2 p-1 border-r border-slate-800">{music.tracks}</td>
                <td className="flex flex-row gap-3 p-1 pt-2">
                  <Link to={`/musics/${music.id}/edit`} aria-label="Edit" title="Edit">
                    <Pencil size={18} />
                  </Link>
                  <button
                    onClick={() => setDeleteDialogState({ isOpen: true, music })}
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
        onClose={() => setDeleteDialogState({ isOpen: false, music: null })}
        entityTitle={deleteDialogState.music?.title}
        deleteAction={deleteDialogState.music ? `/musics/${deleteDialogState.music.id}/delete` : ''}
      />
    </>
  );
}
