import type { IconNode } from 'lucide';
import type { Handle } from 'remix/component';

import type {
  PlexCount,
  PlexKindStatistics,
  PlexMusicAlbum,
  PlexMusicStatistics,
  PlexStatistics,
} from '../../models.ts';
import { formatBytes, formatDuration, statisticLabel } from '../../utils/plex-statistics.ts';
import { BarChart } from '../dashboard/bar-chart.tsx';
import { ChartCard } from '../dashboard/chart-card.tsx';
import { CHART_HEX, FORMAT_PALETTES, STAT_CARD_COLOR, type DashboardTabId } from '../dashboard/config.ts';
import { Icon } from '../icon.tsx';
import { PieChart } from '../dashboard/pie-chart.tsx';
import { StatCard } from '../dashboard/stat-card.tsx';
import { AlertTriangle, Album, Clapperboard, Disc, HardDrive, Layers, Link2Off, Music, Tv } from '../icons.ts';

const num = (value: number): string => value.toLocaleString('en-US');

function slices(counts: PlexCount[], upper = false): Array<{ name: string; value: number }> {
  return counts.filter((c) => c.count > 0).map((c) => ({ name: statisticLabel(c.name, upper), value: c.count }));
}

function PieCard(handle: Handle<{ title: string; counts: PlexCount[]; upper?: boolean; entity: DashboardTabId }>) {
  return () => {
    const { title, counts, upper, entity } = handle.props;
    const data = slices(counts, upper);
    if (data.length === 0) return null;
    return (
      <ChartCard title={title}>
        <PieChart title={title} data={data} colors={FORMAT_PALETTES[entity]} size={140} />
      </ChartCard>
    );
  };
}

function BarCard(handle: Handle<{ title: string; counts: PlexCount[]; upper?: boolean; entity: DashboardTabId }>) {
  return () => {
    const { title, counts, upper, entity } = handle.props;
    const data = slices(counts, upper);
    if (data.length === 0) return null;
    return (
      <ChartCard title={title}>
        <BarChart data={data} color={CHART_HEX[entity]} />
      </ChartCard>
    );
  };
}

function KindSection(handle: Handle<{ stats: PlexKindStatistics; label: string; unit: string }>) {
  return () => {
    const { stats, label, unit } = handle.props;
    const headingId = `plex-stats-${stats.kind}-heading`;
    const { coverage, completeness } = stats;
    return (
      <section aria-labelledby={headingId} class="space-y-4" data-testid="plex-stats-kind" data-kind={stats.kind}>
        <h3 id={headingId} class="text-base font-semibold text-white">
          {label}
        </h3>
        <div class="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <StatCard
            title={`${label} in MediaSet`}
            value={num(coverage.total)}
            icon={Clapperboard}
            colorClass={STAT_CARD_COLOR}
          />
          <StatCard title="On Plex" value={num(coverage.onPlex)} icon={Tv} colorClass={STAT_CARD_COLOR} />
          <StatCard title="Not on Plex" value={num(coverage.notOnPlex)} icon={Link2Off} colorClass={STAT_CARD_COLOR} />
          <StatCard
            title="Broken links"
            value={num(coverage.broken)}
            icon={AlertTriangle}
            colorClass={STAT_CARD_COLOR}
          />
        </div>
        {stats.units === 0 ? (
          <p class="text-sm text-zinc-400" data-testid="plex-stats-empty">
            No {unit} on Plex yet, so there is nothing to break down.
          </p>
        ) : (
          <>
            <div class="grid grid-cols-2 gap-3 sm:grid-cols-4">
              <StatCard
                title={`Total size (${num(stats.units)} ${unit})`}
                value={formatBytes(stats.storage.totalBytes)}
                icon={HardDrive}
                colorClass={STAT_CARD_COLOR}
              />
              <StatCard
                title="Average size"
                value={formatBytes(stats.storage.averageBytes)}
                icon={HardDrive}
                colorClass={STAT_CARD_COLOR}
              />
              <StatCard
                title="Total runtime"
                value={formatDuration(stats.runtime.totalSeconds)}
                icon={Layers}
                colorClass={STAT_CARD_COLOR}
              />
              <StatCard
                title="Average runtime"
                value={formatDuration(stats.runtime.averageSeconds)}
                icon={Layers}
                colorClass={STAT_CARD_COLOR}
              />
            </div>
            <p class="text-xs text-zinc-400">The charts below count {unit} that are on Plex.</p>
            <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
              <PieCard title="Resolution" counts={stats.resolution} upper entity="movies" />
              <PieCard title="Video codec" counts={stats.videoCodec} upper entity="movies" />
              <PieCard title="HDR vs SDR" counts={stats.hdr} entity="movies" />
              <PieCard title="Subtitles" counts={stats.subtitles} entity="movies" />
              <PieCard title="Watched" counts={stats.watched} entity="movies" />
              <BarCard title="Audio codec" counts={stats.audioCodec} upper entity="movies" />
              <BarCard title="Audio channel layout" counts={stats.audioChannels} entity="movies" />
            </div>
          </>
        )}
        {completeness && completeness.episodesExpected > 0 ? (
          <div class="rounded-lg border border-zinc-700 bg-zinc-800/50 p-4" data-testid="plex-stats-completeness">
            <p class="text-sm font-medium text-zinc-400">Episode completeness</p>
            <p class="mt-2 flex items-center gap-2 text-2xl font-bold text-white">
              <span>
                {num(completeness.episodesPresent)} of {num(completeness.episodesExpected)} episodes
              </span>
            </p>
            <p class="mt-1 text-xs text-zinc-400">
              {num(completeness.entriesComplete)} complete, {num(completeness.entriesIncomplete)} missing episodes.
              Expected is the count Plex reports for the covered seasons.
            </p>
          </div>
        ) : null}
        {stats.mostWatched.length > 0 ? (
          <ChartCard title={stats.kind === 'tv' ? 'Most watched (episodes watched)' : 'Most watched'}>
            <BarChart
              data={stats.mostWatched.map((item) => ({ name: item.title, value: item.viewCount }))}
              color={CHART_HEX.movies}
            />
          </ChartCard>
        ) : null}
      </section>
    );
  };
}

