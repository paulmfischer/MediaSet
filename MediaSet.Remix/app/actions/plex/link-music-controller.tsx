import * as s from 'remix/data-schema';
import * as f from 'remix/data-schema/form-data';
import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { getEntity } from '../../api/entity-data.ts';
import { linkPlexItem, PlexActionError, searchPlex } from '../../api/plex-data.ts';
import { Entity, type MusicEntity, type PlexSearchResult } from '../../models.ts';
import type { AppContext } from '../../router.ts';
import { isPlexMusicEnabledForRequest } from '../../utils/plex-nav.server.ts';
import { routes } from '../../routes.ts';
import { Document } from '../../ui/document.tsx';
import { PlexLinkPage } from '../../ui/plex/link-page.tsx';
import { createServerLogger } from '../../utils/serverLogger.ts';
import { text } from '../forms/form-schema.ts';
import { renderError } from '../render-error.tsx';
import { plexFailurePage } from './failures.ts';

const serverLogger = createServerLogger('app/actions/plex/link-music-controller');

const MIN_QUERY_LENGTH = 2;

const linkForm = f.object({ plexItemId: f.field(text()) });

const asText = (value: FormDataEntryValue | null) => (typeof value === 'string' ? value : '');

function failurePage(context: AppContext, id: string, error: unknown, action: 'link' | 'search') {
  const failure = plexFailurePage(error, action, 'album');
  if (!(error instanceof PlexActionError))
    serverLogger.error('Plex music link flow failed', error, { operation: 'plexLink', id });
  return renderError(context, failure.status, error, {
    heading: failure.heading,
    message: failure.message,
    backHref: routes.entityDetail.musics.href({ id }),
    backLabel: 'Back to Album',
  });
}

async function loadMusic(context: AppContext, id: string): Promise<MusicEntity | Response> {
  try {
    return await getEntity<MusicEntity>(Entity.Musics, id);
  } catch (error) {
    const missing = error instanceof Response && error.status === 404;
    if (!missing) serverLogger.error('Failed to load album for Plex link', error, { operation: 'plexLink', id });
    return renderError(context, missing ? 404 : 500, error, {
      heading: missing ? 'Music not found' : "Couldn't load this album",
      message: missing ? "We couldn't find that album." : 'Something went wrong while loading it. Please try again.',
      backHref: routes.entityLists.musics.href(),
      backLabel: 'Back to Music',
    });
  }
}

function renderFlow(
  context: AppContext,
  id: string,
  music: MusicEntity,
  state: { fragment: boolean; query: string; results?: PlexSearchResult[] | undefined }
) {
  const page = (
    <PlexLinkPage
      fragment={state.fragment}
      movieTitle={music.title ?? 'this album'}
      relink={Boolean(music.plex)}
      isTv={false}
      kind="album"
      step="search"
      searchHref={routes.entityPlexLink.musics.index.href({ id })}
      actionHref={routes.entityPlexLink.musics.action.href({ id })}
      cancelHref={routes.entityDetail.musics.href({ id })}
      query={state.query}
      results={state.results}
    />
  );
  // The dialog asks for the bare flow; the page wraps it in the site layout.
  if (state.fragment) return context.render(page);
  return context.render(
    <Document path={context.url.pathname} title={`${music.plex ? 'Relink' : 'Link'} to Plex | MediaSet`}>
      {page}
    </Document>
  );
}

// The first "Link to Plex" of an unlinked album (a POST, so nothing changes on a GET): searches by the title and, when
// exactly one album comes back, links it at once. Anything else goes to the normal search flow, which shows the results.
// Relink never comes here, so a wrong match can be corrected.
async function autoLink(context: AppContext, id: string, music: MusicEntity) {
  const fragment = context.url.searchParams.get('fragment') === '1';
  const title = (music.title ?? '').trim();
  const searchPage = routes.entityPlexLink.musics.index.href({ id });
  if (music.plex || title.length < MIN_QUERY_LENGTH) {
    // The dialog gets the search step itself (never a redirect, which it would read as a finished link)
    return fragment ? index(context) : redirect(searchPage, 303);
  }

  let results: PlexSearchResult[];
  try {
    results = await searchPlex(title, 'music');
  } catch (error) {
    return failurePage(context, id, error, 'search');
  }
  const only = results.length === 1 ? results[0] : undefined;
  if (!only) {
    // The dialog shows the results in place; without JavaScript the browser goes to the search page
    return fragment ? renderFlow(context, id, music, { fragment, query: title, results }) : redirect(searchPage, 303);
  }
  try {
    await linkPlexItem(id, { plexItemId: only.plexItemId }, 'music');
  } catch (error) {
    return failurePage(context, id, error, 'link');
  }
  return redirect(routes.entityDetail.musics.href({ id }), 303);
}

// Plex is contacted only here, when the flow is opened or searched, never when the detail page loads.
async function index(context: AppContext) {
  const { id } = context.params as { id: string };
  if (!(await isPlexMusicEnabledForRequest())) return renderError(context, 404);
  const loaded = await loadMusic(context, id);
  if (loaded instanceof Response) return loaded;
  const music = loaded;

  const params = context.url.searchParams;
  const fragment = params.get('fragment') === '1';
  const query = (params.get('query') ?? music.title ?? '').trim();

  let results: PlexSearchResult[] | undefined;
  if (query.length >= MIN_QUERY_LENGTH) {
    try {
      results = await searchPlex(query, 'music');
    } catch (error) {
      return failurePage(context, id, error, 'search');
    }
  }
  return renderFlow(context, id, music, { fragment, query, results });
}

async function action(context: AppContext) {
  const { id } = context.params as { id: string };
  if (!(await isPlexMusicEnabledForRequest())) return renderError(context, 404);
  const loaded = await loadMusic(context, id);
  if (loaded instanceof Response) return loaded;
  const music = loaded;

  const parsed = s.parseSafe(linkForm, context.formData);
  const plexItemId = parsed.success ? parsed.value.plexItemId.trim() : '';
  if (!plexItemId && asText(context.formData.get('intent')) === 'auto') return autoLink(context, id, music);
  if (!plexItemId) {
    return renderError(context, 400, undefined, {
      heading: 'Choose a Plex album',
      message: 'No Plex album was chosen. Search again and pick a result.',
      backHref: routes.entityPlexLink.musics.index.href({ id }),
      backLabel: 'Back to the search',
    });
  }

  try {
    await linkPlexItem(id, { plexItemId }, 'music');
  } catch (error) {
    return failurePage(context, id, error, 'link');
  }
  return redirect(routes.entityDetail.musics.href({ id }), 303);
}

export default createController(routes.entityPlexLink.musics, { actions: { index, action } });
