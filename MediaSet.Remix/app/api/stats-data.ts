import { baseUrl } from '../constants.server.ts';
import { createServerLogger } from '../utils/serverLogger.ts';
import { apiFetch } from '../utils/apiFetch.server.ts';

const serverLogger = createServerLogger('app/api/stats-data');

export type NameCount = { name: string; count: number };

/** Plex counts for the movie collection; the API sends them only when Plex is enabled for movies. */
export type PlexStats = {
  onPlex: number;
  notOnPlex: number;
  watched: number;
  unwatched: number;
  /** On-Plex movies by resolution; those without one are counted as "unknown". */
  resolutionBreakdown: Record<string, number>;
  needsLinking: number;
};

type Stats = {
  bookStats: BookStats;
  movieStats: MovieStats;
  gameStats: GameStats;
  musicStats: MusicStats;
  plexStats?: PlexStats | null;
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
      'Fetched stats: {totalBooks} books, {totalMovies} movies, {totalGames} games, {totalMusic} music items',
      {
        totalBooks: stats.bookStats.total,
        totalMovies: stats.movieStats.total,
        totalGames: stats.gameStats.total,
        totalMusic: stats.musicStats.total,
      }
    );
    return stats;
  } catch (error) {
    serverLogger.error('Error fetching stats', error);
    throw error;
  }
}
