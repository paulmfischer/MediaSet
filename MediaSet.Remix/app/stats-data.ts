import { baseUrl } from "./constants.server";

type Stats = {
  bookStats: BookStats;
  movieStats: MovieStats;
};

type BookStats = {
  total: number;
  totalFormats: number;
  formats: string[];
  uniqueAuthors: number;
  totalPages: number;
};

type MovieStats = {
  total: number;
  totalFormats: number;
  formats: string[];
  totalTvSeries: number;
};

export async function getStats() {
  const response = await fetch(`${baseUrl}/stats`);
  if (!response.ok) {
    throw new Response("Error fetching data", { status: 500 });
  }
  return await response.json() as Stats;
}