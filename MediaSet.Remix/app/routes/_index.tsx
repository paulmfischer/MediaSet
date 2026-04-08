import { json, type MetaFunction } from '@remix-run/node';
import { useLoaderData } from '@remix-run/react';
import { getStats, type NameCount } from '~/api/stats-data';
import { getIntegrations } from '~/api/integrations-data';
import AttributionBadges from '~/components/attribution-badges';
import Tabs, { type TabConfig } from '~/components/tabs';
import BarChart from '~/components/charts/bar-chart';
import ChartCard from '~/components/charts/chart-card';
import PieChart from '~/components/charts/pie-chart';
import StatCard from '~/components/stat-card';
import {
  LibraryBig,
  Clapperboard,
  Gamepad2,
  Music,
  BookOpen,
  FileText,
  Users,
  Disc,
  Tv,
  Monitor,
  Album,
  Tag,
  Layers,
  Hash,
} from 'lucide-react';

export const meta: MetaFunction = () => {
  return [
    { title: 'Dashboard | MediaSet' },
    { name: 'description', content: 'Your personal media collection dashboard' },
  ];
};

export const loader = async () => {
  const [stats, integrations] = await Promise.all([getStats(), getIntegrations()]);
  return json({ stats, integrations });
};

const ACCENT = {
  books: '#22c55e',
  movies: '#ef4444',
  games: '#a855f7',
  music: '#ec4899',
};

const FORMAT_PALETTES = {
  books: ['#16a34a', '#22c55e', '#4ade80', '#86efac', '#bbf7d0'],
  movies: ['#dc2626', '#ef4444', '#f87171', '#fca5a5', '#fecaca'],
  games: ['#9333ea', '#a855f7', '#c084fc', '#d8b4fe', '#e9d5ff'],
  music: ['#db2777', '#ec4899', '#f472b6', '#f9a8d4', '#fbcfe8'],
};

const MOVIE_TYPE_COLORS = ['#ef4444', '#f97316'];

function breakdownToChartData(breakdown: Record<string, number> | undefined) {
  if (!breakdown) return [];
  return Object.entries(breakdown).map(([name, value]) => ({ name, value }));
}

function nameCountToBarData(items: NameCount[] | undefined) {
  if (!items) return [];
  return items.map(({ name, count }) => ({ name, value: count }));
}

function pct(value: number, total: number) {
  return ((value / total) * 100).toFixed(1);
}

