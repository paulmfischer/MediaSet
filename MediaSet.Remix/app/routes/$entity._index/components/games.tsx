import { Link } from "@remix-run/react";
import { Pencil, Trash2 } from "lucide-react";
import { useState } from "react";
import DeleteDialog from "~/components/delete-dialog";
import ImageDisplay from "~/components/image-display";
import { GameEntity, Entity } from "~/models";

type GamesProps = {
  games: GameEntity[];
  apiUrl?: string;
};

export default function Games({ games, apiUrl }: GamesProps) {
  const [deleteDialogState, setDeleteDialogState] = useState<{ isOpen: boolean; game: GameEntity | null }>({
    isOpen: false,
    game: null
  });

  return (
    <>
      <table className="text-left w-full">
        <thead className="dark:bg-zinc-700 border-b-2 border-slate-600">
          <tr>
            <th className="hidden md:table-cell pl-2 p-1 border-r border-slate-800 underline">Cover</th>
            <th className="pl-2 p-1 border-r border-slate-800 underline">Title</th>
            <th className="hidden md:table-cell pl-2 p-1 border-r border-slate-800 underline">Platform</th>
            <th className="hidden lg:table-cell pl-2 p-1 border-r border-slate-800 underline">Format</th>
            <th className="hidden xl:table-cell pl-2 p-1 border-r border-slate-800 underline">Developers</th>
            <th className="w-1"></th>
          </tr>
        </thead>
        <tbody>
          {games.map(game => {
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
                <td className="hidden xl:table-cell pl-2 p-1 border-r border-slate-800">{game.developers?.map(dev => dev.trimEnd()).join(', ')}</td>
                <td className="flex flex-row gap-3 p-1 pt-2">
                  <Link to={`/games/${game.id}/edit`} aria-label="Edit" title="Edit"><Pencil size={18} /></Link>
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
            )
          })}
        </tbody>
      </table>
      <DeleteDialog
        isOpen={deleteDialogState.isOpen}
        onClose={() => setDeleteDialogState({ isOpen: false, game: null })}
        entityTitle={deleteDialogState.game?.title}
        deleteAction={deleteDialogState.game ? `/games/${deleteDialogState.game.id}/delete` : ""}
      />
    </>
  )
}
