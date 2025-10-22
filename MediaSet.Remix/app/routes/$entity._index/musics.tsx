import { Form, Link } from "@remix-run/react";
import { Pencil, Trash2 } from "lucide-react";
import { MusicEntity } from "~/models";

type MusicsProps = {
  musics: MusicEntity[]
};

export default function Musics({ musics }: MusicsProps) {
  return (
    <table className="text-left w-full">
      <thead className="dark:bg-zinc-700 border-b-2 border-slate-600">
        <tr>
          <th className="pl-2 p-1 border-r border-slate-800 underline">Title</th>
          <th className="pl-2 p-1 border-r border-slate-800 underline">Artist</th>
          <th className="hidden sm:table-cell pl-2 p-1 border-r border-slate-800 underline">Format</th>
          <th className="hidden sm:table-cell pl-2 p-1 border-r border-slate-800 underline">Tracks</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        {musics.map(music => {
          return (
            <tr className="border-b border-slate-700 dark:hover:bg-zinc-800" key={music.id}>
              <td className="pl-2 p-1 border-r border-slate-800">
                <Link to={`/musics/${music.id}`}>{music.title}</Link>
              </td>
              <td className="pl-2 p-1 border-r border-slate-800">{music.artist}</td>
              <td className="hidden sm:table-cell pl-2 p-1 border-r border-slate-800">{music.format}</td>
              <td className="hidden sm:table-cell pl-2 p-1 border-r border-slate-800">{music.tracks}</td>
              <td className="flex flex-row gap-3 p-1 pt-2">
                <Link to={`/musics/${music.id}/edit`} aria-label="Edit" title="Edit"><Pencil size={18} /></Link>
                <Form action={`/musics/${music.id}/delete`} method="post" onSubmit={(event) => {
                  const response = confirm(`Are you sure you want to delete ${music.title}?`);
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
