import type { Handle } from 'remix/component';

import type { BaseEntity } from '../../models.ts';
import { Icon } from '../icon.tsx';
import { Check, ImageOff, Pencil, Trash2 } from '../icons.ts';
import { millisecondsToMinutesSeconds } from '../../utils/helpers.ts';
import type { DetailConfig } from './detail-config.ts';

interface DetailPageProps<T extends BaseEntity> {
  config: DetailConfig<T>;
  entity: T;
  /** Collection path, e.g. `/books`; also the base for the item's edit and delete URLs. */
  basePath: string;
  /** Browser-reachable API origin, used for the cover image. */
  apiUrl: string;
}

const row = 'flex flex-col md:flex-row mb-2 md:mb-0';
const label = 'basis-2/12 dark:text-slate-400';

export function DetailPage<T extends BaseEntity>(handle: Handle<DetailPageProps<T>>) {
  return () => {
    const { config, entity, basePath, apiUrl } = handle.props;
    const title = config.titleOf(entity);
    const subtitle = config.subtitleOf?.(entity);
    const discs = config.discList?.(entity);
    const lookup = entity.imageLookup;
    const lookupFailed = lookup && (lookup.failureReason || lookup.permanentFailure);

    return (
      <div class={`entity-${config.entity.toLowerCase()} flex flex-col`}>
        <div class="flex flex-row items-center justify-between border-b-2 border-slate-600 pb-2">
          <div class="flex flex-col gap-2 items-baseline">
            <h1 class="text-2xl">{title}</h1>
            {subtitle ? <h2 class="text-sm">{subtitle}</h2> : null}
          </div>
          <div class="flex flex-row gap-3">
            <a href={`${basePath}/${entity.id}/edit`} aria-label="Edit" title="Edit">
              <Icon icon={Pencil} size={22} />
            </a>
            <a href={`${basePath}/${entity.id}/delete`} aria-label="Delete" title="Delete">
              <Icon icon={Trash2} size={22} />
            </a>
          </div>
        </div>
        <div class="h-full mt-4">
          <div class="flex flex-col lg:flex-row lg:gap-3">
            <div class="lg:basis-1/5 flex justify-center mb-4 lg:mb-0">
              {entity.coverImage ? (
                <img
                  src={`${apiUrl}/static/images/${entity.coverImage.filePath}`}
                  alt={title || config.coverAlt}
                  class="max-h-96 rounded-lg object-contain"
                />
              ) : (
                <div
                  role="img"
                  aria-label="No image"
                  class="flex h-48 w-48 items-center justify-center rounded-lg border border-slate-700 text-slate-500"
                >
                  <Icon icon={ImageOff} size={48} />
                </div>
              )}
            </div>
            <div class="lg:basis-4/5">
              {config.fields.map((field) => (
                <div class={row}>
                  <span class={label}>{field.label}</span>
                  <div class="grow" data-field={field.label}>
                    {field.check ? field.check(entity) ? <Icon icon={Check} size={18} /> : null : field.value(entity)}
                  </div>
                </div>
              ))}
              {discs && discs.length > 0 ? (
                <div class={`${row} mt-4`}>
                  <span class={label}>Disc List</span>
                  <div class="grow">
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
                </div>
              ) : null}
              {lookupFailed ? (
                <p class="mt-4 text-sm text-amber-400" data-testid="image-lookup-info">
                  {lookup.permanentFailure
                    ? 'Image lookup failed permanently and will not be retried.'
                    : 'Image lookup failed and will be retried.'}
                  {lookup.failureReason ? ` Reason: ${lookup.failureReason}` : ''}
                </p>
              ) : null}
            </div>
          </div>
        </div>
      </div>
    );
  };
}
