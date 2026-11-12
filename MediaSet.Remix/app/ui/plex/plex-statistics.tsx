import type { Handle } from 'remix/component';

import type { PlexCount, PlexKindStatistics, PlexStatistics } from '../../models.ts';
import { formatBytes, formatDuration, statisticLabel } from '../../utils/plex-statistics.ts';
import { BarChart } from '../dashboard/bar-chart.tsx';
import { ChartCard } from '../dashboard/chart-card.tsx';
import { PLEX_RESOLUTION_COLORS, PLEX_STATISTICS_COLORS, STAT_CARD_COLOR } from '../dashboard/config.ts';
import { PieChart } from '../dashboard/pie-chart.tsx';
import { StatCard } from '../dashboard/stat-card.tsx';
import { AlertTriangle, Clapperboard, HardDrive, Layers, Link2Off, Tv } from '../icons.ts';

const num = (value: number): string => value.toLocaleString('en-US');
const PLEX_BAR_COLOR = PLEX_RESOLUTION_COLORS[0];

function slices(counts: PlexCount[], upper = false): Array<{ name: string; value: number }> {
  return counts.filter((c) => c.count > 0).map((c) => ({ name: statisticLabel(c.name, upper), value: c.count }));
}

function PieCard(handle: Handle<{ title: string; counts: PlexCount[]; upper?: boolean }>) {
  return () => {
    const { title, counts, upper } = handle.props;
    const data = slices(counts, upper);
    if (data.length === 0) return null;
    return (
      <ChartCard title={title}>
        <PieChart title={title} data={data} colors={PLEX_STATISTICS_COLORS} size={140} />
      </ChartCard>
    );
  };
}

function BarCard(handle: Handle<{ title: string; counts: PlexCount[]; upper?: boolean }>) {
  return () => {
    const { title, counts, upper } = handle.props;
    const data = slices(counts, upper);
    if (data.length === 0) return null;
    return (
      <ChartCard title={title}>
        <BarChart data={data} color={PLEX_BAR_COLOR} />
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
      <section
        aria-labelledby={headingId}
        class="space-y-4 border-t border-zinc-700 pt-4"
        data-testid="plex-stats-kind"
        data-kind={stats.kind}
      >
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
              <PieCard title="Resolution" counts={stats.resolution} upper />
              <PieCard title="Video codec" counts={stats.videoCodec} upper />
              <PieCard title="HDR vs SDR" counts={stats.hdr} />
              <PieCard title="Subtitles" counts={stats.subtitles} />
              <PieCard title="Watched" counts={stats.watched} />
              <BarCard title="Audio codec" counts={stats.audioCodec} upper />
              <BarCard title="Audio channel layout" counts={stats.audioChannels} />
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
          <ChartCard title="Most watched">
            <BarChart
              data={stats.mostWatched.map((item) => ({ name: item.title, value: item.viewCount }))}
              color={PLEX_BAR_COLOR}
            />
          </ChartCard>
        ) : null}
      </section>
    );
  };
}

// The statistics block of the Plex page, placed before the sync section: an overview by entity, then one section for
// Movies and one for TV. TV breakdowns count episodes.
export function PlexStatisticsSection(handle: Handle<{ statistics: PlexStatistics }>) {
  return () => {
    const { movies, tv } = handle.props.statistics;
    const byEntity = [
      { name: 'Movies', value: movies.coverage.onPlex },
      { name: 'TV series', value: tv.coverage.onPlex },
    ];
    return (
      <section aria-labelledby="plex-page-heading" class="space-y-4" data-testid="plex-statistics">
        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <ChartCard title="On Plex by entity">
            <BarChart data={byEntity} color={PLEX_BAR_COLOR} />
          </ChartCard>
        </div>
        <KindSection stats={movies} label="Movies" unit="movies" />
        <KindSection stats={tv} label="TV series" unit="episodes" />
      </section>
    );
  };
}