// An album's date as "Mar 4, 2024"; the API sends UTC instants, so the date is read in UTC to match what Plex reports.
function formatDate(value: string | null): string {
  if (!value) return 'Unknown';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return 'Unknown';
  return date.toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric', timeZone: 'UTC' });
}

function AlbumListCard(
  handle: Handle<{ title: string; albums: PlexMusicAlbum[]; testId: string; detail: (album: PlexMusicAlbum) => string }>
) {
  return () => {
    const { title, albums, testId, detail } = handle.props;
    if (albums.length === 0) return null;
    return (
      <ChartCard title={title}>
        <ol class="space-y-1 text-sm" data-testid={testId}>
          {albums.map((album) => (
            <li key={album.id} class="flex items-baseline justify-between gap-3">
              <span class="min-w-0 truncate text-white">
                {album.title} <span class="text-zinc-400">by {album.artist || 'Unknown artist'}</span>
              </span>
              <span class="shrink-0 text-xs text-zinc-400">{detail(album)}</span>
            </li>
          ))}
        </ol>
      </ChartCard>
    );
  };
}

function MusicSection(handle: Handle<{ stats: PlexMusicStatistics }>) {
  return () => {
    const { stats } = handle.props;
    const { coverage, completeness } = stats;
    return (
      <section
        aria-labelledby="plex-stats-music-heading"
        class="space-y-4"
        data-testid="plex-stats-kind"
        data-kind="music"
      >
        <h3 id="plex-stats-music-heading" class="text-base font-semibold text-white">
          Music
        </h3>
        <div class="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <StatCard title="Albums in MediaSet" value={num(coverage.total)} icon={Album} colorClass={STAT_CARD_COLOR} />
          <StatCard title="On Plex" value={num(coverage.onPlex)} icon={Music} colorClass={STAT_CARD_COLOR} />
          <StatCard title="Not on Plex" value={num(coverage.notOnPlex)} icon={Link2Off} colorClass={STAT_CARD_COLOR} />
          <StatCard
            title="Broken links"
            value={num(coverage.broken)}
            icon={AlertTriangle}
            colorClass={STAT_CARD_COLOR}
          />
        </div>
        {stats.albums === 0 ? (
          <p class="text-sm text-zinc-400" data-testid="plex-stats-empty">
            No albums on Plex yet, so there is nothing to break down.
          </p>
        ) : (
          <>
            <div class="grid grid-cols-2 gap-3 sm:grid-cols-4">
              <StatCard title="Total plays" value={num(stats.totalPlays)} icon={Disc} colorClass={STAT_CARD_COLOR} />
              <StatCard title="Never played" value={num(stats.neverPlayed)} icon={Disc} colorClass={STAT_CARD_COLOR} />
              <StatCard
                title={`Total size (${num(stats.albums)} albums)`}
                value={formatBytes(stats.storage.totalBytes)}
                icon={HardDrive}
                colorClass={STAT_CARD_COLOR}
              />
              <StatCard
                title="Average size"
                value={formatBytes(stats.storage.averageBytes)}
                icon={HardDrive}
                colorClass={STAT_CARD_COLOR}
              />
            </div>
            <p class="text-xs text-zinc-400">The charts below count albums that are on Plex.</p>
            <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
              <PieCard title="Audio codec" counts={stats.audioCodec} upper entity="musics" />
              <BarCard title="Bitrate" counts={stats.bitrate} entity="musics" />
            </div>
          </>
        )}
        {completeness.tracksExpected > 0 ? (
          <div class="rounded-lg border border-zinc-700 bg-zinc-800/50 p-4" data-testid="plex-stats-completeness">
            <p class="text-sm font-medium text-zinc-400">Track completeness</p>
            <p class="mt-2 flex items-center gap-2 text-2xl font-bold text-white">
              <span>
                {num(completeness.tracksPresent)} of {num(completeness.tracksExpected)} tracks
              </span>
            </p>
            <p class="mt-1 text-xs text-zinc-400">
              {num(completeness.albumsComplete)} complete, {num(completeness.albumsPartial)} partial. Expected is the
              track count of the album in MediaSet.
            </p>
          </div>
        ) : null}
        {stats.mostPlayed.length > 0 ? (
          <ChartCard title="Most played">
            <BarChart
              data={stats.mostPlayed.map((album) => ({
                name: `${album.title} (${album.artist || 'Unknown artist'})`,
                value: album.playCount,
              }))}
              color={CHART_HEX.musics}
            />
          </ChartCard>
        ) : null}
        <div class="grid grid-cols-1 gap-4 lg:grid-cols-2">
          <AlbumListCard
            title="Recently played"
            albums={stats.recentlyPlayed}
            testId="plex-stats-recently-played"
            detail={(album) => formatDate(album.lastPlayedAt)}
          />
          <AlbumListCard
            title="Recently added"
            albums={stats.recentlyAdded}
            testId="plex-stats-recently-added"
            detail={(album) => formatDate(album.dateAdded)}
          />
        </div>
      </section>
    );
  };
}

