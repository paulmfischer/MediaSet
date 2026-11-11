import type { Handle, RemixNode } from 'remix/component';

import type { PlexSearchResult } from '../../models.ts';
import { Field, errorProps, inputClasses } from '../forms/field.tsx';
import { PLEX_LINK_FRAGMENT } from './public/plex-link-fragment.ts';

/** The scope fields as submitted (or prefilled), kept as text so a failed submit can show them again. */
export interface PlexScopeValues {
  scopeKind: string;
  season: string;
  from: string;
  to: string;
}

interface PlexLinkPageProps {
  /** `page` is the full no-JavaScript page; `fragment` is the same content inside the dialog. */
  fragment: boolean;
  movieTitle: string;
  /** True when the entry already has a Plex link, so the flow is a relink. */
  relink: boolean;
  isTv: boolean;
  step: 'search' | 'scope';
  /** GET URL of the flow (search and the scope step). */
  searchHref: string;
  /** POST URL that links. */
  actionHref: string;
  /** The detail page, where Cancel goes. */
  cancelHref: string;
  query: string;
  /** Matches for the query; undefined when no search ran (a query that is too short). */
  results?: PlexSearchResult[] | undefined;
  /** The Plex item picked for the scope step. */
  selected?: { plexItemId: string; title: string } | undefined;
  scope?: PlexScopeValues | undefined;
  errors?: Record<string, string>;
  formError?: string | undefined;
}

export const SCOPE_KINDS = [
  { value: 'season', label: 'One season' },
  { value: 'range', label: 'A range of seasons' },
  { value: 'all', label: 'Complete series' },
] as const;

export function resultLabel(result: PlexSearchResult): string {
  return result.year ? `${result.title} (${result.year})` : result.title;
}

const HEADING_ID = 'plex-link-heading';

// Fragment mode keeps the heading id the dialog is labelled by, and Cancel closes the dialog.
function Shell(handle: Handle<{ fragment: boolean; title: string; cancelHref: string; children?: RemixNode }>) {
  return () => {
    const { fragment, title, cancelHref, children } = handle.props;
    return (
      <div class="flex flex-col gap-4 px-2 max-w-3xl mx-auto" data-plex-link={fragment ? PLEX_LINK_FRAGMENT : 'page'}>
        {fragment ? (
          <h2 id={HEADING_ID} class="text-xl">
            {title}
          </h2>
        ) : (
          <h1 class="text-2xl">{title}</h1>
        )}
        {children}
        <div class="flex justify-end">
          {fragment ? (
            <button type="button" class="secondary" data-plex-close>
              Cancel
            </button>
          ) : (
            // A GET form, so the no-JavaScript Cancel is a real button with the same style
            <form method="get" action={cancelHref}>
              <button type="submit" class="secondary">
                Cancel
              </button>
            </form>
          )}
        </div>
      </div>
    );
  };
}

function SearchStep(handle: Handle<PlexLinkPageProps>) {
  return () => {
    const { movieTitle, isTv, searchHref, actionHref, query, results } = handle.props;
    return (
      <>
        <form method="get" action={searchHref} role="search" class="flex flex-col gap-2">
          <label for="plex-query" class="block text-sm font-medium text-gray-200">
            Search Plex for a {isTv ? 'show' : 'movie'}
          </label>
          <div class="flex gap-2">
            <input
              id="plex-query"
              name="query"
              type="search"
              value={query}
              placeholder={movieTitle}
              autocomplete="off"
              class={inputClasses}
            />
            <button type="submit">Search</button>
          </div>
        </form>
        {results === undefined ? (
          <p role="status">Enter at least 2 characters to search Plex.</p>
        ) : results.length === 0 ? (
          <p role="status" data-testid="plex-no-results">
            No matches on Plex for “{query}”. Try a different title.
          </p>
        ) : (
          <ul class="flex flex-col gap-2" aria-label="Plex results" data-testid="plex-results">
            {results.map((result) => (
              <li
                key={result.plexItemId}
                class="flex items-center justify-between gap-3 rounded-md border border-zinc-300 bg-zinc-100 dark:border-zinc-500 dark:bg-zinc-700 p-3"
              >
                <span class="min-w-0 wrap-break-word">
                  <span class="font-medium">{resultLabel(result)}</span>
                  <span class="block text-sm dark:text-slate-300">{result.library}</span>
                </span>
                {isTv ? (
                  // A TV entry picks its seasons next: a GET to the scope step.
                  <form method="get" action={searchHref}>
                    <input type="hidden" name="query" value={query} />
                    <input type="hidden" name="plexItemId" value={result.plexItemId} />
                    <input type="hidden" name="plexTitle" value={resultLabel(result)} />
                    <button type="submit" aria-label={`Choose ${resultLabel(result)}`}>
                      Choose
                    </button>
                  </form>
                ) : (
                  <form method="post" action={actionHref}>
                    <input type="hidden" name="plexItemId" value={result.plexItemId} />
                    <button type="submit" aria-label={`Link to ${resultLabel(result)}`}>
                      Link
                    </button>
                  </form>
                )}
              </li>
            ))}
          </ul>
        )}
      </>
    );
  };
}

