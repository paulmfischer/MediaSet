import { baseUrl } from "./constants.server";
import { serverLogger } from "./utils/serverLogger";
import { apiFetch } from "./utils/apiFetch.server";

type Stats = {
  bookStats: BookStats;
  movieStats: MovieStats;
  gameStats: GameStats;
  musicStats: MusicStats;
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

type GameStats = {
  total: number;
  totalFormats: number;
  formats: string[];
  totalPlatforms: number;
  platforms: string[];
};

type MusicStats = {
  total: number;
  totalFormats: number;
  formats: string[];
  uniqueArtists: number;
  totalTracks: number;
};

export async function getStats() {
  serverLogger.info("Fetching stats", {});
  try {
    const response = await apiFetch(`${baseUrl}/stats`);
    if (!response.ok) {
      serverLogger.error("Failed to fetch stats", { status: response.status });
      throw new Response("Error fetching data", { status: 500 });
    }
    const stats = await response.json() as Stats;
    serverLogger.info("Successfully fetched stats", {
      totalBooks: stats.bookStats.total,
      totalMovies: stats.movieStats.total,
      totalGames: stats.gameStats.total,
      totalMusic: stats.musicStats.total
    });
    return stats;
  } catch (error) {
    serverLogger.error("Error fetching stats", { error: String(error) });
    throw error;
  }
}