import { Form, Link, useLoaderData, useLocation } from 'react-router';
import { ChevronDown, ChevronUp, Check, X } from 'lucide-react';
import { useEffect, useRef } from 'react';
import type { Route } from './+types/collection-list';
import { readCollection } from '~/api/collections.server';
import { publicApiUrl } from '~/config.server';
import { collectionQuery, fieldValue, getCollection, type CollectionEntity } from '~/utils/collections';
import ImageDisplay from '~/components/images/image-display';
import Pagination from '~/components/inputs/pagination';

export function meta({ data }: Route.MetaArgs) {
  return [{ title: `${data?.entityType ?? 'Collection'} List` }];
}

export async function loader({ request, params }: Route.LoaderArgs) {
  const collection = getCollection(params.entity);
  const query = collectionQuery(new URL(request.url), collection);
  const result = await readCollection(collection.type, query, request.signal);
  return { ...result, ...query, page: result.page, entityType: collection.type, apiUrl: publicApiUrl() };
}

export default function CollectionList() {
  const { items, page, totalPages, totalCount, entityType, searchText, orderBy, apiUrl } =
    useLoaderData<typeof loader>();
  const collection = getCollection(entityType.toLowerCase());
  const { search } = useLocation();
  const basePath = `/${entityType.toLowerCase()}`;
  const searchInput = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (searchInput.current) searchInput.current.value = searchText;
  }, [entityType, searchText]);
  const [sortField, direction] = orderBy.split(':');
  const entityTitle = (entity: CollectionEntity) =>
    `${entity.title ?? ''}${entity.subtitle ? `: ${entity.subtitle}` : ''}`;
  const canonicalSearch = new URLSearchParams(search).toString();
  const detailLink = (entity: CollectionEntity) =>
    `${basePath}/${entity.id}${canonicalSearch ? `?${canonicalSearch}` : ''}`;
  return (
    <div className={`entity-${entityType.toLowerCase()} flex flex-col px-2`}>
      <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between bg-entity/10 border border-entity/20 rounded-lg p-3">
        <h2 className="text-2xl mb-1 sm:mb-0 text-entity">{entityType}</h2>
        <Form method="get" action={basePath} role="search" className="flex w-full sm:w-auto gap-2">
          <input type="hidden" name="orderBy" value={orderBy} />
          <div className="flex flex-1">
            <input
              ref={searchInput}
              id="search"
              type="search"
              name="searchText"
              defaultValue={searchText}
              placeholder={`Search ${entityType}`}
              aria-label={`Search ${entityType}`}
              className={`w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400 ${searchText ? 'border-r-0 rounded-l-md' : 'rounded-md'}`}
            />
            {searchText && (
              <Link
                to={`${basePath}?${new URLSearchParams({ orderBy })}`}
                className="text-icon rounded-r-md flex items-center"
                aria-label="Clear search"
              >
                <X />
              </Link>
            )}
          </div>
          <button type="submit" aria-label="Search">
            Search
          </button>
        </Form>
      </div>
      <div className="h-full mt-4">
        {items.length === 0 ? (
          <div className="flex justify-center text-center">
            {searchText ? `No ${entityType} match your search.` : `You don't appear to have any ${entityType}!`}
          </div>
        ) : (
          <>
            <div className="md:hidden flex flex-col divide-y divide-slate-700">
              {items.map((entity) => (
                <div key={entity.id} className="flex flex-row items-center gap-3 py-2 px-2 dark:hover:bg-zinc-800">
                  {entity.coverImage && (
                    <ImageDisplay
                      key={entity.id}
                      imageData={entity.coverImage}
                      alt={`${entity.title} cover`}
                      entityType={entityType}
                      entityId={entity.id}
                      apiUrl={apiUrl}
                      size="xsmall"
                    />
                  )}
                  <div className="flex-1 min-w-0">
                    <Link to={detailLink(entity)} className="font-medium block truncate">
                      {entityTitle(entity)}
                    </Link>
                    <div className="text-xs text-gray-400 truncate">{collection.summary(entity)}</div>
                  </div>
                </div>
              ))}
            </div>
            <table className="hidden md:table text-left w-full">
              <thead className="dark:bg-zinc-700 border-b-2 border-slate-600">
                <tr>
                  <th className="hidden md:table-cell pl-2 p-1 border-r border-slate-800">Cover</th>
                  {collection.columns.map((column) => {
                    const active = sortField === column.key;
                    const nextOrder = `${column.key}:${active && direction === 'asc' ? 'desc' : 'asc'}`;
                    return (
                      <th
                        key={column.key}
                        className={`pl-2 p-1 border-r border-slate-800 ${column.className ?? ''}`}
                        aria-sort={active ? (direction === 'asc' ? 'ascending' : 'descending') : 'none'}
                      >
                        <Link
                          to={`?${new URLSearchParams({ searchText, orderBy: nextOrder })}`}
                          className="flex items-center gap-1 text-inherit no-underline"
                        >
                          {column.label}
                          {active && (direction === 'asc' ? <ChevronUp size={14} /> : <ChevronDown size={14} />)}
                        </Link>
                      </th>
                    );
                  })}
                </tr>
              </thead>
              <tbody>
                {items.map((entity) => (
                  <tr key={entity.id} className="border-b border-slate-700 dark:hover:bg-zinc-800">
                    <td className="hidden md:table-cell w-16 pl-2 p-1 border-r border-slate-800">
                      {entity.coverImage && (
                        <ImageDisplay
                          key={entity.id}
                          imageData={entity.coverImage}
                          alt={`${entity.title} cover`}
                          entityType={entityType}
                          entityId={entity.id}
                          apiUrl={apiUrl}
                          size="xsmall"
                        />
                      )}
                    </td>
                    {collection.columns.map((column) => (
                      <td key={column.key} className={`pl-2 p-1 border-r border-slate-800 ${column.className ?? ''}`}>
                        {column.key === 'title' ? (
                          <Link to={detailLink(entity)}>{entityTitle(entity)}</Link>
                        ) : column.key === 'isTvSeries' ? (
                          entity.isTvSeries && <Check aria-label="TV show" size={18} />
                        ) : (
                          fieldValue(entity, column.key)
                        )}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </>
        )}
      </div>
      <Pagination
        page={page}
        totalPages={totalPages}
        totalCount={totalCount}
        searchText={searchText}
        orderBy={orderBy}
        basePath={basePath}
      />
    </div>
  );
}