function ScopeStep(handle: Handle<PlexLinkPageProps>) {
  return () => {
    const { searchHref, actionHref, query, selected, scope, errors = {}, formError } = handle.props;
    const values = scope ?? { scopeKind: 'all', season: '', from: '', to: '' };
    const number = (name: 'season' | 'from' | 'to', label: string) => (
      <Field name={name} label={label} error={errors[name]}>
        <input
          id={name}
          name={name}
          type="number"
          inputmode="numeric"
          min="0"
          max="99"
          value={values[name]}
          class={inputClasses}
          {...errorProps(name, errors[name])}
        />
      </Field>
    );
    return (
      <form method="post" action={actionHref} class="flex flex-col gap-4">
        {formError ? (
          <p role="alert" class="rounded-md border border-red-500/50 bg-red-500/10 p-3 text-red-300">
            {formError}
          </p>
        ) : null}
        <p>
          Linking to <strong>{selected?.title}</strong>. Which seasons does this entry cover?
        </p>
        <input type="hidden" name="plexItemId" value={selected?.plexItemId ?? ''} />
        <input type="hidden" name="plexTitle" value={selected?.title ?? ''} />
        <input type="hidden" name="query" value={query} />
        <fieldset class="flex flex-col gap-3" aria-describedby={errors.scopeKind ? 'scopeKind-error' : undefined}>
          <legend class="mb-1 text-sm font-medium text-gray-200">Seasons covered</legend>
          {SCOPE_KINDS.map((kind) => (
            <label key={kind.value} class="flex items-center gap-2">
              <input type="radio" name="scopeKind" value={kind.value} checked={values.scopeKind === kind.value} />
              {kind.label}
            </label>
          ))}
          {errors.scopeKind ? (
            <p id="scopeKind-error" class="text-sm text-red-400">
              {errors.scopeKind}
            </p>
          ) : null}
        </fieldset>
        <div class="grid grid-cols-1 gap-3 sm:grid-cols-3">
          {number('season', 'Season number')}
          {number('from', 'First season of the range')}
          {number('to', 'Last season of the range')}
        </div>
        <p class="text-sm dark:text-slate-300">Season 0 is the specials.</p>
        <div class="flex flex-wrap items-center justify-end gap-2">
          <a href={`${searchHref}?${new URLSearchParams({ query })}`} data-plex-step class="px-3 py-1">
            Back to results
          </a>
          <button type="submit">Link</button>
        </div>
      </form>
    );
  };
}

// The server-rendered Plex link flow: a search page, then for a TV entry the scope step. Without JavaScript it is
// a page of its own; the same markup fills the dialog on the detail page.
export function PlexLinkPage(handle: Handle<PlexLinkPageProps>) {
  return () => {
    const props = handle.props;
    const title = props.relink ? 'Relink to Plex' : 'Link to Plex';
    return (
      <Shell fragment={props.fragment} title={`${title}: ${props.movieTitle}`} cancelHref={props.cancelHref}>
        {props.step === 'scope' ? <ScopeStep {...props} /> : <SearchStep {...props} />}
      </Shell>
    );
  };
}
