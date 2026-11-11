import * as s from 'remix/data-schema';
import * as f from 'remix/data-schema/form-data';
import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { getEntity } from '../../api/entity-data.ts';
import { getPlexScope, linkPlexItem, PlexActionError, searchPlex, type PlexLinkScope } from '../../api/plex-data.ts';
import { Entity, type MovieEntity, type PlexScope, type PlexSearchResult } from '../../models.ts';
import type { AppContext } from '../../router.ts';
import { isPlexEnabledForRequest } from '../../utils/plex-nav.server.ts';
import { routes } from '../../routes.ts';
import { Document } from '../../ui/document.tsx';
import { PlexLinkPage, resultLabel, type PlexScopeValues } from '../../ui/plex/link-page.tsx';
import { serverLogger } from '../../utils/serverLogger.ts';
import { fieldErrors, text } from '../forms/form-schema.ts';
import { renderError } from '../render-error.tsx';
import { plexFailurePage } from './failures.ts';

const MIN_QUERY_LENGTH = 2;
const SEASON_ERROR = 'Enter a season from 0 to 99';

const linkForm = f.object({
  plexItemId: f.field(text()),
  plexTitle: f.field(text()),
  query: f.field(text()),
  scopeKind: f.field(text()),
  // Kept as text: only the fields the chosen scope uses are validated (see readScope)
  season: f.field(text()),
  from: f.field(text()),
  to: f.field(text()),
});

const FORM_FIELDS = ['plexItemId', 'plexTitle', 'query', 'scopeKind', 'season', 'from', 'to'] as const;

const asText = (value: FormDataEntryValue | null) => (typeof value === 'string' ? value : '');

/** The scope step's form values from a title-parsed scope; an unreadable title falls back to the complete series. */
export function scopeToValues(scope: PlexScope | undefined): PlexScopeValues {
  const from = scope?.from ?? undefined;
  const to = scope?.to ?? undefined;
  if (scope?.kind === 'season' && from != null) return { scopeKind: 'season', season: String(from), from: '', to: '' };
  if (scope?.kind === 'range' && from != null && to != null) {
    return { scopeKind: 'range', season: '', from: String(from), to: String(to) };
  }
  return { scopeKind: 'all', season: '', from: '', to: '' };
}

// A season number from 0 to 99, or the error to show for the field
function seasonNumber(value: string, blankMessage: string): { value: number } | { error: string } {
  if (value === '') return { error: blankMessage };
  return /^\d{1,2}$/.test(value) ? { value: Number(value) } : { error: SEASON_ERROR };
}

// Reads the submitted scope. Only the fields the chosen kind uses are checked, so a stale value left in
// another field never blocks the link. Errors are keyed by field name.
function readScope(
  kind: string,
  fields: { season: string; from: string; to: string }
): { scope: PlexLinkScope } | { errors: Record<string, string> } {
  if (kind === 'all') return { scope: { kind: 'all' } };
  if (kind === 'season') {
    const season = seasonNumber(fields.season, 'Enter the season number');
    return 'error' in season
      ? { errors: { season: season.error } }
      : { scope: { kind: 'season', season: season.value } };
  }
  if (kind === 'range') {
    const from = seasonNumber(fields.from, 'Enter the first season');
    const to = seasonNumber(fields.to, 'Enter the last season');
    if ('error' in from || 'error' in to) {
      return {
        errors: { ...('error' in from ? { from: from.error } : {}), ...('error' in to ? { to: to.error } : {}) },
      };
    }
    if (from.value > to.value) return { errors: { to: 'The last season must not be before the first' } };
    // A range of one season is a single season
    return from.value === to.value
      ? { scope: { kind: 'season', season: from.value } }
      : { scope: { kind: 'range', from: from.value, to: to.value } };
  }
  return { errors: { scopeKind: 'Choose which seasons this entry covers' } };
}

function failurePage(context: AppContext, id: string, error: unknown, action: 'link' | 'search') {
  const failure = plexFailurePage(error, action);
  if (!(error instanceof PlexActionError))
    serverLogger.error('Plex link flow failed', error, { operation: 'plexLink', id });
  return renderError(context, failure.status, error, {
    heading: failure.heading,
    message: failure.message,
    backHref: routes.entityDetail.movies.href({ id }),
    backLabel: 'Back to Movie',
  });
}

