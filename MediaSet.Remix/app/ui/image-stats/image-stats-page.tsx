import type { Handle } from 'remix/component';

import type { ImageStats } from '../../api/image-stats-data.ts';
import { routes } from '../../routes.ts';
import { StatCard } from '../dashboard/stat-card.tsx';
import { Icon } from '../icon.tsx';
import {
  AlertTriangle,
  Clapperboard,
  Files,
  Gamepad2,
  HardDrive,
  ImageOff,
  LibraryBig,
  Link2Off,
  Music,
  Trash2,
} from '../icons.ts';
import {
  ENTITY_LABELS,
  detailHref,
  ENTITY_TYPE_ORDER,
  formatBytes,
  groupByEntityType,
  groupOrphanedByEntityType,
} from './format.ts';
import { LocalDateTime } from './public/local-date-time.tsx';
import { ImageStatsTabs, type ImageStatsTab } from './public/image-stats-tabs.tsx';

const DEFAULT_COLOR = 'bg-cyan-500/10 text-cyan-400 border-cyan-500/20';
const ENTITY_COLOR = 'bg-entity/10 text-entity border-entity/20';
const ENTITY_ICONS = { books: LibraryBig, movies: Clapperboard, games: Gamepad2, musics: Music };

interface ImageStatsPageProps {
  /** Null when no statistics have been generated yet. */
  stats: ImageStats | null;
  /** A form error from the last action, shown above the overview. */
  error?: string;
  /** The entity tab to show first. */
  tab?: string;
}

function renderTitleLink(entityType: string, id: string, title: string) {
  const href = detailHref(entityType, id);
  return href ? (
    <a href={href} class="text-cyan-400 hover:underline">
      {title}
    </a>
  ) : (
    <span>{title}</span>
  );
}

function renderHeading() {
  return (
    <div class="flex items-center gap-3">
      <Icon icon={HardDrive} size={32} class="text-cyan-400" />
      <h1 class="text-3xl font-bold text-white">Image Statistics</h1>
    </div>
  );
}

const TABLE_WRAPPER = 'max-h-64 overflow-y-auto rounded-lg border border-zinc-700';
const THEAD = 'sticky top-0 bg-zinc-800 text-left text-zinc-400';
const ROW = 'bg-zinc-900 hover:bg-zinc-800';

function renderResetForm(props: { entityType: string; ids: string[]; label: string; withIcon?: boolean }) {
  return (
    <form method="post" action={routes.imageStats.action.href()}>
      <input type="hidden" name="intent" value="reset-lookup" />
      <input type="hidden" name="entityType" value={props.entityType} />
      <input type="hidden" name="tab" value={props.entityType} />
      <input type="hidden" name="entityIds" value={JSON.stringify(props.ids)} />
      <button type="submit" class={props.withIcon ? 'flex items-center gap-2' : undefined}>
        {props.withIcon ? <Icon icon={ImageOff} size={16} /> : null}
        {props.label}
      </button>
    </form>
  );
}

