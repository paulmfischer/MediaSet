import type { Handle } from 'remix/component';

import type { NameCount } from '../../api/stats-data.ts';
import type { DashboardStats } from './stats.ts';
import { Album, BookOpen, Clapperboard, Disc, FileText, Hash, Layers, Monitor, Tag, Tv, Users } from '../icons.ts';
import { BarChart } from './bar-chart.tsx';
import { ChartCard } from './chart-card.tsx';
import { CHART_HEX, FORMAT_PALETTES, MOVIE_TYPE_COLORS, STAT_CARD_COLOR } from './config.ts';
import { PieChart } from './pie-chart.tsx';
import { PlexSection } from './plex-section.tsx';
import { StatCard } from './stat-card.tsx';

interface PanelProps {
  stats: DashboardStats;
}

const num = (value: number): string => value.toLocaleString('en-US');

function breakdown(source: Record<string, number> | undefined): { name: string; value: number }[] {
  return source ? Object.entries(source).map(([name, value]) => ({ name, value })) : [];
}

function nameCounts(items: NameCount[] | undefined): { name: string; value: number }[] {
  return items ? items.map(({ name, count }) => ({ name, value: count })) : [];
}

export function BooksPanel(handle: Handle<PanelProps>) {
  return () => {
    const s = handle.props.stats.bookStats;
    const formats = breakdown(s.formatBreakdown);
    const pages = breakdown(s.pageCountBuckets);
    const authors = nameCounts(s.topAuthors);
    const decades = breakdown(s.decadeBreakdown);
    const genres = nameCounts(s.topGenres);
    return (
      <div class="space-y-4 p-6">
        <div class="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <StatCard title="Total Pages" value={num(s.totalPages)} icon={BookOpen} colorClass={STAT_CARD_COLOR} />
          <StatCard title="Avg Pages" value={num(s.avgPages)} icon={Hash} colorClass={STAT_CARD_COLOR} />
          <StatCard title="Unique Authors" value={s.uniqueAuthors} icon={Users} colorClass={STAT_CARD_COLOR} />
          <StatCard title="Formats" value={s.totalFormats} icon={FileText} colorClass={STAT_CARD_COLOR} />
        </div>
        <div class="grid grid-cols-2 gap-4">
          {formats.length > 1 && (
            <ChartCard title="Format Breakdown">
              <PieChart title="Format Breakdown" data={formats} colors={FORMAT_PALETTES.books} size={140} />
            </ChartCard>
          )}
          {pages.length > 0 && (
            <ChartCard title="Page Count Distribution">
              <BarChart data={pages} color={CHART_HEX.books} orientation="vertical" />
            </ChartCard>
          )}
          {authors.length > 0 && (
            <ChartCard title="Top Authors">
              <BarChart data={authors} color={CHART_HEX.books} orientation="horizontal" />
            </ChartCard>
          )}
          {decades.length > 0 && (
            <ChartCard title="Publication Decade">
              <BarChart data={decades} color={CHART_HEX.books} orientation="vertical" />
            </ChartCard>
          )}
          {genres.length > 0 && (
            <ChartCard title="Top Genres">
              <BarChart data={genres} color={CHART_HEX.books} orientation="horizontal" />
            </ChartCard>
          )}
        </div>
      </div>
    );
  };
}

export function MoviesPanel(handle: Handle<PanelProps>) {
  return () => {
    const s = handle.props.stats.movieStats;
    const movieCount = s.total - s.totalTvSeries;
    const types = [
      { name: 'Movies', value: movieCount },
      { name: 'TV Series', value: s.totalTvSeries },
    ].filter((d) => d.value > 0);
    const formats = breakdown(s.formatBreakdown);
    const studios = nameCounts(s.topStudios);
    const decades = breakdown(s.decadeBreakdown);
    const genres = breakdown(s.genreBreakdown);
    return (
      <div class="space-y-4 p-6">
        <div class="grid grid-cols-2 gap-3 sm:grid-cols-3">
          <StatCard title="Movies" value={movieCount} icon={Clapperboard} colorClass={STAT_CARD_COLOR} />
          <StatCard title="TV Series" value={s.totalTvSeries} icon={Tv} colorClass={STAT_CARD_COLOR} />
          <StatCard title="Formats" value={s.totalFormats} icon={Disc} colorClass={STAT_CARD_COLOR} />
        </div>
        <div class="grid grid-cols-2 gap-4">
          {types.length > 1 && (
            <ChartCard title="Movies vs TV Series">
              <PieChart title="Movies vs TV Series" data={types} colors={MOVIE_TYPE_COLORS} size={140} />
            </ChartCard>
          )}
          {formats.length > 1 && (
            <ChartCard title="Format Breakdown">
              <PieChart title="Format Breakdown" data={formats} colors={FORMAT_PALETTES.movies} size={140} />
            </ChartCard>
          )}
          {studios.length > 0 && (
            <ChartCard title="Top Studios">
              <BarChart data={studios} color={CHART_HEX.movies} orientation="horizontal" />
            </ChartCard>
          )}
          {decades.length > 0 && (
            <ChartCard title="Release Decade">
              <BarChart data={decades} color={CHART_HEX.movies} orientation="vertical" />
            </ChartCard>
          )}
          {genres.length > 0 && (
            <ChartCard title="Top Genres">
              <BarChart data={genres} color={CHART_HEX.movies} orientation="horizontal" />
            </ChartCard>
          )}
        </div>
        {handle.props.stats.plexStats && <PlexSection plex={handle.props.stats.plexStats} />}
      </div>
    );
  };
}