async function loadMovie(context: AppContext, id: string): Promise<MovieEntity | Response> {
  try {
    return await getEntity<MovieEntity>(Entity.Movies, id);
  } catch (error) {
    const missing = error instanceof Response && error.status === 404;
    if (!missing) serverLogger.error('Failed to load movie for Plex link', error, { operation: 'plexLink', id });
    return renderError(context, missing ? 404 : 500, error, {
      heading: missing ? 'Movie not found' : "Couldn't load this movie",
      message: missing ? "We couldn't find that movie." : 'Something went wrong while loading it. Please try again.',
      backHref: routes.entityLists.movies.href(),
      backLabel: 'Back to Movies',
    });
  }
}

interface RenderState {
  fragment?: boolean;
  step: 'search' | 'scope';
  query: string;
  results?: PlexSearchResult[] | undefined;
  selected?: { plexItemId: string; title: string };
  scope?: PlexScopeValues;
  errors?: Record<string, string>;
  status?: number;
}

function renderFlow(context: AppContext, id: string, movie: MovieEntity, state: RenderState) {
  const page = (
    <PlexLinkPage
      fragment={state.fragment ?? false}
      movieTitle={movie.title ?? 'this movie'}
      relink={Boolean(movie.plex)}
      isTv={Boolean(movie.isTvSeries)}
      step={state.step}
      searchHref={routes.entityPlexLink.movies.index.href({ id })}
      actionHref={routes.entityPlexLink.movies.action.href({ id })}
      cancelHref={routes.entityDetail.movies.href({ id })}
      query={state.query}
      results={state.results}
      selected={state.selected}
      scope={state.scope}
      errors={state.errors}
    />
  );
  const init = { status: state.status ?? 200 };
  // The dialog asks for the bare flow; the page wraps it in the site layout.
  if (state.fragment) return context.render(page, init);
  return context.render(
    <Document path={context.url.pathname} title={`${movie.plex ? 'Relink' : 'Link'} to Plex | MediaSet`}>
      {page}
    </Document>,
    init
  );
}

// A movie entry links to movies and a TV entry to shows; the other kind would be refused
async function searchEntryKind(movie: MovieEntity, query: string): Promise<PlexSearchResult[]> {
  const wanted = movie.isTvSeries ? 'show' : 'movie';
  return (await searchPlex(query)).filter((result) => (result.type || 'movie') === wanted);
}

// The first "Link to Plex" of an unlinked entry (a POST, so nothing changes on a GET): searches by the title and, when
// exactly one match of the right kind comes back, links it at once. Anything else goes to the normal search flow, which
// shows the results; a TV entry still chooses its seasons. Relink never comes here, so a wrong match can be corrected.
async function autoLink(context: AppContext, id: string, movie: MovieEntity) {
  const fragment = context.url.searchParams.get('fragment') === '1';
  const title = (movie.title ?? '').trim();
  const searchPage = routes.entityPlexLink.movies.index.href({ id });
  if (movie.plex || title.length < MIN_QUERY_LENGTH) return redirect(searchPage, 303);

  let results: PlexSearchResult[];
  try {
    results = await searchEntryKind(movie, title);
  } catch (error) {
    return failurePage(context, id, error, 'search');
  }
  const only = results.length === 1 ? results[0] : undefined;
  if (!only) {
    // The dialog shows the results in place; without JavaScript the browser goes to the search page
    return fragment
      ? renderFlow(context, id, movie, { fragment, step: 'search', query: title, results })
      : redirect(searchPage, 303);
  }
  if (movie.isTvSeries) {
    if (!fragment) {
      const params = new URLSearchParams({ plexItemId: only.plexItemId, plexTitle: resultLabel(only), query: title });
      return redirect(`${searchPage}?${params}`, 303);
    }
    return scopeStep(context, id, movie, {
      fragment,
      query: title,
      selected: { plexItemId: only.plexItemId, title: resultLabel(only) },
    });
  }
  try {
    await linkPlexItem(id, { plexItemId: only.plexItemId });
  } catch (error) {
    return failurePage(context, id, error, 'link');
  }
  return redirect(routes.entityDetail.movies.href({ id }), 303);
}