interface PlexTabConfig {
  id: 'movies' | 'music';
  /** Sets --entity-color for the tab and its panel. */
  entityClass: string;
  label: string;
  icon: IconNode;
  inputId: string;
  // Tailwind only generates classes it finds as complete strings, so each tab's state variants are written out in
  // full. They key off the tab's checked radio through the wrapper's `tabs` group (as on the dashboard).
  activeLabelClass: string;
  focusLabelClass: string;
  activePanelClass: string;
}

const PLEX_TABS: PlexTabConfig[] = [
  {
    id: 'movies',
    entityClass: 'entity-movies',
    label: 'Movies & TV',
    icon: Clapperboard,
    inputId: 'plex-tab-movies',
    activeLabelClass:
      'group-has-[#plex-tab-movies:checked]/tabs:relative group-has-[#plex-tab-movies:checked]/tabs:z-10 group-has-[#plex-tab-movies:checked]/tabs:-mb-[5px] group-has-[#plex-tab-movies:checked]/tabs:rounded-b-none group-has-[#plex-tab-movies:checked]/tabs:border-x-zinc-700 group-has-[#plex-tab-movies:checked]/tabs:border-b-zinc-900 group-has-[#plex-tab-movies:checked]/tabs:border-t-entity group-has-[#plex-tab-movies:checked]/tabs:bg-zinc-800',
    focusLabelClass:
      'group-has-[#plex-tab-movies:focus-visible]/tabs:ring-2 group-has-[#plex-tab-movies:focus-visible]/tabs:ring-blue-400',
    activePanelClass: 'group-has-[#plex-tab-movies:checked]/tabs:block',
  },
  {
    id: 'music',
    entityClass: 'entity-musics',
    label: 'Music',
    icon: Music,
    inputId: 'plex-tab-music',
    activeLabelClass:
      'group-has-[#plex-tab-music:checked]/tabs:relative group-has-[#plex-tab-music:checked]/tabs:z-10 group-has-[#plex-tab-music:checked]/tabs:-mb-[5px] group-has-[#plex-tab-music:checked]/tabs:rounded-b-none group-has-[#plex-tab-music:checked]/tabs:border-x-zinc-700 group-has-[#plex-tab-music:checked]/tabs:border-b-zinc-900 group-has-[#plex-tab-music:checked]/tabs:border-t-entity group-has-[#plex-tab-music:checked]/tabs:bg-zinc-800',
    focusLabelClass:
      'group-has-[#plex-tab-music:focus-visible]/tabs:ring-2 group-has-[#plex-tab-music:focus-visible]/tabs:ring-blue-400',
    activePanelClass: 'group-has-[#plex-tab-music:checked]/tabs:block',
  },
];

