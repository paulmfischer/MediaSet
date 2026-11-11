import type { PlexFilter } from '../../models.ts';
import { parsePlexFilter } from '../../utils/plex.ts';

export const DEFAULT_ORDER_BY = 'title:asc';

export interface ListParams {
  searchText: string | null;
  orderBy: string;
  page: number;
  /** The Plex filter (movies only); an unknown value reads as no filter. */
  plex: PlexFilter | null;
}

/** Reads search, sort, page and the Plex filter from the query string, falling back to defaults for missing or invalid values. */
export function parseListParams(url: URL): ListParams {
  const page = Number.parseInt(url.searchParams.get('page') ?? '', 10);
  return {
    searchText: url.searchParams.get('searchText') || null,
    orderBy: url.searchParams.get('orderBy') || DEFAULT_ORDER_BY,
    page: Number.isFinite(page) && page > 0 ? page : 1,
    plex: parsePlexFilter(url.searchParams.get('plex')),
  };
}

/** Builds a list URL that carries only the non-default state. */
export function listHref(
  basePath: string,
  params: { searchText?: string | null; orderBy?: string; page?: number; plex?: PlexFilter | null }
) {
  const query = new URLSearchParams();
  if (params.searchText) query.set('searchText', params.searchText);
  if (params.orderBy) query.set('orderBy', params.orderBy);
  if (params.plex) query.set('plex', params.plex);
  if (params.page && params.page > 1) query.set('page', String(params.page));
  const text = query.toString();
  return text ? `${basePath}?${text}` : basePath;
}
