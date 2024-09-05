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
    <div>
      Some information about your MediaSet!
      <div className="flex flex-col gap-2 mt-2">
        <div><label>Total Number of books: </label>{stats.totalBooks}</div>
        <div><label>Total Number of pages: </label>{stats.totalPages}</div>
        <div><label>Total Number of formats: </label>{stats.totalFormats}</div>
        <div><label>Total Number of unique authors: </label>{stats.uniqueAuthors}</div>
      </div>
    </div>
  );
}
