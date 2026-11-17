import type { Handle } from 'remix/component';

import { listHref } from '../../actions/entity/list-params.ts';
import type { PlexMusicStatistics } from '../../models.ts';
import { routes } from '../../routes.ts';
import { Album, Hash, Link2Off, Tv } from '../icons.ts';
import { STAT_CARD_COLOR } from './config.ts';
import { StatCard } from './stat-card.tsx';

interface PlexMusicSectionProps {
  plex: PlexMusicStatistics;
}

const num = (value: number): string => value.toLocaleString('en-US');

// The Plex part of the Music panel. The dashboard controller passes the music statistics only when Plex is enabled for
// music, so a gate that is off renders nothing here. An empty collection on Plex still shows the cards, with zeros.
export function PlexMusicSection(handle: Handle<PlexMusicSectionProps>) {
  return () => {
    const { plex } = handle.props;
    const { onPlex, notOnPlex } = plex.coverage;
    const notOnPlexHref = listHref(routes.entityLists.musics.href(), { plex: 'notOnPlex' });

    return (
      <section aria-labelledby="dashboard-plex-music-heading" class="space-y-4 border-t border-zinc-700 pt-4">
        <h3 id="dashboard-plex-music-heading" class="text-base font-semibold text-white">
          Plex
        </h3>
        <div class="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <StatCard title="On Plex" value={num(onPlex)} icon={Tv} colorClass={STAT_CARD_COLOR} />
          <StatCard title="Not on Plex" value={num(notOnPlex)} icon={Link2Off} colorClass={STAT_CARD_COLOR} />
          <StatCard title="Total plays" value={num(plex.totalPlays)} icon={Hash} colorClass={STAT_CARD_COLOR} />
          <StatCard title="Never played" value={num(plex.neverPlayed)} icon={Album} colorClass={STAT_CARD_COLOR} />
        </div>
        <a href={notOnPlexHref} data-plex-music-not-on-plex="" class="text-sm">
          Albums not on Plex
        </a>
      </section>
    );
  };
}
