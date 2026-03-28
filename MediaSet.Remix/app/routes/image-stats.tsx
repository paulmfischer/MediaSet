import { type ActionFunctionArgs, type MetaFunction } from '@remix-run/node';
import { useLoaderData, Link, Form, useNavigation } from '@remix-run/react';
import Tabs, { type TabConfig } from '~/components/tabs';
import {
  getImageStats,
  type BrokenImageLink,
  type ImageLookupFailure,
  type OrphanedImageFile,
} from '~/api/image-stats-data';
import { deleteOrphanedImages, resetImageLookup } from '~/api/image-management-data';
import StatCard from '~/components/stat-card';
import {
  HardDrive,
  Files,
  AlertTriangle,
  Link2Off,
  Trash2,
  ImageOff,
  LibraryBig,
  Clapperboard,
  Gamepad2,
  Music,
  type LucideIcon,
} from 'lucide-react';

export const meta: MetaFunction = () => {
  return [
    { title: 'Image Statistics | MediaSet' },
    { name: 'description', content: 'Statistics about stored cover images' },
  ];
};

export const loader = async () => {
  const stats = await getImageStats();
  return { stats };
};

export const action = async ({ request }: ActionFunctionArgs) => {
  const formData = await request.formData();
  const intent = formData.get('intent');
  if (intent === 'delete-orphaned') {
    await deleteOrphanedImages();
  } else if (intent === 'reset-lookup') {
    const entityType = formData.get('entityType') as string;
    const entityIdsRaw = formData.get('entityIds') as string;
    const entityIds = JSON.parse(entityIdsRaw) as string[];
    await resetImageLookup(entityIds, entityType);
  }
  return null;
};

type EntityTypeConfig = {
  label: string;
  icon: LucideIcon;
  activeTopBorderClass: string;
  iconColorClass: string;
};

const ENTITY_TYPE_CONFIG: Record<string, EntityTypeConfig> = {
  books: {
    label: 'Books',
    icon: LibraryBig,
    activeTopBorderClass: '!border-t-green-500',
    iconColorClass: 'bg-green-500/10 text-green-400 border-green-500/20',
  },
  movies: {
    label: 'Movies',
    icon: Clapperboard,
    activeTopBorderClass: '!border-t-red-500',
    iconColorClass: 'bg-red-500/10 text-red-400 border-red-500/20',
  },
  games: {
    label: 'Games',
    icon: Gamepad2,
    activeTopBorderClass: '!border-t-purple-500',
    iconColorClass: 'bg-purple-500/10 text-purple-400 border-purple-500/20',
  },
  musics: {
    label: 'Music',
    icon: Music,
    activeTopBorderClass: '!border-t-pink-500',
    iconColorClass: 'bg-pink-500/10 text-pink-400 border-pink-500/20',
  },
};

const ENTITY_TYPE_ORDER = ['books', 'movies', 'games', 'musics'];

function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(1024));
  return `${(bytes / Math.pow(1024, i)).toFixed(1)} ${units[i]}`;
}

function groupByEntityType<T extends { entityType: string }>(items: T[]): Record<string, T[]> {
  return items.reduce<Record<string, T[]>>((acc, item) => {
    if (!acc[item.entityType]) acc[item.entityType] = [];
    acc[item.entityType].push(item);
    return acc;
  }, {});
}

function groupOrphanedByEntityType(files: OrphanedImageFile[]): Record<string, OrphanedImageFile[]> {
  return files.reduce<Record<string, OrphanedImageFile[]>>((acc, file) => {
    const type = file.relativePath.split('/')[0];
    if (type) {
      if (!acc[type]) acc[type] = [];
      acc[type].push(file);
    }
    return acc;
  }, {});
}

