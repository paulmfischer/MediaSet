import { Link } from '@remix-run/react';
import { Pencil, Trash2 } from 'lucide-react';
import { useState } from 'react';
import DeleteDialog from '~/components/delete-dialog';
import ImageDisplay from '~/components/image-display';
import SortableColumnHeader from '~/components/sortable-column-header';
import { GameEntity, Entity } from '~/models';

type GamesProps = {
  games: GameEntity[];
  apiUrl?: string;
  orderBy?: string;
  searchText?: string | null;
};

export default function Games({ games, apiUrl, orderBy = 'title:asc', searchText }: GamesProps) {
  const [deleteDialogState, setDeleteDialogState] = useState<{ isOpen: boolean; game: GameEntity | null }>({
    isOpen: false,
    game: null,
  });

  return (
    <>
      <div className="md:hidden flex flex-col divide-y divide-slate-700">
        {games.map((game) => (
          <div key={game.id} className="flex flex-row items-center gap-3 py-2 px-2 dark:hover:bg-zinc-800">
            {game.coverImage && (
              <div className="flex-shrink-0">
                <ImageDisplay
                  imageData={game.coverImage}
                  alt={`${game.title} cover`}
                  entityType={Entity.Games}
                  entityId={game.id}
                  apiUrl={apiUrl}
                  size="xsmall"
                />
              </div>
            )}
            <div className="flex-1 min-w-0">
              <Link to={`/games/${game.id}`} className="font-medium block truncate">
                {game.title}
              </Link>
              <div className="text-xs text-gray-400 truncate">
                {[game.platform, game.format].filter(Boolean).join(' · ')}
              </div>
            </div>
            <div className="flex flex-row gap-3 flex-shrink-0">
              <Link to={`/games/${game.id}/edit`} aria-label="Edit" title="Edit">
                <Pencil size={18} />
              </Link>
              <button
                onClick={() => setDeleteDialogState({ isOpen: true, game })}
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
              label="Platform"
              field="platform"
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
              label="Developers"
              field="developers"
              currentOrderBy={orderBy}
              searchText={searchText}
              className="hidden xl:table-cell"
            />
            <th className="w-1"></th>
          </tr>
        </thead>
        <tbody>
          {games.map((game) => {
            return (
              <tr className="border-b border-slate-700 dark:hover:bg-zinc-800" key={game.id}>
                <td className="hidden md:table-cell w-16 pl-2 p-1 border-r border-slate-800">
                  {game.coverImage && (
                    <ImageDisplay
                      imageData={game.coverImage}
                      alt={`${game.title} cover`}
                      entityType={Entity.Games}
                      entityId={game.id}
                      apiUrl={apiUrl}
                      size="xsmall"
                    />
                  )}
                </td>
                <td className="pl-2 p-1 border-r border-slate-800">
                  <Link to={`/games/${game.id}`}>{game.title}</Link>
                </td>
                <td className="hidden md:table-cell pl-2 p-1 border-r border-slate-800">{game.platform}</td>
                <td className="hidden lg:table-cell pl-2 p-1 border-r border-slate-800">{game.format}</td>
                <td className="hidden xl:table-cell pl-2 p-1 border-r border-slate-800">
                  {game.developers?.map((dev) => dev.trimEnd()).join(', ')}
                </td>
                <td className="flex flex-row gap-3 p-1 pt-2">
                  <Link to={`/games/${game.id}/edit`} aria-label="Edit" title="Edit">
                    <Pencil size={18} />
                  </Link>
                  <button
                    onClick={() => setDeleteDialogState({ isOpen: true, game })}
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
        onClose={() => setDeleteDialogState({ isOpen: false, game: null })}
        entityTitle={deleteDialogState.game?.title}
        deleteAction={deleteDialogState.game ? `/games/${deleteDialogState.game.id}/delete` : ''}
      />
    </>
  );
}
