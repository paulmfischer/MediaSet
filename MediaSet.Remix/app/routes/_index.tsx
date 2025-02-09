import { json, type MetaFunction } from "@remix-run/node";
import { useLoaderData } from "@remix-run/react";
import { getStats } from "~/stats-data";

export const meta: MetaFunction = () => {
  return [
    { title: "MediaSet" },
    { name: "description", content: "Welcome to your MediaSet!" },
  ];
};

export const loader = async () => {
  const stats = await getStats();
  return json({ stats });
};

export default function Index() {
  const { stats } = useLoaderData<typeof loader>();
  return (
    <div className="flex gap-12">
      <div className="flex flex-col gap-2 mt-2">
        <div><label>Total number of Books: </label>{stats.bookStats.total}</div>
        <div><label>Total number of Book formats: </label>{stats.bookStats.totalFormats}</div>
        <div><label>Total number of pages: </label>{stats.bookStats.totalPages}</div>
        <div><label>Total number of unique authors: </label>{stats.bookStats.uniqueAuthors}</div>
        <div><label>List of Book formats: </label>{stats.bookStats.formats.join(', ')}</div>
      </div>
      <div className="flex flex-col gap-2 mt-2">
        <div><label>Total number of Movies: </label>{stats.movieStats.total}</div>
        <div><label>Total number of Movie formats: </label>{stats.movieStats.totalFormats}</div>
        <div><label>Total number of TV Series: </label>{stats.movieStats.totalTvSeries}</div>
        <div><label>List of Movie formats: </label>{stats.movieStats.formats.join(', ')}</div>
      </div>
    </div>
  );
}