function renderEntityPanel(props: { entityType: string; stats: ImageStats }) {
  const { entityType, stats } = props;
  const brokenLinks = groupByEntityType(stats.brokenLinks)[entityType] ?? [];
  const lookupFailures = groupByEntityType(stats.imageLookupFailures)[entityType] ?? [];
  const orphanedFiles = groupOrphanedByEntityType(stats.orphanedFiles)[entityType] ?? [];

  return (
    <div class="space-y-6 p-4">
      {brokenLinks.length > 0 ? (
        <div>
          <h3 class="mb-2 flex items-center gap-2 text-sm font-semibold text-red-400">
            <Icon icon={Link2Off} size={16} />
            Broken Links
            <span class="text-zinc-500">— entities referencing missing image files</span>
          </h3>
          <div class={TABLE_WRAPPER}>
            <table class="w-full text-sm">
              <thead class={THEAD}>
                <tr>
                  <th class="px-4 py-3">Title</th>
                  <th class="px-4 py-3">Missing File</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-zinc-700">
                {brokenLinks.map((link) => (
                  <tr key={`${link.entityType}-${link.entityId}`} class={ROW}>
                    <td class="px-4 py-3">{renderTitleLink(link.entityType, link.entityId, link.title)}</td>
                    <td class="px-4 py-3 font-mono text-xs text-zinc-500">{link.missingFilePath}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ) : null}

      {lookupFailures.length > 0 ? (
        <div>
          <div class="mb-2 flex items-center justify-between">
            <h3 class="flex items-center gap-2 text-sm font-semibold text-orange-400">
              <Icon icon={ImageOff} size={16} />
              Lookup Failures
              <span class="text-zinc-500">— entities where background image lookup failed</span>
            </h3>
            {renderResetForm({
              entityType,
              ids: lookupFailures.map((failure) => failure.entityId),
              label: 'Reset All',
              withIcon: true,
            })}
          </div>
          <div class={TABLE_WRAPPER}>
            <table class="w-full text-sm">
              <thead class={THEAD}>
                <tr>
                  <th class="px-4 py-3">Title</th>
                  <th class="px-4 py-3">Attempted At</th>
                  <th class="px-4 py-3">Failure Reason</th>
                  <th class="px-4 py-3">Permanent</th>
                  <th class="px-4 py-3"></th>
                </tr>
              </thead>
              <tbody class="divide-y divide-zinc-700">
                {lookupFailures.map((failure) => (
                  <tr key={`${failure.entityType}-${failure.entityId}`} class={ROW}>
                    <td class="px-4 py-3">{renderTitleLink(failure.entityType, failure.entityId, failure.title)}</td>
                    <td class="px-4 py-3 text-zinc-400">
                      <LocalDateTime value={failure.lookupAttemptedAt} />
                    </td>
                    <td class="px-4 py-3 text-zinc-400">
                      <div>{failure.failureReason ?? '—'}</div>
                      <div class="text-xs">
                        Provider: {failure.provider ?? 'Unknown (legacy)'} · Outcome:{' '}
                        {failure.outcome ?? 'Unknown (legacy)'}
                      </div>
                      <div class="text-xs">
                        Strategy: {failure.attemptedStrategy ?? 'Unknown (legacy)'} · Attempts:{' '}
                        {failure.attemptCount ?? 'Unknown'}
                      </div>
                      <div class="text-xs">
                        Retry:{' '}
                        {failure.nextEligibleAt ? (
                          <LocalDateTime value={failure.nextEligibleAt} />
                        ) : failure.permanentFailure ? (
                          'Terminal'
                        ) : (
                          'Unknown (legacy)'
                        )}
                      </div>
                    </td>
                    <td class="px-4 py-3">
                      {failure.permanentFailure ? (
                        <span class="rounded-full bg-red-500/20 px-2 py-0.5 text-xs text-red-400">Yes</span>
                      ) : (
                        <span class="rounded-full bg-zinc-700 px-2 py-0.5 text-xs text-zinc-400">No</span>
                      )}
                    </td>
                    <td class="px-4 py-3">
                      {renderResetForm({ entityType: failure.entityType, ids: [failure.entityId], label: 'Reset' })}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ) : null}

      {orphanedFiles.length > 0 ? (
        <div>
          <div class="mb-2 flex items-center justify-between">
            <h3 class="flex items-center gap-2 text-sm font-semibold text-yellow-400">
              <Icon icon={AlertTriangle} size={16} />
              Orphaned Files
              <span class="text-zinc-500">— files on disk not referenced by any entity</span>
            </h3>
            <form method="post" action={routes.imageStats.action.href()}>
              <input type="hidden" name="intent" value="delete-orphaned" />
              <input type="hidden" name="tab" value={entityType} />
              <button type="submit" class="flex items-center gap-2">
                <Icon icon={Trash2} size={16} />
                Clean Up All
              </button>
            </form>
          </div>
          <div class={TABLE_WRAPPER}>
            <table class="w-full text-sm">
              <thead class={THEAD}>
                <tr>
                  <th class="px-4 py-3">File Path</th>
                  <th class="px-4 py-3">Size</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-zinc-700">
                {orphanedFiles.map((file) => (
                  <tr key={file.relativePath} class={ROW}>
                    <td class="px-4 py-3 font-mono text-xs text-zinc-400">{file.relativePath}</td>
                    <td class="px-4 py-3 text-zinc-400">{formatBytes(file.sizeBytes)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ) : null}

      {brokenLinks.length === 0 && lookupFailures.length === 0 && orphanedFiles.length === 0 ? (
        <p class="text-sm text-zinc-500">No image issues for this entity type.</p>
      ) : null}
    </div>
  );
}

function entityTabs(stats: ImageStats): ImageStatsTab[] {
  const broken = groupByEntityType(stats.brokenLinks);
  const failures = groupByEntityType(stats.imageLookupFailures);
  const orphaned = groupOrphanedByEntityType(stats.orphanedFiles);

  return ENTITY_TYPE_ORDER.filter(
    (type) =>
      (stats.filesByEntityType[type] ?? 0) > 0 ||
      (broken[type]?.length ?? 0) > 0 ||
      (failures[type]?.length ?? 0) > 0 ||
      (orphaned[type]?.length ?? 0) > 0
  ).map((type) => ({
    id: type,
    className: `entity-${type}`,
    label: (
      <div class="flex items-start justify-between">
        <div class="flex-1">
          <div class="flex items-center gap-2">
            <p class="text-sm font-medium text-zinc-400">{ENTITY_LABELS[type]}</p>
            {(orphaned[type]?.length ?? 0) > 0 ? <Icon icon={AlertTriangle} size={14} class="text-yellow-400" /> : null}
            {(broken[type]?.length ?? 0) > 0 ? <Icon icon={Link2Off} size={14} class="text-red-400" /> : null}
            {(failures[type]?.length ?? 0) > 0 ? <Icon icon={ImageOff} size={14} class="text-orange-400" /> : null}
          </div>
          <p class="mt-2 text-3xl font-bold text-white">
            {(stats.filesByEntityType[type] ?? 0).toLocaleString('en-US')} files (
            {formatBytes(stats.sizeByEntityType[type] ?? 0)})
          </p>
        </div>
        <div class={`rounded-lg border p-3 ${ENTITY_COLOR}`}>
          <Icon icon={ENTITY_ICONS[type]} />
        </div>
      </div>
    ),
    panel: renderEntityPanel({ entityType: type, stats }),
  }));
}

export function ImageStatsPage(handle: Handle<ImageStatsPageProps>) {
  return () => {
    const { stats, error, tab } = handle.props;

    if (!stats) {
      return (
        <div class="space-y-6">
          {renderHeading()}
          <div class="rounded-lg border border-zinc-700 bg-zinc-800 p-8 text-center">
            <Icon icon={HardDrive} size={48} class="mx-auto mb-4 text-zinc-500" />
            <p class="text-zinc-400">No image statistics available yet. Navigate away and back to refresh.</p>
          </div>
        </div>
      );
    }

    const tabs = entityTabs(stats);
    const counts = [
      { title: 'Total Files', value: stats.totalFiles, icon: Files, alert: '' as string },
      { title: 'Total Size', value: formatBytes(stats.totalSizeBytes), icon: HardDrive, alert: '' as string },
      {
        title: 'Orphaned Files',
        value: stats.orphanedFiles.length,
        icon: AlertTriangle,
        alert: 'bg-yellow-500/10 text-yellow-400 border-yellow-500/20',
      },
      {
        title: 'Broken Links',
        value: stats.brokenLinks.length,
        icon: Link2Off,
        alert: 'bg-red-500/10 text-red-400 border-red-500/20',
      },
      {
        title: 'Lookup Failures',
        value: stats.imageLookupFailures.length,
        icon: ImageOff,
        alert: 'bg-orange-500/10 text-orange-400 border-orange-500/20',
      },
    ];

    return (
      <div class="space-y-8">
        {renderHeading()}
        <p class="text-sm text-zinc-400">
          Last updated: <LocalDateTime value={stats.lastUpdated} />
        </p>

        {error ? (
          <div role="alert" class="rounded-lg border border-red-500/20 bg-red-500/10 p-4 text-sm text-red-400">
            {error}
          </div>
        ) : null}

        <div>
          <h2 class="mb-4 text-xl font-semibold text-white">Overview</h2>
          <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {counts.map((card) => {
              const numeric = typeof card.value === 'number';
              return (
                <StatCard
                  title={card.title}
                  value={numeric ? card.value.toLocaleString('en-US') : card.value}
                  icon={card.icon}
                  colorClass={
                    card.alert && typeof card.value === 'number' && card.value > 0 ? card.alert : DEFAULT_COLOR
                  }
                />
              );
            })}
          </div>
        </div>

        {tabs.length > 0 ? (
          <div>
            <h2 class="mb-4 text-xl font-semibold text-white">By Entity Type</h2>
            <ImageStatsTabs tabs={tabs} initialTab={tab} />
          </div>
        ) : null}
      </div>
    );
  };
}