export default function Index() {
  const { stats, integrations } = useLoaderData<typeof loader>();

  const totalItems = stats.bookStats.total + stats.movieStats.total + stats.gameStats.total + stats.musicStats.total;
  const isEmpty = totalItems === 0;

  const bookFormatData = breakdownToChartData(stats.bookStats.formatBreakdown);
  const bookDecadeData = breakdownToChartData(stats.bookStats.decadeBreakdown);
  const bookPageBucketData = breakdownToChartData(stats.bookStats.pageCountBuckets);
  const bookTopAuthors = nameCountToBarData(stats.bookStats.topAuthors);
  const bookTopGenres = nameCountToBarData(stats.bookStats.topGenres);

  const movieFormatData = breakdownToChartData(stats.movieStats.formatBreakdown);
  const movieDecadeData = breakdownToChartData(stats.movieStats.decadeBreakdown);
  const movieGenreData = breakdownToChartData(stats.movieStats.genreBreakdown);
  const movieTopStudios = nameCountToBarData(stats.movieStats.topStudios);
  const movieTypeData = [
    { name: 'Movies', value: stats.movieStats.total - stats.movieStats.totalTvSeries },
    { name: 'TV Series', value: stats.movieStats.totalTvSeries },
  ].filter((d) => d.value > 0);

  const gameFormatData = breakdownToChartData(stats.gameStats.formatBreakdown);
  const gamePlatformData = breakdownToChartData(stats.gameStats.platformBreakdown);
  const gameDecadeData = breakdownToChartData(stats.gameStats.decadeBreakdown);
  const gameGenreData = breakdownToChartData(stats.gameStats.genreBreakdown);
  const gameTopPublishers = nameCountToBarData(stats.gameStats.topPublishers);
  const gameTopDevelopers = nameCountToBarData(stats.gameStats.topDevelopers);

  const musicFormatData = breakdownToChartData(stats.musicStats.formatBreakdown);
  const musicDecadeData = breakdownToChartData(stats.musicStats.decadeBreakdown);
  const musicGenreData = breakdownToChartData(stats.musicStats.genreBreakdown);
  const musicTopArtists = nameCountToBarData(stats.musicStats.topArtists);
  const musicTopLabels = nameCountToBarData(stats.musicStats.topLabels);

  const tabs: TabConfig[] = [
    stats.bookStats.total > 0 && {
      id: 'books',
      activeTopBorderClass: '!border-t-green-500',
      label: (
        <div>
          <div className="flex items-center gap-2">
            <LibraryBig className="h-4 w-4 text-green-400" />
            <span className="font-semibold text-white">Books</span>
          </div>
          <div className="mt-2 text-2xl font-bold text-green-400">{stats.bookStats.total.toLocaleString()}</div>
          <div className="text-xs text-zinc-400">{pct(stats.bookStats.total, totalItems)}% of collection</div>
        </div>
      ),
      panel: (
        <div className="space-y-4 p-6">
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <StatCard
              title="Total Pages"
              value={stats.bookStats.totalPages.toLocaleString()}
              icon={BookOpen}
              colorClass="bg-green-500/10 text-green-400 border-green-500/20"
            />
            <StatCard
              title="Avg Pages"
              value={stats.bookStats.avgPages.toLocaleString()}
              icon={Hash}
              colorClass="bg-green-500/10 text-green-400 border-green-500/20"
            />
            <StatCard
              title="Unique Authors"
              value={stats.bookStats.uniqueAuthors}
              icon={Users}
              colorClass="bg-green-500/10 text-green-400 border-green-500/20"
            />
            <StatCard
              title="Formats"
              value={stats.bookStats.totalFormats}
              icon={FileText}
              colorClass="bg-green-500/10 text-green-400 border-green-500/20"
            />
          </div>
          <div className="grid grid-cols-2 gap-4">
            {bookFormatData.length > 1 && (
              <ChartCard title="Format Breakdown">
                <PieChart data={bookFormatData} colors={FORMAT_PALETTES.books} size={140} compact />
              </ChartCard>
            )}
            {bookPageBucketData.length > 0 && (
              <ChartCard title="Page Count Distribution">
                <BarChart data={bookPageBucketData} color={ACCENT.books} orientation="vertical" />
              </ChartCard>
            )}
            {bookTopAuthors.length > 0 && (
              <ChartCard title="Top Authors">
                <BarChart data={bookTopAuthors} color={ACCENT.books} orientation="horizontal" />
              </ChartCard>
            )}
            {bookDecadeData.length > 0 && (
              <ChartCard title="Publication Decade">
                <BarChart data={bookDecadeData} color={ACCENT.books} orientation="vertical" />
              </ChartCard>
            )}
            {bookTopGenres.length > 0 && (
              <ChartCard title="Top Genres">
                <BarChart data={bookTopGenres} color={ACCENT.books} orientation="horizontal" />
              </ChartCard>
            )}
          </div>
        </div>
      ),
    },
    stats.movieStats.total > 0 && {
      id: 'movies',
      activeTopBorderClass: '!border-t-red-500',
      label: (
        <div>
          <div className="flex items-center gap-2">
            <Clapperboard className="h-4 w-4 text-red-400" />
            <span className="font-semibold text-white">Movies & TV</span>
          </div>
          <div className="mt-2 text-2xl font-bold text-red-400">{stats.movieStats.total.toLocaleString()}</div>
          <div className="text-xs text-zinc-400">{pct(stats.movieStats.total, totalItems)}% of collection</div>
        </div>
      ),
      panel: (
        <div className="space-y-4 p-6">
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
            <StatCard
              title="Movies"
              value={stats.movieStats.total - stats.movieStats.totalTvSeries}
              icon={Clapperboard}
              colorClass="bg-red-500/10 text-red-400 border-red-500/20"
            />
            <StatCard
              title="TV Series"
              value={stats.movieStats.totalTvSeries}
              icon={Tv}
              colorClass="bg-red-500/10 text-red-400 border-red-500/20"
            />
            <StatCard
              title="Formats"
              value={stats.movieStats.totalFormats}
              icon={Disc}
              colorClass="bg-red-500/10 text-red-400 border-red-500/20"
            />
          </div>
          <div className="grid grid-cols-2 gap-4">
            {movieTypeData.length > 1 && (
              <ChartCard title="Movies vs TV Series">
                <PieChart data={movieTypeData} colors={MOVIE_TYPE_COLORS} size={140} compact />
              </ChartCard>
            )}
            {movieFormatData.length > 1 && (
              <ChartCard title="Format Breakdown">
                <PieChart data={movieFormatData} colors={FORMAT_PALETTES.movies} size={140} compact />
              </ChartCard>
            )}
            {movieTopStudios.length > 0 && (
              <ChartCard title="Top Studios">
                <BarChart data={movieTopStudios} color={ACCENT.movies} orientation="horizontal" />
              </ChartCard>
            )}
            {movieDecadeData.length > 0 && (
              <ChartCard title="Release Decade">
                <BarChart data={movieDecadeData} color={ACCENT.movies} orientation="vertical" />
              </ChartCard>
            )}
            {movieGenreData.length > 0 && (
              <ChartCard title="Top Genres">
                <BarChart data={movieGenreData} color={ACCENT.movies} orientation="horizontal" />
              </ChartCard>
            )}
          </div>
        </div>
      ),
    },
    stats.gameStats.total > 0 && {
      id: 'games',
      activeTopBorderClass: '!border-t-purple-500',
      label: (
        <div>
          <div className="flex items-center gap-2">
            <Gamepad2 className="h-4 w-4 text-purple-400" />
            <span className="font-semibold text-white">Games</span>
          </div>
          <div className="mt-2 text-2xl font-bold text-purple-400">{stats.gameStats.total.toLocaleString()}</div>
          <div className="text-xs text-zinc-400">{pct(stats.gameStats.total, totalItems)}% of collection</div>
        </div>
      ),
      panel: (
        <div className="space-y-4 p-6">
          <div className="grid grid-cols-2 gap-3">
            <StatCard
              title="Platforms"
              value={stats.gameStats.totalPlatforms}
              icon={Monitor}
              colorClass="bg-purple-500/10 text-purple-400 border-purple-500/20"
            />
            <StatCard
              title="Formats"
              value={stats.gameStats.totalFormats}
              icon={Disc}
              colorClass="bg-purple-500/10 text-purple-400 border-purple-500/20"
            />
          </div>
          <div className="grid grid-cols-2 gap-4">
            {gamePlatformData.length > 1 && (
              <ChartCard title="Platform Breakdown">
                <PieChart data={gamePlatformData} colors={FORMAT_PALETTES.games} size={140} compact />
              </ChartCard>
            )}
            {gameFormatData.length > 1 && (
              <ChartCard title="Format Breakdown">
                <PieChart data={gameFormatData} colors={FORMAT_PALETTES.games} size={140} compact />
              </ChartCard>
            )}
            {gameTopPublishers.length > 0 && (
              <ChartCard title="Top Publishers">
                <BarChart data={gameTopPublishers} color={ACCENT.games} orientation="horizontal" />
              </ChartCard>
            )}
            {gameTopDevelopers.length > 0 && (
              <ChartCard title="Top Developers">
                <BarChart data={gameTopDevelopers} color={ACCENT.games} orientation="horizontal" />
              </ChartCard>
            )}
            {gameDecadeData.length > 0 && (
              <ChartCard title="Release Decade">
                <BarChart data={gameDecadeData} color={ACCENT.games} orientation="vertical" />
              </ChartCard>
            )}
            {gameGenreData.length > 0 && (
              <ChartCard title="Top Genres">
                <BarChart data={gameGenreData} color={ACCENT.games} orientation="horizontal" />
              </ChartCard>
            )}
          </div>
        </div>
      ),
    },
    stats.musicStats.total > 0 && {
      id: 'music',
      activeTopBorderClass: '!border-t-pink-500',
      label: (
        <div>
          <div className="flex items-center gap-2">
            <Music className="h-4 w-4 text-pink-400" />
            <span className="font-semibold text-white">Music</span>
          </div>
          <div className="mt-2 text-2xl font-bold text-pink-400">{stats.musicStats.total.toLocaleString()}</div>
          <div className="text-xs text-zinc-400">{pct(stats.musicStats.total, totalItems)}% of collection</div>
        </div>
      ),
      panel: (
        <div className="space-y-4 p-6">
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
            <StatCard
              title="Total Tracks"
              value={stats.musicStats.totalTracks.toLocaleString()}
              icon={Album}
              colorClass="bg-pink-500/10 text-pink-400 border-pink-500/20"
            />
            <StatCard
              title="Avg Tracks"
              value={stats.musicStats.avgTracks}
              icon={Hash}
              colorClass="bg-pink-500/10 text-pink-400 border-pink-500/20"
            />
            <StatCard
              title="Unique Artists"
              value={stats.musicStats.uniqueArtists}
              icon={Users}
              colorClass="bg-pink-500/10 text-pink-400 border-pink-500/20"
            />
            <StatCard
              title="Unique Labels"
              value={stats.musicStats.uniqueLabels}
              icon={Tag}
              colorClass="bg-pink-500/10 text-pink-400 border-pink-500/20"
            />
            <StatCard
              title="Total Discs"
              value={stats.musicStats.totalDiscs}
              icon={Layers}
              colorClass="bg-pink-500/10 text-pink-400 border-pink-500/20"
            />
            <StatCard
              title="Formats"
              value={stats.musicStats.totalFormats}
              icon={Disc}
              colorClass="bg-pink-500/10 text-pink-400 border-pink-500/20"
            />
          </div>
          <div className="grid grid-cols-2 gap-4">
            {musicFormatData.length > 1 && (
              <ChartCard title="Format Breakdown">
                <PieChart data={musicFormatData} colors={FORMAT_PALETTES.music} size={140} compact />
              </ChartCard>
            )}
            {musicTopArtists.length > 0 && (
              <ChartCard title="Top Artists">
                <BarChart data={musicTopArtists} color={ACCENT.music} orientation="horizontal" />
              </ChartCard>
            )}
            {musicTopLabels.length > 0 && (
              <ChartCard title="Top Labels">
                <BarChart data={musicTopLabels} color={ACCENT.music} orientation="horizontal" />
              </ChartCard>
            )}
            {musicDecadeData.length > 0 && (
              <ChartCard title="Release Decade">
                <BarChart data={musicDecadeData} color={ACCENT.music} orientation="vertical" />
              </ChartCard>
            )}
            {musicGenreData.length > 0 && (
              <ChartCard title="Top Genres">
                <BarChart data={musicGenreData} color={ACCENT.music} orientation="horizontal" />
              </ChartCard>
            )}
          </div>
        </div>
      ),
    },
  ].filter(Boolean) as TabConfig[];

  return (
    <div className="space-y-8">
      {isEmpty ? (
        <div className="rounded-lg border border-zinc-700 bg-zinc-800/30 p-12 text-center">
          <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-zinc-700/50">
            <LibraryBig className="h-8 w-8 text-zinc-400" />
          </div>
          <h2 className="mt-4 text-xl font-semibold text-white">No media items yet</h2>
          <p className="mt-2 text-zinc-400">Start building your collection by adding books, movies, games, or music.</p>
        </div>
      ) : (
        <div>
          <div className="mb-6">
            <h2 className="text-xl font-semibold text-white">
              Collection Overview
              <span className="ml-3 rounded-lg bg-blue-500/10 px-3 py-1 text-base font-normal text-blue-400">
                {totalItems.toLocaleString()} items
              </span>
            </h2>
          </div>
          <Tabs tabs={tabs} defaultTabId="books" />
        </div>
      )}

      <AttributionBadges integrations={integrations} />
    </div>
  );
}