export function GamesPanel(handle: Handle<PanelProps>) {
  return () => {
    const s = handle.props.stats.gameStats;
    const platforms = breakdown(s.platformBreakdown);
    const formats = breakdown(s.formatBreakdown);
    const publishers = nameCounts(s.topPublishers);
    const developers = nameCounts(s.topDevelopers);
    const decades = breakdown(s.decadeBreakdown);
    const genres = breakdown(s.genreBreakdown);
    return (
      <div class="space-y-4 p-6">
        <div class="grid grid-cols-2 gap-3">
          <StatCard title="Platforms" value={s.totalPlatforms} icon={Monitor} colorClass={STAT_CARD_COLOR} />
          <StatCard title="Formats" value={s.totalFormats} icon={Disc} colorClass={STAT_CARD_COLOR} />
        </div>
        <div class="grid grid-cols-2 gap-4">
          {platforms.length > 1 && (
            <ChartCard title="Platform Breakdown">
              <PieChart title="Platform Breakdown" data={platforms} colors={FORMAT_PALETTES.games} size={140} />
            </ChartCard>
          )}
          {formats.length > 1 && (
            <ChartCard title="Format Breakdown">
              <PieChart title="Format Breakdown" data={formats} colors={FORMAT_PALETTES.games} size={140} />
            </ChartCard>
          )}
          {publishers.length > 0 && (
            <ChartCard title="Top Publishers">
              <BarChart data={publishers} color={CHART_HEX.games} orientation="horizontal" />
            </ChartCard>
          )}
          {developers.length > 0 && (
            <ChartCard title="Top Developers">
              <BarChart data={developers} color={CHART_HEX.games} orientation="horizontal" />
            </ChartCard>
          )}
          {decades.length > 0 && (
            <ChartCard title="Release Decade">
              <BarChart data={decades} color={CHART_HEX.games} orientation="vertical" />
            </ChartCard>
          )}
          {genres.length > 0 && (
            <ChartCard title="Top Genres">
              <BarChart data={genres} color={CHART_HEX.games} orientation="horizontal" />
            </ChartCard>
          )}
        </div>
      </div>
    );
  };
}

export function MusicsPanel(handle: Handle<PanelProps>) {
  return () => {
    const s = handle.props.stats.musicStats;
    const formats = breakdown(s.formatBreakdown);
    const artists = nameCounts(s.topArtists);
    const labels = nameCounts(s.topLabels);
    const decades = breakdown(s.decadeBreakdown);
    const genres = breakdown(s.genreBreakdown);
    return (
      <div class="space-y-4 p-6">
        <div class="grid grid-cols-2 gap-3 sm:grid-cols-3">
          <StatCard title="Total Tracks" value={num(s.totalTracks)} icon={Album} colorClass={STAT_CARD_COLOR} />
          <StatCard title="Avg Tracks" value={s.avgTracks} icon={Hash} colorClass={STAT_CARD_COLOR} />
          <StatCard title="Unique Artists" value={s.uniqueArtists} icon={Users} colorClass={STAT_CARD_COLOR} />
          <StatCard title="Unique Labels" value={s.uniqueLabels} icon={Tag} colorClass={STAT_CARD_COLOR} />
          <StatCard title="Total Discs" value={s.totalDiscs} icon={Layers} colorClass={STAT_CARD_COLOR} />
          <StatCard title="Formats" value={s.totalFormats} icon={Disc} colorClass={STAT_CARD_COLOR} />
        </div>
        <div class="grid grid-cols-2 gap-4">
          {formats.length > 1 && (
            <ChartCard title="Format Breakdown">
              <PieChart title="Format Breakdown" data={formats} colors={FORMAT_PALETTES.musics} size={140} />
            </ChartCard>
          )}
          {artists.length > 0 && (
            <ChartCard title="Top Artists">
              <BarChart data={artists} color={CHART_HEX.musics} orientation="horizontal" />
            </ChartCard>
          )}
          {labels.length > 0 && (
            <ChartCard title="Top Labels">
              <BarChart data={labels} color={CHART_HEX.musics} orientation="horizontal" />
            </ChartCard>
          )}
          {decades.length > 0 && (
            <ChartCard title="Release Decade">
              <BarChart data={decades} color={CHART_HEX.musics} orientation="vertical" />
            </ChartCard>
          )}
          {genres.length > 0 && (
            <ChartCard title="Top Genres">
              <BarChart data={genres} color={CHART_HEX.musics} orientation="horizontal" />
            </ChartCard>
          )}
        </div>
      </div>
    );
  };
}
