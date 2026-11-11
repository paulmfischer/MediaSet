import type { Handle } from 'remix/component';

import type { PlexEpisode, PlexItem, PlexRollup, PlexSeason } from '../../models.ts';
import {
  describeLanguages,
  describeProgress,
  describeRollupResolution,
  episodeCode,
  formatSize,
  seasonLabel,
} from '../../utils/plex.ts';
import { Icon } from '../icon.tsx';
import { Check } from '../icons.ts';

interface PlexSeasonListProps {
  plex: PlexItem;
}

// The rollup facts of one season, shown when it is expanded.
function rollupFacts(rollup: PlexRollup): string[] {
  const audio = describeLanguages(rollup.audioLanguages, rollup.audioLanguagesDiffer);
  const subtitles = describeLanguages(rollup.subtitleLanguages, rollup.subtitleLanguagesDiffer);
  return [describeRollupResolution(rollup), audio && `Audio: ${audio}`, subtitles && `Subtitles: ${subtitles}`].filter(
    Boolean
  );
}

function EpisodeRow(handle: Handle<{ episode: PlexEpisode }>) {
  return () => {
    const { episode } = handle.props;
    const quality = episode.resolution ? `${episode.resolution}${episode.hdr ? ' HDR' : ''}` : '';
    return (
      <li
        class={`flex flex-wrap items-center gap-x-3 text-sm ${episode.onPlex ? '' : 'opacity-60'}`}
        data-plex-episode={episodeCode(episode.seasonNumber, episode.episodeNumber)}
        data-plex-episode-state={episode.onPlex ? 'present' : 'missing'}
      >
        <span class="w-16 shrink-0 dark:text-slate-300">
          {episodeCode(episode.seasonNumber, episode.episodeNumber)}
        </span>
        <span class={`min-w-0 flex-1 wrap-break-word ${episode.onPlex ? '' : 'line-through'}`}>{episode.title}</span>
        {episode.onPlex ? null : (
          <span class="text-amber-400" data-testid="plex-episode-missing">
            Missing from Plex
          </span>
        )}
        <span class="w-4 shrink-0">
          {episode.watched ? (
            <span role="img" aria-label="Watched" title="Watched">
              <Icon icon={Check} size={16} />
            </span>
          ) : null}
        </span>
        <span class="w-20 text-right">{quality}</span>
        <span class="w-20 text-right">{formatSize(episode.sizeBytes)}</span>
      </li>
    );
  };
}

function SeasonItem(handle: Handle<{ season: PlexSeason; episodes: PlexEpisode[] }>) {
  return () => {
    const { season, episodes } = handle.props;
    const rollup = season.rollup;
    const summary = rollup ? describeProgress(rollup) : `${season.episodeCount} episodes`;
    const facts = rollup ? rollupFacts(rollup) : [];
    return (
      <details class="rounded border border-slate-600 px-3 py-2" data-plex-season={season.index}>
        <summary class="cursor-pointer text-sm">
          <span class="font-semibold">{seasonLabel(season.index)}</span>
          <span class="dark:text-slate-300"> - {summary}</span>
        </summary>
        <div class="mt-2 flex flex-col gap-2">
          {facts.length > 0 ? (
            <p class="text-xs dark:text-slate-300" data-testid="plex-season-rollup">
              {facts.join(' | ')}
            </p>
          ) : null}
          {episodes.length > 0 ? (
            <ul class="flex flex-col gap-1">
              {episodes.map((episode) => (
                <EpisodeRow
                  key={episode.plexEpisodeId || `${episode.seasonNumber}-${episode.episodeNumber}`}
                  episode={episode}
                />
              ))}
            </ul>
          ) : (
            <p class="text-xs dark:text-slate-300">No episodes stored.</p>
          )}
        </div>
      </details>
    );
  };
}

// Episodes whose season is not in the seasons list (not expected from the API) still show.
function OtherEpisodes(handle: Handle<{ episodes: PlexEpisode[] }>) {
  return () => (
    <details class="rounded border border-slate-600 px-3 py-2" data-plex-season="other">
      <summary class="cursor-pointer text-sm font-semibold">Other episodes</summary>
      <ul class="mt-2 flex flex-col gap-1">
        {handle.props.episodes.map((episode) => (
          <EpisodeRow key={`${episode.seasonNumber}-${episode.episodeNumber}`} episode={episode} />
        ))}
      </ul>
    </details>
  );
}

/** The native collapsible per-season list of a TV entry; works without JavaScript. */
export function PlexSeasonList(handle: Handle<PlexSeasonListProps>) {
  return () => {
    const { plex } = handle.props;
    const seasons = plex.seasons ?? [];
    const episodes = plex.episodes ?? [];
    const known = new Set(seasons.map((season) => season.index));
    const others = episodes.filter((episode) => !known.has(episode.seasonNumber));
    if (seasons.length === 0 && others.length === 0) return null;
    return (
      <div class="flex flex-col gap-2" data-testid="plex-seasons">
        {seasons.map((season) => (
          <SeasonItem
            key={season.index}
            season={season}
            episodes={episodes.filter((e) => e.seasonNumber === season.index)}
          />
        ))}
        {others.length > 0 ? <OtherEpisodes episodes={others} /> : null}
      </div>
    );
  };
}
