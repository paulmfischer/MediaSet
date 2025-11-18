import { Link } from "@remix-run/react";
import { Pencil, Trash2 } from "lucide-react";
import { useState } from "react";
import DeleteDialog from "~/components/delete-dialog";
import ImageDisplay from "~/components/image-display";
import { GameEntity, Entity } from "~/models";

type GameProps = {
  game: GameEntity;
};

export default function Game({ game }: GameProps) {
  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);

  return (
    <div className="flex flex-col">
      <div className="flex flex-row items-center justify-between border-b-2 border-slate-600 pb-2">
        <div className="flex flex-col gap-2 items-baseline">
          <h2 className="text-2xl">{game.title}</h2>
        </div>
        <div className="flex flex-row gap-2">
          <Link to={`/games/${game.id}/edit`} aria-label="Edit" title="Edit">
            <Pencil size={22} />
          </Link>
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
        <div className="flex flex-col lg:flex-row lg:gap-3">
          <div className="lg:basis-1/5 flex justify-center mb-4 lg:mb-0">
            <ImageDisplay
              imageData={game.coverImage}
              alt={game.title ?? "Game cover art"}
              entityType={Entity.Games}
              entityId={game.id}
            />
          </div>
          <div className="lg:basis-4/5">
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="platform" className="basis-2/12 dark:text-slate-400">
                Platform
              </label>
              <div id="platform" className="grow">
                {game.platform}
              </div>
            </div>
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="format" className="basis-2/12 dark:text-slate-400">
                Format
              </label>
              <div id="format" className="grow">
                {game.format}
              </div>
            </div>
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="releaseDate" className="basis-2/12 dark:text-slate-400">
                Release Date
              </label>
              <div id="releaseDate" className="grow">
                {game.releaseDate}
              </div>
            </div>
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="rating" className="basis-2/12 dark:text-slate-400">
                Rating
              </label>
              <div id="rating" className="grow">
                {game.rating}
              </div>
            </div>
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="developers" className="basis-2/12 dark:text-slate-400">
                Developers
              </label>
              <div id="developers" className="grow">
                {game.developers?.join(", ")}
              </div>
            </div>
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="publishers" className="basis-2/12 dark:text-slate-400">
                Publishers
              </label>
              <div id="publishers" className="grow">
                {game.publishers?.join(", ")}
              </div>
            </div>
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="genres" className="basis-2/12 dark:text-slate-400">
                Genres
              </label>
              <div id="genres" className="grow">
                {game.genres?.join(", ")}
              </div>
            </div>
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="barcode" className="basis-2/12 dark:text-slate-400">
                Barcode
              </label>
              <div id="barcode" className="grow">
                {game.barcode}
              </div>
            </div>
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="description" className="basis-2/12 dark:text-slate-400">
                Description
              </label>
              <div id="description" className="basis-3/4">
                {game.description}
              </div>
            </div>
          </div>
        </div>
      </div>
      <DeleteDialog
        isOpen={isDeleteDialogOpen}
        onClose={() => setIsDeleteDialogOpen(false)}
        entityTitle={game.title}
        deleteAction={`/games/${game.id}/delete`}
      />
    </div>
  );
}
