import { type MetaFunction } from '@remix-run/node';
import { useLoaderData } from '@remix-run/react';
import { getStats, type NameCount } from '~/api/stats-data';
import { getIntegrations } from '~/api/integrations-data';
import AttributionBadges from '~/components/common/attribution-badges';
import Tabs, { type TabConfig } from '~/components/common/tabs';
import BarChart from '~/components/charts/bar-chart';
import ChartCard from '~/components/charts/chart-card';
import PieChart from '~/components/charts/pie-chart';
import StatCard from '~/components/common/stat-card';
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
  return { stats, integrations };
};

const CHART_HEX = {
  books: '#22c55e',
  movies: '#ef4444',
  games: '#a855f7',
  musics: '#ec4899',
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
      className: 'entity-books',
      activeTopBorderClass: '!border-t-entity',
      label: (
        <div>
          <div className="flex items-center gap-2">
            <LibraryBig className="h-4 w-4 text-entity" />
            <span className="font-semibold text-white">Books</span>
          </div>
          <div className="mt-2 text-2xl font-bold text-entity">{stats.bookStats.total.toLocaleString()}</div>
          <div className="text-xs text-zinc-400">{pct(stats.bookStats.total, totalItems)}% of collection</div>
        </div>
      ),
      panel: (
        <div className="entity-books space-y-4 p-6">
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <StatCard
              title="Total Pages"
              value={stats.bookStats.totalPages.toLocaleString()}
              icon={BookOpen}
              colorClass="bg-entity/10 text-entity border-entity/20"
            />
            <StatCard
              title="Avg Pages"
              value={stats.bookStats.avgPages.toLocaleString()}
              icon={Hash}
              colorClass="bg-entity/10 text-entity border-entity/20"
            />
            <StatCard
              title="Unique Authors"
              value={stats.bookStats.uniqueAuthors}
              icon={Users}
              colorClass="bg-entity/10 text-entity border-entity/20"
            />
            <StatCard
              title="Formats"
              value={stats.bookStats.totalFormats}
              icon={FileText}
              colorClass="bg-entity/10 text-entity border-entity/20"
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
                <BarChart data={bookPageBucketData} color={CHART_HEX.books} orientation="vertical" />
              </ChartCard>
            )}
            {bookTopAuthors.length > 0 && (
              <ChartCard title="Top Authors">
                <BarChart data={bookTopAuthors} color={CHART_HEX.books} orientation="horizontal" />
              </ChartCard>
            )}
            {bookDecadeData.length > 0 && (
              <ChartCard title="Publication Decade">
                <BarChart data={bookDecadeData} color={CHART_HEX.books} orientation="vertical" />
              </ChartCard>
            )}
            {bookTopGenres.length > 0 && (
              <ChartCard title="Top Genres">
                <BarChart data={bookTopGenres} color={CHART_HEX.books} orientation="horizontal" />
              </ChartCard>
            )}
          </div>
        </div>
      ),
    },
    stats.movieStats.total > 0 && {
      id: 'movies',
      className: 'entity-movies',
      activeTopBorderClass: '!border-t-entity',
      label: (
        <div>
          <div className="flex items-center gap-2">
            <Clapperboard className="h-4 w-4 text-entity" />
            <span className="font-semibold text-white">Movies & TV</span>
          </div>
          <div className="mt-2 text-2xl font-bold text-entity">{stats.movieStats.total.toLocaleString()}</div>
          <div className="text-xs text-zinc-400">{pct(stats.movieStats.total, totalItems)}% of collection</div>
        </div>
      ),
      panel: (
        <div className="entity-movies space-y-4 p-6">
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
            <StatCard
              title="Movies"
              value={stats.movieStats.total - stats.movieStats.totalTvSeries}
              icon={Clapperboard}
              colorClass="bg-entity/10 text-entity border-entity/20"
            />
            <StatCard
              title="TV Series"
              value={stats.movieStats.totalTvSeries}
              icon={Tv}
              colorClass="bg-entity/10 text-entity border-entity/20"
            />
            <StatCard
              title="Formats"
              value={stats.movieStats.totalFormats}
              icon={Disc}
              colorClass="bg-entity/10 text-entity border-entity/20"
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
                <BarChart data={movieTopStudios} color={CHART_HEX.movies} orientation="horizontal" />
              </ChartCard>
            )}
            {movieDecadeData.length > 0 && (
              <ChartCard title="Release Decade">
                <BarChart data={movieDecadeData} color={CHART_HEX.movies} orientation="vertical" />
              </ChartCard>
            )}
            {movieGenreData.length > 0 && (
              <ChartCard title="Top Genres">
                <BarChart data={movieGenreData} color={CHART_HEX.movies} orientation="horizontal" />
              </ChartCard>
            )}
          </div>
        </div>
      ),
    },
    stats.gameStats.total > 0 && {
      id: 'games',
      className: 'entity-games',
      activeTopBorderClass: '!border-t-entity',
      label: (
        <div>
          <div className="flex items-center gap-2">
            <Gamepad2 className="h-4 w-4 text-entity" />
            <span className="font-semibold text-white">Games</span>
          </div>
          <div className="mt-2 text-2xl font-bold text-entity">{stats.gameStats.total.toLocaleString()}</div>
          <div className="text-xs text-zinc-400">{pct(stats.gameStats.total, totalItems)}% of collection</div>
        </div>
      ),
      panel: (
        <div className="entity-games space-y-4 p-6">
          <div className="grid grid-cols-2 gap-3">
            <StatCard
              title="Platforms"
              value={stats.gameStats.totalPlatforms}
              icon={Monitor}
              colorClass="bg-entity/10 text-entity border-entity/20"
            />
            <StatCard
              title="Formats"
              value={stats.gameStats.totalFormats}
              icon={Disc}
              colorClass="bg-entity/10 text-entity border-entity/20"
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
                <BarChart data={gameTopPublishers} color={CHART_HEX.games} orientation="horizontal" />
              </ChartCard>
            )}
            {gameTopDevelopers.length > 0 && (
              <ChartCard title="Top Developers">
                <BarChart data={gameTopDevelopers} color={CHART_HEX.games} orientation="horizontal" />
              </ChartCard>
            )}
            {gameDecadeData.length > 0 && (
              <ChartCard title="Release Decade">
                <BarChart data={gameDecadeData} color={CHART_HEX.games} orientation="vertical" />
              </ChartCard>
            )}
            {gameGenreData.length > 0 && (
              <ChartCard title="Top Genres">
                <BarChart data={gameGenreData} color={CHART_HEX.games} orientation="horizontal" />
              </ChartCard>
            )}
          </div>
        </div>
      ),
    },
    stats.musicStats.total > 0 && {
      id: 'musics',
      className: 'entity-musics',
      activeTopBorderClass: '!border-t-entity',
      label: (
        <div>
          <div className="flex items-center gap-2">
            <Music className="h-4 w-4 text-entity" />
            <span className="font-semibold text-white">Music</span>
          </div>
          <div className="mt-2 text-2xl font-bold text-entity">{stats.musicStats.total.toLocaleString()}</div>
          <div className="text-xs text-zinc-400">{pct(stats.musicStats.total, totalItems)}% of collection</div>
        </div>
      ),
      panel: (
        <div className="entity-musics space-y-4 p-6">
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
            <StatCard
              title="Total Tracks"
              value={stats.musicStats.totalTracks.toLocaleString()}
              icon={Album}
              colorClass="bg-entity/10 text-entity border-entity/20"
            />
            <StatCard
              title="Avg Tracks"
              value={stats.musicStats.avgTracks}
              icon={Hash}
              colorClass="bg-entity/10 text-entity border-entity/20"
            />
            <StatCard
              title="Unique Artists"
              value={stats.musicStats.uniqueArtists}
              icon={Users}
              colorClass="bg-entity/10 text-entity border-entity/20"
            />
            <StatCard
              title="Unique Labels"
              value={stats.musicStats.uniqueLabels}
              icon={Tag}
              colorClass="bg-entity/10 text-entity border-entity/20"
            />
            <StatCard
              title="Total Discs"
              value={stats.musicStats.totalDiscs}
              icon={Layers}
              colorClass="bg-entity/10 text-entity border-entity/20"
            />
            <StatCard
              title="Formats"
              value={stats.musicStats.totalFormats}
              icon={Disc}
              colorClass="bg-entity/10 text-entity border-entity/20"
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
                <BarChart data={musicTopArtists} color={CHART_HEX.musics} orientation="horizontal" />
              </ChartCard>
            )}
            {musicTopLabels.length > 0 && (
              <ChartCard title="Top Labels">
                <BarChart data={musicTopLabels} color={CHART_HEX.musics} orientation="horizontal" />
              </ChartCard>
            )}
            {musicDecadeData.length > 0 && (
              <ChartCard title="Release Decade">
                <BarChart data={musicDecadeData} color={CHART_HEX.musics} orientation="vertical" />
              </ChartCard>
            )}
            {musicGenreData.length > 0 && (
              <ChartCard title="Top Genres">
                <BarChart data={musicGenreData} color={CHART_HEX.musics} orientation="horizontal" />
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
