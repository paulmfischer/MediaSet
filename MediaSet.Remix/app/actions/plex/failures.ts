import { PlexActionError, type PlexFailure } from '../../api/plex-data.ts';

/** The Plex actions that can fail with the shared messages. */
export type PlexAction = 'refresh' | 'link' | 'unlink' | 'search' | 'sync';

export interface PlexFailurePage {
  status: number;
  heading: string;
  message: string;
}

const ACTION_VERB: Record<PlexAction, string> = {
  refresh: 'refreshing from Plex',
  link: 'linking to Plex',
  unlink: 'removing the Plex link',
  search: 'searching Plex',
  sync: 'starting the Plex sync',
};

const RETRY: Record<PlexAction, string> = {
  refresh: 'refresh again',
  link: 'link again',
  unlink: 'unlink again',
  search: 'search again',
  sync: 'start the sync again',
};

const OTHER_HEADING: Record<PlexAction, string> = {
  refresh: "Couldn't refresh from Plex",
  link: "Couldn't link to Plex",
  unlink: "Couldn't unlink from Plex",
  search: "Couldn't search Plex",
  sync: "Couldn't start the Plex sync",
};

/** The heading, message and status of the entity error page for a failed Plex action. */
export function plexFailurePage(error: unknown, action: PlexAction): PlexFailurePage {
  const failure: PlexFailure = error instanceof PlexActionError ? error.failure : 'other';
  switch (failure) {
    case 'unreachable':
      return {
        status: 502,
        heading: "Couldn't reach Plex",
        message: 'MediaSet could not reach your Plex server. Check that it is running and try again.',
      };
    case 'syncInProgress':
      return {
        status: 409,
        heading: 'A Plex sync is running',
        message: `A Plex sync is already in progress. Wait for it to finish, then ${RETRY[action]}.`,
      };
    case 'itemMissing':
      return {
        status: 404,
        heading: 'Item missing on Plex',
        message:
          action === 'link'
            ? 'Plex no longer has the item you picked. Search again and pick another result.'
            : 'Plex no longer has this item. It may have been removed from your library.',
      };
    case 'movieMissing':
      return { status: 404, heading: 'Movie not found', message: "We couldn't find that movie." };
    case 'notLinked':
      return {
        status: action === 'unlink' ? 404 : 400,
        heading: 'Not linked to Plex',
        message:
          action === 'unlink'
            ? 'This movie is not linked to Plex, so there is nothing to unlink.'
            : 'This movie is not linked to a Plex item, so there is nothing to refresh.',
      };
    default:
      return {
        status: 500,
        heading: OTHER_HEADING[action],
        message: `Something went wrong while ${ACTION_VERB[action]}. Please try again.`,
      };
  }
}
