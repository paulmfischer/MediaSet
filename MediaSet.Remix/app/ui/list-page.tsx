import type { Handle } from 'remix/component';

import { DEFAULT_ORDER_BY, listHref } from '../actions/list-params.ts';
import type { PagedResult } from '../api/entity-data.ts';
import type { BaseEntity } from '../models.ts';
import { Icon } from './icon.tsx';
import { Check, ChevronDown, ChevronLeft, ChevronRight, ChevronUp, Pencil, Plus, X } from './icons.ts';
import type { ListConfig } from './list-config.ts';

interface ListPageProps<T extends BaseEntity> {
  config: ListConfig<T>;
  /** Collection path, e.g. `/books`; also the base for item links. */
  basePath: string;
  result: PagedResult<T>;
  searchText: string | null;
  orderBy: string;
  /** Browser-reachable API origin, used for cover images. */
  apiUrl: string;
}

const cell = 'pl-2 p-1 border-r border-slate-800';

export function ListPage<T extends BaseEntity>(handle: Handle<ListPageProps<T>>) {
  return () => {
    const { config, basePath, result, searchText, orderBy, apiUrl } = handle.props;
    const { items, page, totalPages, totalCount } = result;
    const name = config.entity;
    const [sortField, sortDirection] = orderBy.split(':');

    const cover = (item: T) =>
      item.coverImage ? (
        <img
          src={`${apiUrl}/static/images/${item.coverImage.filePath}`}
          alt={`${item.title} cover`}
          class="h-16 w-16 rounded-lg object-cover"
          loading="lazy"
        />
      ) : null;

    const sortableHeader = (label: string, field: string, className = '') => {
      const active = sortField?.toLowerCase() === field.toLowerCase();
      const next = active && sortDirection === 'asc' ? 'desc' : 'asc';
      return (
        <th
          class={`${cell} select-none ${className}`}
          aria-sort={active ? (sortDirection === 'asc' ? 'ascending' : 'descending') : 'none'}
        >
          <a
            href={listHref(basePath, { searchText, orderBy: `${field}:${next}` })}
            class="flex items-center gap-1"
            data-rmx-reset-scroll="manual"
          >
            {label}
            {active ? <Icon icon={sortDirection === 'asc' ? ChevronUp : ChevronDown} size={14} /> : null}
          </a>
        </th>
      );
    };

    const editLink = (item: T) => (
      <a href={`${basePath}/${item.id}/edit`} aria-label="Edit" title="Edit">
        <Icon icon={Pencil} size={18} />
      </a>
    );

    const pageLink = (target: number, label: string, icon: typeof ChevronLeft, iconFirst: boolean) => {
      const content = (
        <>
          {iconFirst ? <Icon icon={icon} size={16} /> : null}
          {label}
          {iconFirst ? null : <Icon icon={icon} size={16} />}
        </>
      );
      const enabled = target >= 1 && target <= totalPages;
      return enabled ? (
        <a href={listHref(basePath, { searchText, orderBy, page: target })} class="flex items-center gap-1">
          {content}
        </a>
      ) : (
        <span class="flex items-center gap-1 text-gray-600 cursor-not-allowed select-none">{content}</span>
      );
    };

    return (
      <div class={`entity-${name.toLowerCase()} flex flex-col px-2`}>
        <div class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between bg-entity/10 border border-entity/20 rounded-lg p-3">
          <div class="flex flex-row items-center gap-2">
            <h1 class="text-2xl mb-1 sm:mb-0 text-entity">{name}</h1>
            <a href={`${basePath}/add`} class="flex gap-1 items-center">
              <Icon icon={Plus} size={18} /> Add
            </a>
          </div>
          <div class="flex flex-row w-full sm:w-auto gap-2 items-center">
            <form role="search" method="get" action={basePath} class="flex flex-1 sm:flex-none gap-2">
              {orderBy !== DEFAULT_ORDER_BY ? <input type="hidden" name="orderBy" value={orderBy} /> : null}
              <div class="flex flex-1 gap-0 z-20">
                <input
                  id="search"
                  type="search"
                  name="searchText"
                  value={searchText ?? ''}
                  placeholder={`Search ${name}`}
                  aria-label={`Search ${name}`}
                  class={`w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400 ${searchText ? 'border-r-0 rounded-l-md rounded-r-none' : 'rounded-md'}`}
                />
                {searchText ? (
                  <a
                    href={basePath}
                    class="text-icon rounded-r-md flex items-center"
                    aria-label="Clear search"
                    title="Clear search"
                  >
                    <Icon icon={X} />
                  </a>
                ) : null}
              </div>
              <button type="submit" aria-label="Search">
                Search
              </button>
            </form>
          </div>
        </div>

        <div class="h-full mt-4">
          {items.length === 0 ? (
            <div class="flex justify-center text-center">
              <p>
                You don&apos;t appear to have any {name}!<br />
                Add to your collection by clicking the &apos;Add&apos; link up above!
              </p>
            </div>
          ) : (
            <>
              <div class="md:hidden flex flex-col divide-y divide-slate-700">
                {items.map((item) => (
                  <div key={item.id} class="flex flex-row items-center gap-3 py-2 px-2 dark:hover:bg-zinc-800">
                    {item.coverImage ? <div class="flex-shrink-0">{cover(item)}</div> : null}
                    <div class="flex-1 min-w-0">
                      <a href={`${basePath}/${item.id}`} class="font-medium block truncate">
                        {config.titleOf(item)}
                      </a>
                      <div class="text-xs text-gray-400 truncate">{config.summary(item)}</div>
                    </div>
                    <div class="flex flex-row gap-3 flex-shrink-0">{editLink(item)}</div>
                  </div>
                ))}
              </div>
              <table class="hidden md:table text-left w-full">
                <thead class="dark:bg-zinc-700 border-b-2 border-slate-600">
                  <tr>
                    <th class={`hidden md:table-cell ${cell}`}>Cover</th>
                    {sortableHeader('Title', 'title')}
                    {config.columns.map((column) => sortableHeader(column.label, column.field, column.class))}
                    <th class="w-1"></th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((item) => (
                    <tr key={item.id} class="border-b border-slate-700 dark:hover:bg-zinc-800">
                      <td class={`hidden md:table-cell w-16 ${cell}`}>{cover(item)}</td>
                      <td class={cell}>
                        <a href={`${basePath}/${item.id}`}>{config.titleOf(item)}</a>
                      </td>
                      {config.columns.map((column) => (
                        <td class={`${column.class} ${cell}`}>
                          {column.check ? (
                            <div class="flex justify-center">
                              {column.cell(item) ? <Icon icon={Check} size={16} /> : null}
                            </div>
                          ) : (
                            column.cell(item)
                          )}
                        </td>
                      ))}
                      <td class="flex flex-row gap-3 p-1 pt-2">{editLink(item)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </>
          )}
        </div>

        {totalPages > 1 ? (
          <nav
            aria-label="Pagination"
            class="sticky bottom-2 mb-2 flex items-center justify-center gap-6 py-3 px-4 bg-entity/20 border border-entity/30 rounded-lg backdrop-blur-sm"
          >
            <span class="text-sm text-gray-400">{totalCount} items</span>
            {pageLink(page - 1, 'Prev', ChevronLeft, true)}
            <span class="text-sm">
              Page {page} of {totalPages}
            </span>
            {pageLink(page + 1, 'Next', ChevronRight, false)}
          </nav>
        ) : null}
      </div>
    );
  };
}
