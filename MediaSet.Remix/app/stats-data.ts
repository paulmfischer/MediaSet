import { baseUrl } from "./constants";

export type Stats = {
  totalBooks: number;
  totalFormats: number;
  uniqueAuthors: number;
  totalPages: number;
};

export async function getStats() {
  const response = await fetch(`${baseUrl}/stats`);
  if (!response.ok) {
    throw new Response("Error fetching data", { status: 500 });
  }
  return await response.json() as Stats;
}