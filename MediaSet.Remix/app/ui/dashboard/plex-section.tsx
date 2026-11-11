import type { Handle } from 'remix/component';

import type { PlexStats } from '../../api/stats-data.ts';
import { listHref } from '../../actions/entity/list-params.ts';
import { routes } from '../../routes.ts';
import { Check, Files, Link2Off, Tv } from '../icons.ts';
import { ChartCard } from './chart-card.tsx';
import { PLEX_RESOLUTION_COLORS, STAT_CARD_COLOR } from './config.ts';
import { PieChart } from './pie-chart.tsx';
import { StatCard } from './stat-card.tsx';
import { Icon } from '../icon.tsx';

interface PlexSectionProps {
  plex: PlexStats;
}

const num = (value: number): string => value.toLocaleString('en-US');

// "unknown" is the API's name for an on-Plex movie with no resolution.
function resolutionLabel(name: string): string {
  return name === 'unknown' ? 'Unknown' : name;
}

// The Plex part of the Movies panel. The dashboard controller passes `plexStats` only when the request's Plex gate is
// on, so a gate that is off renders nothing here. An empty collection on Plex still shows the cards, with zeros.
export function PlexSection(handle: Handle<PlexSectionProps>) {
  return () => {
    const { plex } = handle.props;
    const resolutions = Object.entries(plex.resolutionBreakdown ?? {})
      .map(([name, value]) => ({ name: resolutionLabel(name), value }))
      .filter((slice) => slice.value > 0);
    const notOnPlexHref = listHref(routes.entityLists.movies.href(), { plex: 'notOnPlex' });

    return (
      <section aria-labelledby="dashboard-plex-heading" class="space-y-4 border-t border-zinc-700 pt-4">
        <h3 id="dashboard-plex-heading" class="text-base font-semibold text-white">
          Plex
        </h3>
        <div class="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <StatCard title="On Plex" value={num(plex.onPlex)} icon={Tv} colorClass={STAT_CARD_COLOR} />
          <StatCard title="Not on Plex" value={num(plex.notOnPlex)} icon={Link2Off} colorClass={STAT_CARD_COLOR} />
          <StatCard title="Watched" value={num(plex.watched)} icon={Check} colorClass={STAT_CARD_COLOR} />
          <StatCard title="Unwatched" value={num(plex.unwatched)} icon={Files} colorClass={STAT_CARD_COLOR} />
        </div>
        <div class="grid grid-cols-2 gap-4">
          {resolutions.length > 0 && (
            <ChartCard title="Plex Resolution">
              <PieChart title="Plex Resolution" data={resolutions} colors={PLEX_RESOLUTION_COLORS} size={140} />
            </ChartCard>
          )}
          <a
            href={notOnPlexHref}
            data-plex-needs-linking=""
            class="block rounded-lg border border-zinc-700 no-underline bg-zinc-800/50 p-6 transition-colors hover:bg-zinc-800 focus-visible:ring-2 focus-visible:ring-blue-400 focus-visible:outline-none"
          >
            <p class="text-sm font-medium text-zinc-400">Needs linking</p>
            <p class="mt-2 flex items-center gap-2 text-3xl font-bold text-white">
              <Icon icon={Link2Off} class="text-entity" />
              {num(plex.needsLinking)}
            </p>
            <p class="mt-2 text-xs text-zinc-400">Movies with no working Plex link. View movies not on Plex.</p>
          </a>
        </div>
      </section>
    );
  };
}
