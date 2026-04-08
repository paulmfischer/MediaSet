import { baseUrl } from '~/constants.server';
import { serverLogger } from '~/utils/serverLogger';
import { apiFetch } from '~/utils/apiFetch.server';

export type NameCount = { name: string; count: number };

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
  avgPages: number;
  formatBreakdown: Record<string, number>;
  decadeBreakdown: Record<string, number>;
  pageCountBuckets: Record<string, number>;
  topAuthors: NameCount[];
  topGenres: NameCount[];
};

type MovieStats = {
  total: number;
  totalFormats: number;
  formats: string[];
  totalTvSeries: number;
  formatBreakdown: Record<string, number>;
  decadeBreakdown: Record<string, number>;
  genreBreakdown: Record<string, number>;
  topStudios: NameCount[];
};

type GameStats = {
  total: number;
  totalFormats: number;
  formats: string[];
  totalPlatforms: number;
  platforms: string[];
  formatBreakdown: Record<string, number>;
  platformBreakdown: Record<string, number>;
  decadeBreakdown: Record<string, number>;
  genreBreakdown: Record<string, number>;
  topPublishers: NameCount[];
  topDevelopers: NameCount[];
};

type MusicStats = {
  total: number;
  totalFormats: number;
  formats: string[];
  uniqueArtists: number;
  totalTracks: number;
  avgTracks: number;
  uniqueLabels: number;
  totalDiscs: number;
  formatBreakdown: Record<string, number>;
  decadeBreakdown: Record<string, number>;
  genreBreakdown: Record<string, number>;
  topArtists: NameCount[];
  topLabels: NameCount[];
};

export async function getStats() {
  serverLogger.info('Fetching stats for all entities', {});
  try {
    const response = await apiFetch(`${baseUrl}/stats`);
    if (!response.ok) {
      serverLogger.error('Failed to fetch stats', { status: response.status });
      throw new Response('Error fetching data', { status: 500 });
    }
    const stats = (await response.json()) as Stats;
    serverLogger.info(
      `Successfully fetched stats: ${stats.bookStats.total} books, ${stats.movieStats.total} movies, ${stats.gameStats.total} games, ${stats.musicStats.total} music items`,
      {
        totalBooks: stats.bookStats.total,
        totalMovies: stats.movieStats.total,
        totalGames: stats.gameStats.total,
        totalMusic: stats.musicStats.total,
      }
    );
    return stats;
  } catch (error) {
    serverLogger.error('Error fetching stats', { error: String(error) });
    throw error;
  }
}
