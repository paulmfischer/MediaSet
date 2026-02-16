import { Link } from '@remix-run/react';
import { Pencil, Trash2 } from 'lucide-react';
import { useState } from 'react';
import DeleteDialog from '~/components/delete-dialog';
import ImageDisplay from '~/components/image-display';
import { MusicEntity, Entity } from '~/models';

type MusicProps = {
  music: MusicEntity;
  apiUrl?: string;
};

export default function Music({ music, apiUrl }: MusicProps) {
  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);

  return (
    <div className="flex flex-col">
      <div className="flex flex-row items-center justify-between border-b-2 border-slate-600 pb-2">
        <div className="flex flex-col gap-2 items-baseline">
          <h2 className="text-2xl">{music.title}</h2>
        </div>
        <div className="flex flex-row gap-2">
          <Link to={`/musics/${music.id}/edit`} aria-label="Edit" title="Edit">
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
              imageData={music.coverImage}
              alt={music.title ?? 'Album cover'}
              entityType={Entity.Musics}
              entityId={music.id}
              apiUrl={apiUrl}
            />
          </div>
          <div className="lg:basis-4/5">
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="artist" className="basis-2/12 dark:text-slate-400">
                Artist
              </label>
              <div id="artist" className="grow">
                {music.artist}
              </div>
            </div>
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="format" className="basis-2/12 dark:text-slate-400">
                Format
              </label>
              <div id="format" className="grow">
                {music.format}
              </div>
            </div>
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="releaseDate" className="basis-2/12 dark:text-slate-400">
                Release Date
              </label>
              <div id="releaseDate" className="grow">
                {music.releaseDate}
              </div>
            </div>
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="genres" className="basis-2/12 dark:text-slate-400">
                Genres
              </label>
              <div id="genres" className="grow">
                {music.genres?.join(', ')}
              </div>
            </div>
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="duration" className="basis-2/12 dark:text-slate-400">
                Duration
              </label>
              <div id="duration" className="grow">
                {music.duration} minutes
              </div>
            </div>
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="label" className="basis-2/12 dark:text-slate-400">
                Label
              </label>
              <div id="label" className="grow">
                {music.label}
              </div>
            </div>
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="barcode" className="basis-2/12 dark:text-slate-400">
                Barcode
              </label>
              <div id="barcode" className="grow">
                {music.barcode}
              </div>
            </div>
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="tracks" className="basis-2/12 dark:text-slate-400">
                Tracks
              </label>
              <div id="tracks" className="grow">
                {music.tracks}
              </div>
            </div>
            <div className="flex flex-col md:flex-row mb-2 md:mb-0">
              <label htmlFor="discs" className="basis-2/12 dark:text-slate-400">
                Discs
              </label>
              <div id="discs" className="grow">
                {music.discs}
              </div>
            </div>
            {music.discList && music.discList.length > 0 && (
              <div className="flex flex-col md:flex-row mb-2 md:mb-0 mt-4">
                <span className="basis-2/12 dark:text-slate-400">Disc List</span>
                <div className="grow">
                  <table className="w-full text-sm">
                    <thead className="border-b border-gray-600">
                      <tr>
                        <th className="text-left py-1 px-2">Track #</th>
                        <th className="text-left py-1 px-2">Title</th>
                        <th className="text-left py-1 px-2">Duration</th>
                      </tr>
                    </thead>
                    <tbody>
                      {music.discList.map((disc, index) => (
                        <tr key={index} className="border-b border-gray-700">
                          <td className="py-1 px-2">{disc.trackNumber}</td>
                          <td className="py-1 px-2">{disc.title}</td>
                          <td className="py-1 px-2">{disc.duration}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
      <DeleteDialog
        isOpen={isDeleteDialogOpen}
        onClose={() => setIsDeleteDialogOpen(false)}
        entityTitle={music.title}
        deleteAction={`/musics/${music.id}/delete`}
      />
    </div>
  );
}
