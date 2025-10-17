import { Form, Link } from "@remix-run/react";
import { Pencil, Trash2 } from "lucide-react";
import { GameEntity } from "~/models";

type GamesProps = {
  games: GameEntity[]
};

export default function Games({ games }: GamesProps) {
  return (
    <table className="text-left w-full">
      <thead className="dark:bg-zinc-700 border-b-2 border-slate-600">
        <tr>
          <th className="pl-2 p-1 border-r border-slate-800 underline">Title</th>
          <th className="hidden sm:table-cell pl-2 p-1 border-r border-slate-800 underline">Platform</th>
          <th className="hidden sm:table-cell pl-2 p-1 border-r border-slate-800 underline">Format</th>
          <th className="hidden sm:table-cell pl-2 p-1 border-r border-slate-800 underline">Developers</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        {games.map(game => {
          return (
            <tr className="border-b border-slate-700 dark:hover:bg-zinc-800" key={game.id}>
              <td className="pl-2 p-1 border-r border-slate-800">
                <Link to={`/games/${game.id}`}>{game.title}</Link>
              </td>
              <td className="hidden sm:table-cell pl-2 p-1 border-r border-slate-800">{game.platform}</td>
              <td className="hidden sm:table-cell pl-2 p-1 border-r border-slate-800">{game.format}</td>
              <td className="hidden sm:table-cell pl-2 p-1 border-r border-slate-800">{game.developers?.map(dev => dev.trimEnd()).join(', ')}</td>
              <td className="flex flex-row gap-3 p-1 pt-2">
                <Link to={`/games/${game.id}/edit`} aria-label="Edit" title="Edit"><Pencil size={18} /></Link>
                <Form action={`/games/${game.id}/delete`} method="post" onSubmit={(event) => {
                  const response = confirm(`Are you sure you want to delete ${game.title}?`);
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