export default function ImageStatsPage() {
  const { stats } = useLoaderData<typeof loader>();
  const navigation = useNavigation();
  const isDeleting = navigation.state === 'submitting' && navigation.formData?.get('intent') === 'delete-orphaned';
  const isResettingLookup = navigation.state === 'submitting' && navigation.formData?.get('intent') === 'reset-lookup';

  const brokenLinksByType = stats ? groupByEntityType<BrokenImageLink>(stats.brokenLinks) : {};
  const lookupFailuresByType = stats ? groupByEntityType<ImageLookupFailure>(stats.imageLookupFailures) : {};
  const orphanedByType = stats ? groupOrphanedByEntityType(stats.orphanedFiles) : {};

  const allEntityTypes = stats
    ? ENTITY_TYPE_ORDER.filter(
        (t) =>
          (stats.filesByEntityType[t] ?? 0) > 0 ||
          (brokenLinksByType[t]?.length ?? 0) > 0 ||
          (lookupFailuresByType[t]?.length ?? 0) > 0 ||
          (orphanedByType[t]?.length ?? 0) > 0
      )
    : [];

  if (!stats) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-3">
          <HardDrive className="h-8 w-8 text-cyan-400" />
          <h1 className="text-3xl font-bold text-white">Image Statistics</h1>
        </div>
        <div className="rounded-lg border border-zinc-700 bg-zinc-800 p-8 text-center">
          <HardDrive className="mx-auto mb-4 h-12 w-12 text-zinc-500" />
          <p className="text-zinc-400">No image statistics available yet. Navigate away and back to refresh.</p>
        </div>
      </div>
    );
  }

  const entityTabs: TabConfig[] = allEntityTypes.map((entityType) => {
    const config = ENTITY_TYPE_CONFIG[entityType];
    const Icon = config?.icon ?? HardDrive;
    const hasOrphaned = (orphanedByType[entityType]?.length ?? 0) > 0;
    const hasBrokenLinks = (brokenLinksByType[entityType]?.length ?? 0) > 0;
    const hasLookupFailures = (lookupFailuresByType[entityType]?.length ?? 0) > 0;
    const fileCount = stats.filesByEntityType[entityType] ?? 0;
    const sizeBytes = stats.sizeByEntityType[entityType] ?? 0;
    const brokenLinks = brokenLinksByType[entityType] ?? [];
    const lookupFailures = lookupFailuresByType[entityType] ?? [];
    const orphanedFiles = orphanedByType[entityType] ?? [];

    return {
      id: entityType,
      activeTopBorderClass: config?.activeTopBorderClass,
      label: (
        <div className="flex items-start justify-between">
          <div className="flex-1">
            <div className="flex items-center gap-2">
              <p className="text-sm font-medium text-zinc-400">{config?.label ?? entityType}</p>
              {hasOrphaned && <AlertTriangle className="h-3.5 w-3.5 text-yellow-400" />}
              {hasBrokenLinks && <Link2Off className="h-3.5 w-3.5 text-red-400" />}
              {hasLookupFailures && <ImageOff className="h-3.5 w-3.5 text-orange-400" />}
            </div>
            <p className="mt-2 text-3xl font-bold text-white">
              {fileCount.toLocaleString()} files ({formatBytes(sizeBytes)})
            </p>
          </div>
          <div
            className={`rounded-lg border p-3 ${config?.iconColorClass ?? 'bg-cyan-500/10 text-cyan-400 border-cyan-500/20'}`}
          >
            <Icon className="h-6 w-6" />
          </div>
        </div>
      ),
      panel: (
        <div className="space-y-6 p-4">
          {/* Broken Links */}
          {brokenLinks.length > 0 && (
            <div>
              <h3 className="mb-2 flex items-center gap-2 text-sm font-semibold text-red-400">
                <Link2Off className="h-4 w-4" />
                Broken Links
                <span className="text-zinc-500">— entities referencing missing image files</span>
              </h3>
              <div className="max-h-64 overflow-y-auto rounded-lg border border-zinc-700">
                <table className="w-full text-sm">
                  <thead className="sticky top-0 bg-zinc-800 text-left text-zinc-400">
                    <tr>
                      <th className="px-4 py-3">Title</th>
                      <th className="px-4 py-3">Missing File</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-zinc-700">
                    {brokenLinks.map((link) => (
                      <tr key={`${link.entityType}-${link.entityId}`} className="bg-zinc-900 hover:bg-zinc-800">
                        <td className="px-4 py-3">
                          <Link to={`/${link.entityType}/${link.entityId}`} className="text-cyan-400 hover:underline">
                            {link.title}
                          </Link>
                        </td>
                        <td className="px-4 py-3 font-mono text-xs text-zinc-500">{link.missingFilePath}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* Lookup Failures */}
          {lookupFailures.length > 0 && (
            <div>
              <div className="mb-2 flex items-center justify-between">
                <h3 className="flex items-center gap-2 text-sm font-semibold text-orange-400">
                  <ImageOff className="h-4 w-4" />
                  Lookup Failures
                  <span className="text-zinc-500">— entities where background image lookup failed</span>
                </h3>
                <Form method="post">
                  <input type="hidden" name="intent" value="reset-lookup" />
                  <input type="hidden" name="entityType" value={entityType} />
                  <input type="hidden" name="entityIds" value={JSON.stringify(lookupFailures.map((f) => f.entityId))} />
                  <button type="submit" disabled={isResettingLookup} className="flex items-center gap-2">
                    <ImageOff className="h-4 w-4" />
                    {isResettingLookup ? 'Resetting...' : 'Reset All'}
                  </button>
                </Form>
              </div>
              <div className="max-h-64 overflow-y-auto rounded-lg border border-zinc-700">
                <table className="w-full text-sm">
                  <thead className="sticky top-0 bg-zinc-800 text-left text-zinc-400">
                    <tr>
                      <th className="px-4 py-3">Title</th>
                      <th className="px-4 py-3">Attempted At</th>
                      <th className="px-4 py-3">Failure Reason</th>
                      <th className="px-4 py-3">Permanent</th>
                      <th className="px-4 py-3"></th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-zinc-700">
                    {lookupFailures.map((failure) => (
                      <tr key={`${failure.entityType}-${failure.entityId}`} className="bg-zinc-900 hover:bg-zinc-800">
                        <td className="px-4 py-3">
                          <Link
                            to={`/${failure.entityType}/${failure.entityId}`}
                            className="text-cyan-400 hover:underline"
                          >
                            {failure.title}
                          </Link>
                        </td>
                        <td className="px-4 py-3 text-zinc-400" suppressHydrationWarning>
                          {new Date(failure.lookupAttemptedAt).toLocaleString()}
                        </td>
                        <td className="px-4 py-3 text-zinc-400">{failure.failureReason ?? '—'}</td>
                        <td className="px-4 py-3">
                          {failure.permanentFailure ? (
                            <span className="rounded-full bg-red-500/20 px-2 py-0.5 text-xs text-red-400">Yes</span>
                          ) : (
                            <span className="rounded-full bg-zinc-700 px-2 py-0.5 text-xs text-zinc-400">No</span>
                          )}
                        </td>
                        <td className="px-4 py-3">
                          <Form method="post">
                            <input type="hidden" name="intent" value="reset-lookup" />
                            <input type="hidden" name="entityType" value={failure.entityType} />
                            <input type="hidden" name="entityIds" value={JSON.stringify([failure.entityId])} />
                            <button type="submit" disabled={isResettingLookup}>
                              Reset
                            </button>
                          </Form>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* Orphaned Files */}
          {orphanedFiles.length > 0 && (
            <div>
              <div className="mb-2 flex items-center justify-between">
                <h3 className="flex items-center gap-2 text-sm font-semibold text-yellow-400">
                  <AlertTriangle className="h-4 w-4" />
                  Orphaned Files
                  <span className="text-zinc-500">— files on disk not referenced by any entity</span>
                </h3>
                <Form method="post">
                  <input type="hidden" name="intent" value="delete-orphaned" />
                  <button type="submit" disabled={isDeleting} className="flex items-center gap-2">
                    <Trash2 className="h-4 w-4" />
                    {isDeleting ? 'Cleaning up...' : 'Clean Up All'}
                  </button>
                </Form>
              </div>
              <div className="max-h-64 overflow-y-auto rounded-lg border border-zinc-700">
                <table className="w-full text-sm">
                  <thead className="sticky top-0 bg-zinc-800 text-left text-zinc-400">
                    <tr>
                      <th className="px-4 py-3">File Path</th>
                      <th className="px-4 py-3">Size</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-zinc-700">
                    {orphanedFiles.map((file) => (
                      <tr key={file.relativePath} className="bg-zinc-900 hover:bg-zinc-800">
                        <td className="px-4 py-3 font-mono text-xs text-zinc-400">{file.relativePath}</td>
                        <td className="px-4 py-3 text-zinc-400">{formatBytes(file.sizeBytes)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {brokenLinks.length === 0 && lookupFailures.length === 0 && orphanedFiles.length === 0 && (
            <p className="text-sm text-zinc-500">No image issues for this entity type.</p>
          )}
        </div>
      ),
    };
  });

  return (
    <div className="space-y-8">
      <div className="flex items-center gap-3">
        <HardDrive className="h-8 w-8 text-cyan-400" />
        <h1 className="text-3xl font-bold text-white">Image Statistics</h1>
      </div>

      <p className="text-sm text-zinc-400" suppressHydrationWarning>
        Last updated: {new Date(stats.lastUpdated).toLocaleString()}
      </p>

      {/* Overview */}
      <div>
        <h2 className="mb-4 text-xl font-semibold text-white">Overview</h2>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <StatCard
            title="Total Files"
            value={stats.totalFiles.toLocaleString()}
            icon={Files}
            colorClass="bg-cyan-500/10 text-cyan-400 border-cyan-500/20"
          />
          <StatCard
            title="Total Size"
            value={formatBytes(stats.totalSizeBytes)}
            icon={HardDrive}
            colorClass="bg-cyan-500/10 text-cyan-400 border-cyan-500/20"
          />
          <StatCard
            title="Orphaned Files"
            value={stats.orphanedFiles.length.toLocaleString()}
            icon={AlertTriangle}
            colorClass={
              stats.orphanedFiles.length > 0
                ? 'bg-yellow-500/10 text-yellow-400 border-yellow-500/20'
                : 'bg-cyan-500/10 text-cyan-400 border-cyan-500/20'
            }
          />
          <StatCard
            title="Broken Links"
            value={stats.brokenLinks.length.toLocaleString()}
            icon={Link2Off}
            colorClass={
              stats.brokenLinks.length > 0
                ? 'bg-red-500/10 text-red-400 border-red-500/20'
                : 'bg-cyan-500/10 text-cyan-400 border-cyan-500/20'
            }
          />
          <StatCard
            title="Lookup Failures"
            value={stats.imageLookupFailures.length.toLocaleString()}
            icon={ImageOff}
            colorClass={
              stats.imageLookupFailures.length > 0
                ? 'bg-orange-500/10 text-orange-400 border-orange-500/20'
                : 'bg-cyan-500/10 text-cyan-400 border-cyan-500/20'
            }
          />
        </div>
      </div>

      {/* Per Entity Type Tabs */}
      {entityTabs.length > 0 && (
        <div>
          <h2 className="mb-4 text-xl font-semibold text-white">By Entity Type</h2>
          <Tabs tabs={entityTabs} />
        </div>
      )}
    </div>
  );
}