// The scope step for a picked show, prefilled from the entry's title.
async function scopeStep(
  context: AppContext,
  id: string,
  movie: MovieEntity,
  picked: { fragment: boolean; query: string; selected: { plexItemId: string; title: string } }
) {
  let scope: PlexScope | undefined;
  try {
    scope = await getPlexScope(id);
  } catch (error) {
    if (error instanceof PlexActionError && error.failure === 'movieMissing')
      return failurePage(context, id, error, 'link');
    // The title only prefills; without it the entry covers the complete series
  }
  return renderFlow(context, id, movie, { ...picked, step: 'scope', scope: scopeToValues(scope) });
}

// Plex is contacted only here, when the flow is opened or searched, never when the detail page loads.
async function index(context: AppContext) {
  const { id } = context.params as { id: string };
  if (!(await isPlexEnabledForRequest())) return renderError(context, 404);
  const loaded = await loadMovie(context, id);
  if (loaded instanceof Response) return loaded;
  const movie = loaded;

  const params = context.url.searchParams;
  const fragment = params.get('fragment') === '1';
  const query = (params.get('query') ?? movie.title ?? '').trim();
  const plexItemId = (params.get('plexItemId') ?? '').trim();

  if (plexItemId && movie.isTvSeries) {
    return scopeStep(context, id, movie, {
      fragment,
      query,
      selected: { plexItemId, title: (params.get('plexTitle') ?? '').trim() || plexItemId },
    });
  }

  let results: PlexSearchResult[] | undefined;
  if (query.length >= MIN_QUERY_LENGTH) {
    try {
      results = await searchEntryKind(movie, query);
    } catch (error) {
      return failurePage(context, id, error, 'search');
    }
  }
  return renderFlow(context, id, movie, { fragment, step: 'search', query, results });
}

async function action(context: AppContext) {
  const { id } = context.params as { id: string };
  if (!(await isPlexEnabledForRequest())) return renderError(context, 404);
  const loaded = await loadMovie(context, id);
  if (loaded instanceof Response) return loaded;
  const movie = loaded;

  const parsed = s.parseSafe(linkForm, context.formData);
  const submitted = Object.fromEntries(FORM_FIELDS.map((name) => [name, asText(context.formData.get(name))])) as Record<
    (typeof FORM_FIELDS)[number],
    string
  >;
  const plexItemId = submitted.plexItemId.trim();
  if (!plexItemId && asText(context.formData.get('intent')) === 'auto') return autoLink(context, id, movie);
  if (!plexItemId) {
    return renderError(context, 400, undefined, {
      heading: 'Choose a Plex item',
      message: 'No Plex item was chosen. Search again and pick a result.',
      backHref: routes.entityPlexLink.movies.index.href({ id }),
      backLabel: 'Back to the search',
    });
  }

  let request: { plexItemId: string; scope?: PlexLinkScope } = { plexItemId };
  if (movie.isTvSeries) {
    const scope = parsed.success ? readScope(parsed.value.scopeKind, parsed.value) : undefined;
    if (!parsed.success || !scope || 'errors' in scope) {
      return renderFlow(context, id, movie, {
        // The dialog posts with fragment=1 and shows the answer in place
        fragment: context.url.searchParams.get('fragment') === '1',
        step: 'scope',
        query: submitted.query.trim(),
        selected: { plexItemId, title: submitted.plexTitle.trim() || plexItemId },
        scope: { scopeKind: submitted.scopeKind, season: submitted.season, from: submitted.from, to: submitted.to },
        errors: !parsed.success ? fieldErrors(parsed.issues) : scope && 'errors' in scope ? scope.errors : {},
        status: 400,
      });
    }
    request = { plexItemId, scope: scope.scope };
  }

  try {
    await linkPlexItem(id, request);
  } catch (error) {
    return failurePage(context, id, error, 'link');
  }
  return redirect(routes.entityDetail.movies.href({ id }), 303);
}

export default createController(routes.entityPlexLink.movies, { actions: { index, action } });
