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

function getResultDetails(entity: TitleLookupEntity): string[] {
  if (entity.type === Entity.Books) {
    const book = entity as BookEntity;
    return [
      book.authors?.length ? book.authors.join(', ') : null,
      book.publicationDate ?? null,
    ].filter((v): v is string => v !== null);
  } else if (entity.type === Entity.Movies) {
    const movie = entity as MovieEntity;
    return [
      movie.releaseDate ?? null,
      movie.genres?.length ? movie.genres.slice(0, 3).join(', ') : null,
    ].filter((v): v is string => v !== null);
  } else if (entity.type === Entity.Games) {
    const game = entity as GameEntity;
    return [
      game.platform ?? null,
      game.releaseDate ?? null,
    ].filter((v): v is string => v !== null);
  } else if (entity.type === Entity.Musics) {
    const music = entity as MusicEntity;
    return [
      music.artist ?? null,
      music.releaseDate ?? null,
    ].filter((v): v is string => v !== null);
  }
  return [];
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
            {results.length} results found. Pick the one you&apos;re looking for.
          </p>
          <ul className="flex flex-col gap-2">
            {results.map((entity, index) => {
              const details = getResultDetails(entity);
              return (
                <li
                  key={index}
                  className="flex items-center gap-3 p-3 rounded-md bg-gray-700 border border-gray-600"
                >
                  <button
                    type="button"
                    onClick={() => handleSelect(entity)}
                    className="shrink-0 px-3 py-1 text-sm font-medium rounded bg-blue-600 hover:bg-blue-500 text-white transition-colors"
                  >
                    Select
                  </button>
                  <div className="shrink-0 w-10 h-14 bg-gray-600 rounded overflow-hidden flex items-center justify-center">
                    {entity.imageUrl ? (
                      <img
                        src={entity.imageUrl}
                        alt=""
                        className="w-full h-full object-cover"
                      />
                    ) : null}
                  </div>
                  <div>
                    <div className="font-medium text-white">{entity.title ?? '(No title)'}</div>
                    {details.map((line, i) => (
                      <div key={i} className="text-sm text-gray-400 mt-0.5">{line}</div>
                    ))}
                  </div>
                </li>
              );
            })}
          </ul>
        </div>
      </div>
    </dialog>
  );
}
