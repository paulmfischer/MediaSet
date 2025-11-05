import { json, type MetaFunction } from "@remix-run/node";
import { useLoaderData } from "@remix-run/react";
import { getStats } from "~/stats-data";
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
} from "lucide-react";
import StatCard from "~/components/StatCard";

export const meta: MetaFunction = () => {
  return [
    { title: "Dashboard | MediaSet" },
    { name: "description", content: "Your personal media collection dashboard" },
  ];
};

export const loader = async () => {
  const stats = await getStats();
  return json({ stats });
};

export default function Index() {
  const { stats } = useLoaderData<typeof loader>();
  
  const totalItems =
    stats.bookStats.total +
    stats.movieStats.total +
    stats.gameStats.total +
    stats.musicStats.total;

  const isEmpty = totalItems === 0;

  return (
    <div className="space-y-8">
      {/* Hero Section */}
      <div className="rounded-lg border border-zinc-700 bg-gradient-to-br from-zinc-800/50 to-zinc-900/50 p-8">
        <h1 className="text-3xl font-bold text-white">Welcome to MediaSet</h1>
        <p className="mt-2 text-zinc-400">
          Your personal media collection dashboard
        </p>
        <div className="mt-6 flex items-center gap-4">
          <div className="rounded-lg bg-blue-500/10 px-4 py-2">
            <span className="text-2xl font-bold text-blue-400">{totalItems}</span>
            <span className="ml-2 text-sm text-zinc-400">Total Items</span>
          </div>
        </div>
      </div>

      {isEmpty ? (
        /* Empty State */
        <div className="rounded-lg border border-zinc-700 bg-zinc-800/30 p-12 text-center">
          <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-zinc-700/50">
            <LibraryBig className="h-8 w-8 text-zinc-400" />
          </div>
          <h2 className="mt-4 text-xl font-semibold text-white">
            No media items yet
          </h2>
          <p className="mt-2 text-zinc-400">
            Start building your collection by adding books, movies, games, or music.
          </p>
        </div>
      ) : (
        <>
          {/* Overview Cards */}
          <div>
            <h2 className="mb-4 text-xl font-semibold text-white">Overview</h2>
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <StatCard
                title="Books"
                value={stats.bookStats.total}
                icon={LibraryBig}
                subtitle={`${stats.bookStats.totalFormats} formats`}
                colorClass="bg-green-500/10 text-green-400 border-green-500/20"
              />
              <StatCard
                title="Movies & TV"
                value={stats.movieStats.total}
                icon={Clapperboard}
                subtitle={`${stats.movieStats.totalFormats} formats`}
                colorClass="bg-red-500/10 text-red-400 border-red-500/20"
              />
              <StatCard
                title="Games"
                value={stats.gameStats.total}
                icon={Gamepad2}
                subtitle={`${stats.gameStats.totalPlatforms} platforms`}
                colorClass="bg-purple-500/10 text-purple-400 border-purple-500/20"
              />
              <StatCard
                title="Music"
                value={stats.musicStats.total}
                icon={Music}
                subtitle={`${stats.musicStats.uniqueArtists} artists`}
                colorClass="bg-pink-500/10 text-pink-400 border-pink-500/20"
              />
            </div>
          </div>

          {/* Books Section */}
          {stats.bookStats.total > 0 && (
            <div>
              <h2 className="mb-4 flex items-center gap-2 text-xl font-semibold text-white">
                <LibraryBig className="h-5 w-5 text-green-400" />
                Books
              </h2>
              <div className="space-y-4">
                <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
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
                {stats.bookStats.formats.length > 0 && (
                  <div className="rounded-lg border border-zinc-700 bg-zinc-800/30 p-4">
                    <p className="mb-2 text-sm font-medium text-zinc-400">Formats</p>
                    <div className="flex flex-wrap gap-2">
                      {stats.bookStats.formats.map((format) => (
                        <span
                          key={format}
                          className="rounded-md bg-green-500/10 px-3 py-1 text-sm text-green-400 border border-green-500/20"
                        >
                          {format}
                        </span>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Movies Section */}
          {stats.movieStats.total > 0 && (
            <div>
              <h2 className="mb-4 flex items-center gap-2 text-xl font-semibold text-white">
                <Clapperboard className="h-5 w-5 text-red-400" />
                Movies & TV Shows
              </h2>
              <div className="space-y-4">
                <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                  <StatCard
                    title="TV Series"
                    value={stats.movieStats.totalTvSeries}
                    icon={Tv}
                    colorClass="bg-red-500/10 text-red-400 border-red-500/20"
                  />
                  <StatCard
                    title="Movies"
                    value={stats.movieStats.total - stats.movieStats.totalTvSeries}
                    icon={Clapperboard}
                    colorClass="bg-red-500/10 text-red-400 border-red-500/20"
                  />
                  <StatCard
                    title="Formats"
                    value={stats.movieStats.totalFormats}
                    icon={Disc}
                    colorClass="bg-red-500/10 text-red-400 border-red-500/20"
                  />
                </div>
                {stats.movieStats.formats.length > 0 && (
                  <div className="rounded-lg border border-zinc-700 bg-zinc-800/30 p-4">
                    <p className="mb-2 text-sm font-medium text-zinc-400">Formats</p>
                    <div className="flex flex-wrap gap-2">
                      {stats.movieStats.formats.map((format) => (
                        <span
                          key={format}
                          className="rounded-md bg-red-500/10 px-3 py-1 text-sm text-red-400 border border-red-500/20"
                        >
                          {format}
                        </span>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Games Section */}
          {stats.gameStats.total > 0 && (
            <div>
              <h2 className="mb-4 flex items-center gap-2 text-xl font-semibold text-white">
                <Gamepad2 className="h-5 w-5 text-purple-400" />
                Games
              </h2>
              <div className="space-y-4">
                <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
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
                {stats.gameStats.formats.length > 0 && (
                  <div className="rounded-lg border border-zinc-700 bg-zinc-800/30 p-4">
                    <p className="mb-2 text-sm font-medium text-zinc-400">Formats</p>
                    <div className="flex flex-wrap gap-2">
                      {stats.gameStats.formats.map((format) => (
                        <span
                          key={format}
                          className="rounded-md bg-purple-500/10 px-3 py-1 text-sm text-purple-400 border border-purple-500/20"
                        >
                          {format}
                        </span>
                      ))}
                    </div>
                  </div>
                )}
                {stats.gameStats.platforms.length > 0 && (
                  <div className="rounded-lg border border-zinc-700 bg-zinc-800/30 p-4">
                    <p className="mb-2 text-sm font-medium text-zinc-400">Platforms</p>
                    <div className="flex flex-wrap gap-2">
                      {stats.gameStats.platforms.map((platform) => (
                        <span
                          key={platform}
                          className="rounded-md bg-purple-500/10 px-3 py-1 text-sm text-purple-400 border border-purple-500/20"
                        >
                          {platform}
                        </span>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Music Section */}
          {stats.musicStats.total > 0 && (
            <div>
              <h2 className="mb-4 flex items-center gap-2 text-xl font-semibold text-white">
                <Music className="h-5 w-5 text-pink-400" />
                Music
              </h2>
              <div className="space-y-4">
                <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
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
                {stats.musicStats.formats.length > 0 && (
                  <div className="rounded-lg border border-zinc-700 bg-zinc-800/30 p-4">
                    <p className="mb-2 text-sm font-medium text-zinc-400">Formats</p>
                    <div className="flex flex-wrap gap-2">
                      {stats.musicStats.formats.map((format) => (
                        <span
                          key={format}
                          className="rounded-md bg-pink-500/10 px-3 py-1 text-sm text-pink-400 border border-pink-500/20"
                        >
                          {format}
                        </span>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
