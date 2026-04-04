import { json, type MetaFunction } from '@remix-run/node';
import { useLoaderData } from '@remix-run/react';
import { useState } from 'react';
import { getStats } from '~/api/stats-data';
import { getIntegrations } from '~/api/integrations-data';
import AttributionBadges from '~/components/attribution-badges';
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

type EntityType = 'books' | 'movies' | 'games' | 'music';

const ENTITY_COLORS: Record<EntityType, string> = {
  books: '#22c55e',
  movies: '#ef4444',
  games: '#a855f7',
  music: '#ec4899',
};

const FORMAT_PALETTES: Record<EntityType, string[]> = {
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

export default function Index() {
  const { stats, integrations } = useLoaderData<typeof loader>();
  const [selectedEntity, setSelectedEntity] = useState<EntityType>('books');

  const totalItems = stats.bookStats.total + stats.movieStats.total + stats.gameStats.total + stats.musicStats.total;

  const isEmpty = totalItems === 0;

  const overviewData = [
    { name: 'Books', value: stats.bookStats.total, entity: 'books' as EntityType },
    { name: 'Movies & TV', value: stats.movieStats.total, entity: 'movies' as EntityType },
    { name: 'Games', value: stats.gameStats.total, entity: 'games' as EntityType },
    { name: 'Music', value: stats.musicStats.total, entity: 'music' as EntityType },
  ].filter((d) => d.value > 0);

  const overviewColors = overviewData.map((d) => ENTITY_COLORS[d.entity]);

  function handleOverviewSliceClick(name: string) {
    const item = overviewData.find((d) => d.name === name);
    if (item) setSelectedEntity(item.entity);
  }

  const bookFormatData = breakdownToChartData(stats.bookStats.formatBreakdown);
  const movieFormatData = breakdownToChartData(stats.movieStats.formatBreakdown);
  const movieTypeData = [
    { name: 'Movies', value: stats.movieStats.total - stats.movieStats.totalTvSeries },
    { name: 'TV Series', value: stats.movieStats.totalTvSeries },
  ].filter((d) => d.value > 0);
  const gameFormatData = breakdownToChartData(stats.gameStats.formatBreakdown);
  const gamePlatformData = breakdownToChartData(stats.gameStats.platformBreakdown);
  const musicFormatData = breakdownToChartData(stats.musicStats.formatBreakdown);

  return (
    <div className="space-y-8">
      {/* Hero */}
      <div className="rounded-lg border border-zinc-700 bg-gradient-to-br from-zinc-800/50 to-zinc-900/50 p-8">
        <h1 className="text-3xl font-bold text-white">Welcome to MediaSet</h1>
        <p className="mt-2 text-zinc-400">Your personal media collection dashboard</p>
        <div className="mt-6 flex items-center gap-4">
          <div className="rounded-lg bg-blue-500/10 px-4 py-2">
            <span className="text-2xl font-bold text-blue-400">{totalItems}</span>
            <span className="ml-2 text-sm text-zinc-400">Total Items</span>
          </div>
        </div>
      </div>

      {isEmpty ? (
        <div className="rounded-lg border border-zinc-700 bg-zinc-800/30 p-12 text-center">
          <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-zinc-700/50">
            <LibraryBig className="h-8 w-8 text-zinc-400" />
          </div>
          <h2 className="mt-4 text-xl font-semibold text-white">No media items yet</h2>
          <p className="mt-2 text-zinc-400">Start building your collection by adding books, movies, games, or music.</p>
        </div>
      ) : (
        <div className="rounded-lg border border-zinc-700 bg-zinc-800/30 p-6">
          <h2 className="mb-6 text-xl font-semibold text-white">Collection Overview</h2>
          <div className="grid gap-8 lg:grid-cols-2">
            {/* Left: overview pie chart */}
            <div className="flex flex-col items-center justify-center">
              <p className="mb-4 text-sm text-zinc-400">Click a slice to explore</p>
              <PieChart
                data={overviewData}
                colors={overviewColors}
                onSliceClick={handleOverviewSliceClick}
                activeSlice={
                  ENTITY_COLORS[selectedEntity]
                    ? overviewData.find((d) => d.entity === selectedEntity)?.name
                    : undefined
                }
              />
            </div>

            {/* Right: detail panel */}
            <div>
              {selectedEntity === 'books' && stats.bookStats.total > 0 && (
                <div className="space-y-4">
                  <h3 className="flex items-center gap-2 text-lg font-semibold text-white">
                    <LibraryBig className="h-5 w-5 text-green-400" />
                    Books
                  </h3>
                  <div className="grid grid-cols-2 gap-3">
                    <StatCard
                      title="Total"
                      value={stats.bookStats.total}
                      icon={LibraryBig}
                      colorClass="bg-green-500/10 text-green-400 border-green-500/20"
                    />
                    <StatCard
                      title="Total Pages"
                      value={stats.bookStats.totalPages.toLocaleString()}
                      icon={BookOpen}
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
                  {bookFormatData.length > 1 ? (
                    <div className="rounded-lg border border-zinc-700 bg-zinc-800/50 p-4">
                      <p className="mb-3 text-sm font-medium text-zinc-400">Format Breakdown</p>
                      <PieChart data={bookFormatData} colors={FORMAT_PALETTES.books} size={180} />
                    </div>
                  ) : stats.bookStats.formats.length > 0 ? (
                    <div className="rounded-lg border border-zinc-700 bg-zinc-800/50 p-4">
                      <p className="mb-2 text-sm font-medium text-zinc-400">Formats</p>
                      <div className="flex flex-wrap gap-2">
                        {stats.bookStats.formats.map((f) => (
                          <span
                            key={f}
                            className="rounded-md border border-green-500/20 bg-green-500/10 px-3 py-1 text-sm text-green-400"
                          >
                            {f}
                          </span>
                        ))}
                      </div>
                    </div>
                  ) : null}
                </div>
              )}

              {selectedEntity === 'movies' && stats.movieStats.total > 0 && (
                <div className="space-y-4">
                  <h3 className="flex items-center gap-2 text-lg font-semibold text-white">
                    <Clapperboard className="h-5 w-5 text-red-400" />
                    Movies & TV Shows
                  </h3>
                  <div className="grid grid-cols-2 gap-3">
                    <StatCard
                      title="Total"
                      value={stats.movieStats.total}
                      icon={Clapperboard}
                      colorClass="bg-red-500/10 text-red-400 border-red-500/20"
                    />
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
                  {movieTypeData.length > 1 && (
                    <div className="rounded-lg border border-zinc-700 bg-zinc-800/50 p-4">
                      <p className="mb-3 text-sm font-medium text-zinc-400">Movies vs TV Series</p>
                      <PieChart data={movieTypeData} colors={MOVIE_TYPE_COLORS} size={180} />
                    </div>
                  )}
                  {movieFormatData.length > 1 ? (
                    <div className="rounded-lg border border-zinc-700 bg-zinc-800/50 p-4">
                      <p className="mb-3 text-sm font-medium text-zinc-400">Format Breakdown</p>
                      <PieChart data={movieFormatData} colors={FORMAT_PALETTES.movies} size={180} />
                    </div>
                  ) : stats.movieStats.formats.length > 0 ? (
                    <div className="rounded-lg border border-zinc-700 bg-zinc-800/50 p-4">
                      <p className="mb-2 text-sm font-medium text-zinc-400">Formats</p>
                      <div className="flex flex-wrap gap-2">
                        {stats.movieStats.formats.map((f) => (
                          <span
                            key={f}
                            className="rounded-md border border-red-500/20 bg-red-500/10 px-3 py-1 text-sm text-red-400"
                          >
                            {f}
                          </span>
                        ))}
                      </div>
                    </div>
                  ) : null}
                </div>
              )}

              {selectedEntity === 'games' && stats.gameStats.total > 0 && (
                <div className="space-y-4">
                  <h3 className="flex items-center gap-2 text-lg font-semibold text-white">
                    <Gamepad2 className="h-5 w-5 text-purple-400" />
                    Games
                  </h3>
                  <div className="grid grid-cols-2 gap-3">
                    <StatCard
                      title="Total"
                      value={stats.gameStats.total}
                      icon={Gamepad2}
                      colorClass="bg-purple-500/10 text-purple-400 border-purple-500/20"
                    />
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
                  {gamePlatformData.length > 1 ? (
                    <div className="rounded-lg border border-zinc-700 bg-zinc-800/50 p-4">
                      <p className="mb-3 text-sm font-medium text-zinc-400">Platform Breakdown</p>
                      <PieChart data={gamePlatformData} colors={FORMAT_PALETTES.games} size={180} />
                    </div>
                  ) : stats.gameStats.platforms.length > 0 ? (
                    <div className="rounded-lg border border-zinc-700 bg-zinc-800/50 p-4">
                      <p className="mb-2 text-sm font-medium text-zinc-400">Platforms</p>
                      <div className="flex flex-wrap gap-2">
                        {stats.gameStats.platforms.map((p) => (
                          <span
                            key={p}
                            className="rounded-md border border-purple-500/20 bg-purple-500/10 px-3 py-1 text-sm text-purple-400"
                          >
                            {p}
                          </span>
                        ))}
                      </div>
                    </div>
                  ) : null}
                  {gameFormatData.length > 1 ? (
                    <div className="rounded-lg border border-zinc-700 bg-zinc-800/50 p-4">
                      <p className="mb-3 text-sm font-medium text-zinc-400">Format Breakdown</p>
                      <PieChart data={gameFormatData} colors={FORMAT_PALETTES.games} size={180} />
                    </div>
                  ) : stats.gameStats.formats.length > 0 && gamePlatformData.length <= 1 ? (
                    <div className="rounded-lg border border-zinc-700 bg-zinc-800/50 p-4">
                      <p className="mb-2 text-sm font-medium text-zinc-400">Formats</p>
                      <div className="flex flex-wrap gap-2">
                        {stats.gameStats.formats.map((f) => (
                          <span
                            key={f}
                            className="rounded-md border border-purple-500/20 bg-purple-500/10 px-3 py-1 text-sm text-purple-400"
                          >
                            {f}
                          </span>
                        ))}
                      </div>
                    </div>
                  ) : null}
                </div>
              )}

              {selectedEntity === 'music' && stats.musicStats.total > 0 && (
                <div className="space-y-4">
                  <h3 className="flex items-center gap-2 text-lg font-semibold text-white">
                    <Music className="h-5 w-5 text-pink-400" />
                    Music
                  </h3>
                  <div className="grid grid-cols-2 gap-3">
                    <StatCard
                      title="Total"
                      value={stats.musicStats.total}
                      icon={Music}
                      colorClass="bg-pink-500/10 text-pink-400 border-pink-500/20"
                    />
                    <StatCard
                      title="Total Tracks"
                      value={stats.musicStats.totalTracks.toLocaleString()}
                      icon={Album}
                      colorClass="bg-pink-500/10 text-pink-400 border-pink-500/20"
                    />
                    <StatCard
                      title="Unique Artists"
                      value={stats.musicStats.uniqueArtists}
                      icon={Users}
                      colorClass="bg-pink-500/10 text-pink-400 border-pink-500/20"
                    />
                    <StatCard
                      title="Formats"
                      value={stats.musicStats.totalFormats}
                      icon={Disc}
                      colorClass="bg-pink-500/10 text-pink-400 border-pink-500/20"
                    />
                  </div>
                  {musicFormatData.length > 1 ? (
                    <div className="rounded-lg border border-zinc-700 bg-zinc-800/50 p-4">
                      <p className="mb-3 text-sm font-medium text-zinc-400">Format Breakdown</p>
                      <PieChart data={musicFormatData} colors={FORMAT_PALETTES.music} size={180} />
                    </div>
                  ) : stats.musicStats.formats.length > 0 ? (
                    <div className="rounded-lg border border-zinc-700 bg-zinc-800/50 p-4">
                      <p className="mb-2 text-sm font-medium text-zinc-400">Formats</p>
                      <div className="flex flex-wrap gap-2">
                        {stats.musicStats.formats.map((f) => (
                          <span
                            key={f}
                            className="rounded-md border border-pink-500/20 bg-pink-500/10 px-3 py-1 text-sm text-pink-400"
                          >
                            {f}
                          </span>
                        ))}
                      </div>
                    </div>
                  ) : null}
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      <AttributionBadges integrations={integrations} />
    </div>
  );
}