// The statistics block of the Plex page, placed before the sync section: an overview by entity, then one tab per
// entity (Movies and TV episodes when movies are enabled, Music when music is enabled). Like the dashboard, the tab
// choice is CSS only: a hidden radio per tab picks the visible panel, so every panel is in the HTML.
export function PlexStatisticsSection(handle: Handle<{ statistics: PlexStatistics }>) {
  return () => {
    const { movies, tv, music } = handle.props.statistics;
    const byEntity = [
      ...(movies ? [{ name: 'Movies', value: movies.coverage.onPlex }] : []),
      ...(tv ? [{ name: 'TV series', value: tv.coverage.onPlex }] : []),
      ...(music ? [{ name: 'Albums', value: music.coverage.onPlex }] : []),
    ];
    const onPlex = {
      movies: (movies?.coverage.onPlex ?? 0) + (tv?.coverage.onPlex ?? 0),
      music: music?.coverage.onPlex ?? 0,
    };
    const tabs = PLEX_TABS.filter((tab) => (tab.id === 'movies' ? movies || tv : music));
    const panel = (id: PlexTabConfig['id']) =>
      id === 'movies' ? (
        <div class="space-y-8">
          {movies ? <KindSection stats={movies} label="Movies" unit="movies" /> : null}
          {tv ? (
            <div class={movies ? 'border-t border-zinc-700 pt-8' : ''}>
              <KindSection stats={tv} label="TV series" unit="episodes" />
            </div>
          ) : null}
        </div>
      ) : music ? (
        <MusicSection stats={music} />
      ) : null;
    return (
      <section aria-labelledby="plex-page-heading" class="space-y-4" data-testid="plex-statistics">
        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <ChartCard title="On Plex by entity">
            <BarChart data={byEntity} color={CHART_HEX.movies} />
          </ChartCard>
        </div>
        {tabs.length > 0 ? (
          <fieldset class="group/tabs min-w-0">
            <legend class="sr-only">Plex entity</legend>
            {tabs.map((tab, index) => (
              <input
                type="radio"
                name="plex-tab"
                id={tab.inputId}
                aria-controls={`plex-panel-${tab.id}`}
                class="sr-only"
                checked={index === 0}
              />
            ))}
            <div class="mb-1 grid gap-4 sm:grid-cols-2">
              {tabs.map((tab) => (
                <label
                  for={tab.inputId}
                  id={`plex-label-${tab.id}`}
                  class={`${tab.entityClass} ${tab.activeLabelClass} ${tab.focusLabelClass} block w-full cursor-pointer rounded-sm border border-gray-500 bg-transparent p-4 text-left text-slate-300 transition-colors hover:bg-gray-800`}
                >
                  <div class="flex items-center gap-2">
                    <Icon icon={tab.icon} size={16} class="text-entity" />
                    <span class="font-semibold text-white">{tab.label}</span>
                  </div>
                  <div class="mt-1 text-xs text-zinc-400">{num(onPlex[tab.id])} on Plex</div>
                </label>
              ))}
            </div>
            {tabs.map((tab) => (
              <div
                id={`plex-panel-${tab.id}`}
                role="region"
                aria-labelledby={`plex-label-${tab.id}`}
                class={`${tab.entityClass} ${tab.activePanelClass} hidden rounded-lg rounded-tl-none rounded-tr-none border border-zinc-700 p-6`}
              >
                {panel(tab.id)}
              </div>
            ))}
          </fieldset>
        ) : null}
      </section>
    );
  };
}
