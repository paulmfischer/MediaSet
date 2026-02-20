import { useCallback, useEffect, useRef } from 'react';
import { X } from 'lucide-react';
import { BookEntity, Entity, GameEntity, MovieEntity, MusicEntity } from '~/models';

type TitleLookupEntity = BookEntity | MovieEntity | GameEntity | MusicEntity;

type TitleLookupResultsDialogProps = {
  isOpen: boolean;
  results: TitleLookupEntity[];
  onSelect: (entity: TitleLookupEntity) => void;
  onClose: () => void;
};

function getResultDetails(entity: TitleLookupEntity): string {
  const parts: string[] = [];

  if (entity.type === Entity.Books) {
    const book = entity as BookEntity;
    if (book.authors?.length) parts.push(book.authors.join(', '));
    if (book.publicationDate) parts.push(book.publicationDate);
  } else if (entity.type === Entity.Movies) {
    const movie = entity as MovieEntity;
    if (movie.releaseDate) parts.push(movie.releaseDate);
    if (movie.genres?.length) parts.push(movie.genres.slice(0, 3).join(', '));
  } else if (entity.type === Entity.Games) {
    const game = entity as GameEntity;
    if (game.platform) parts.push(game.platform);
    if (game.releaseDate) parts.push(game.releaseDate);
  } else if (entity.type === Entity.Musics) {
    const music = entity as MusicEntity;
    if (music.artist) parts.push(music.artist);
    if (music.releaseDate) parts.push(music.releaseDate);
  }

  return parts.join(' · ');
}

export default function TitleLookupResultsDialog({
  isOpen,
  results,
  onSelect,
  onClose,
}: TitleLookupResultsDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);

  const handleClose = useCallback(() => {
    onClose();
  }, [onClose]);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;

    if (isOpen) {
      dialog.showModal();
    } else {
      dialog.close();
    }
  }, [isOpen]);

  const handleBackdropClick = (event: React.MouseEvent<HTMLDialogElement>) => {
    const dialog = dialogRef.current;
    if (!dialog) return;

    const rect = dialog.getBoundingClientRect();
    const isInDialog =
      rect.top <= event.clientY &&
      event.clientY <= rect.top + rect.height &&
      rect.left <= event.clientX &&
      event.clientX <= rect.left + rect.width;

    if (!isInDialog) handleClose();
  };

  const handleSelect = (entity: TitleLookupEntity) => {
    onSelect(entity);
    handleClose();
  };

  return (
    // eslint-disable-next-line jsx-a11y/no-noninteractive-element-interactions
    <dialog
      ref={dialogRef}
      onClick={handleBackdropClick}
      onKeyDown={(e) => {
        if (e.key === 'Escape') handleClose();
      }}
      className="backdrop:bg-gray-900 backdrop:bg-opacity-60 bg-zinc-800 text-slate-200 rounded-lg shadow-xl p-0 w-full max-w-lg"
    >
      <div className="flex flex-col max-h-[80vh]">
        <div className="flex items-center justify-between border-b border-slate-600 p-4">
          <h2 className="text-xl font-semibold">Select a Result</h2>
          <button onClick={handleClose} className="secondary" aria-label="Close dialog">
            <X size={24} />
          </button>
        </div>
        <div className="overflow-y-auto p-4">
          <p className="text-slate-400 text-sm mb-3">
            {results.length} results found. Select one to populate the form.
          </p>
          <ul className="flex flex-col gap-2">
            {results.map((entity, index) => {
              const details = getResultDetails(entity);
              return (
                <li key={index}>
                  <button
                    type="button"
                    onClick={() => handleSelect(entity)}
                    className="w-full text-left p-3 rounded-md bg-gray-700 hover:bg-gray-600 border border-gray-600 hover:border-blue-400 transition-colors"
                  >
                    <div className="font-medium text-white">{entity.title ?? '(No title)'}</div>
                    {details && <div className="text-sm text-gray-400 mt-1">{details}</div>}
                  </button>
                </li>
              );
            })}
          </ul>
        </div>
      </div>
    </dialog>
  );
}
