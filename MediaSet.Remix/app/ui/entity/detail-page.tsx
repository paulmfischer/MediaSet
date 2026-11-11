import type { Handle } from 'remix/component';

import type { BaseEntity, PlexItem } from '../../models.ts';
import { Icon } from '../icon.tsx';
import { Check, ImageOff, Pencil } from '../icons.ts';
import { millisecondsToMinutesSeconds } from '../../utils/helpers.ts';
import type { DetailConfig } from './detail-config.ts';
import { PlexCard } from '../plex/plex-card.tsx';
import { PlexIcon } from '../plex/plex-icon.tsx';
import { ConfirmDelete } from './public/confirm-delete.tsx';

interface DetailPageProps<T extends BaseEntity> {
  config: DetailConfig<T>;
  entity: T;
  /** Collection path, e.g. `/books`; also the base for the item's edit and delete URLs. */
  basePath: string;
  /** Browser-reachable API origin, used for the cover image. */
  apiUrl: string;
  /** The Plex gate: when false, no Plex icon or card renders. Only ever true for movies. */
  plexEnabled?: boolean;
  /** Set after a save whose cover image could not be stored; the entity itself was saved. */
  imageNotice?: string;
  /** Fixed "now" for the card's relative sync time (tests); defaults to the current time. */
  now?: Date;
}

// "Jan 5, 2025"; falls back to the raw text when it is not a parseable date.
function formatLookupDate(value: string | undefined): string {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric', timeZone: 'UTC' });
}

// Two columns: field names (as wide as the longest) and values, so every value starts at the same left edge and
// a long value wraps inside its own column.
const label = 'dark:text-slate-300';
const value = 'min-w-0 wrap-break-word';

export function DetailPage<T extends BaseEntity>(handle: Handle<DetailPageProps<T>>) {
  return () => {
    const { config, entity, basePath, apiUrl, plexEnabled = false, imageNotice, now } = handle.props;
    const plex = plexEnabled ? (entity as { plex?: PlexItem }).plex : undefined;
    const itemId = encodeURIComponent(entity.id ?? '');
    const title = config.titleOf(entity);
    const subtitle = config.subtitleOf?.(entity);
    const discs = config.discList?.(entity);
    const lookup = entity.imageLookup;
    const attemptedOn = lookup ? formatLookupDate(lookup.lookupAttemptedAt) : '';
    const lookupFailed = lookup && (lookup.failureReason || lookup.permanentFailure);

    return (
      <div class={`entity-${config.entity.toLowerCase()} flex flex-col`}>
        <div class="flex flex-row items-center justify-between border-b-2 border-slate-600 pb-2">
          <div class="flex flex-col gap-2 items-baseline">
            <div class="flex flex-row items-center gap-2">
              <h1 class="text-2xl">{title}</h1>
              {plexEnabled ? <PlexIcon plex={plex} /> : null}
            </div>
            {subtitle ? <h2 class="text-sm">{subtitle}</h2> : null}
          </div>
          <div class="flex flex-row gap-3">
            <a href={`${basePath}/${itemId}/edit`} aria-label="Edit" title="Edit">
              <Icon icon={Pencil} size={22} />
            </a>
            <ConfirmDelete title={title || config.singular} href={`${basePath}/${itemId}/delete`} />
          </div>
        </div>
        {imageNotice ? (
          <p class="mt-4 text-sm text-amber-400" role="status" data-testid="image-error-notice">
            {imageNotice}
          </p>
        ) : null}
        <div class="h-full mt-4">
          <div class="flex flex-col lg:flex-row lg:gap-3">
            <div class="lg:basis-1/5 flex justify-center mb-4 lg:mb-0">
              {entity.coverImage ? (
                <img
                  src={`${apiUrl}/static/images/${entity.coverImage.filePath}`}
                  alt={title || config.coverAlt}
                  class="max-h-96 rounded-lg object-contain"
                  loading="lazy"
                />
              ) : (
                <div class="flex h-48 w-48 flex-col items-center justify-center rounded-lg bg-gray-700 text-center text-sm text-gray-300 lg:h-64 lg:w-64">
                  <Icon icon={ImageOff} size={64} />
                  <p class="mt-2">No image available</p>
                </div>
              )}
            </div>
            <div class="lg:basis-4/5">
              <div class="grid grid-cols-[max-content_1fr] gap-x-6 gap-y-2">
                {config.fields.map((field) => (
                  <>
                    <span class={label}>{field.label}</span>
                    <div class={value} data-field={field.label}>
                      {field.check ? field.check(entity) ? <Icon icon={Check} size={18} /> : null : field.value(entity)}
                    </div>
                  </>
                ))}
                {discs && discs.length > 0 ? (
                  <>
                    <span class={`${label} mt-2`}>Disc List</span>
                    <div class={`${value} mt-2`} data-field="Disc List">
                      <table class="w-full text-sm">
                        <thead class="border-b border-gray-600">
                          <tr>
                            <th class="text-left py-1 px-2">Track #</th>
                            <th class="text-left py-1 px-2">Title</th>
                            <th class="text-left py-1 px-2">Duration</th>
                          </tr>
                        </thead>
                        <tbody>
                          {discs.map((disc) => (
                            <tr class="border-b border-gray-700">
                              <td class="py-1 px-2">{disc.trackNumber}</td>
                              <td class="py-1 px-2">{disc.title}</td>
                              <td class="py-1 px-2">{millisecondsToMinutesSeconds(disc.duration)}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </>
                ) : null}
              </div>
              {plexEnabled ? (
                <PlexCard
                  plex={plex}
                  title={title || config.singular}
                  refreshHref={`${basePath}/${itemId}/plex/refresh`}
                  linkHref={`${basePath}/${itemId}/plex/link`}
                  unlinkHref={`${basePath}/${itemId}/plex/unlink`}
                  detailHref={`${basePath}/${itemId}`}
                  now={now}
                />
              ) : null}
              {lookupFailed ? (
                <div
                  class="mt-4 flex flex-col items-start gap-2 text-sm text-amber-400"
                  data-testid="image-lookup-info"
                >
                  <p>
                    {lookup.permanentFailure
                      ? 'Image lookup failed permanently and will not be retried.'
                      : 'Image lookup failed and will be retried.'}
                    {lookup.failureReason ? ` Reason: ${lookup.failureReason}` : ''}
                    {attemptedOn ? ` Last attempted ${attemptedOn}.` : ''}
                  </p>
                  <form method="post" action={`${basePath}/${itemId}/clear-image-lookup`}>
                    <button type="submit">Clear &amp; retry</button>
                  </form>
                </div>
              ) : null}
            </div>
          </div>
        </div>
      </div>
    );
  };
}
