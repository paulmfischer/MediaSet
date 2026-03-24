import { type ActionFunctionArgs, type MetaFunction } from '@remix-run/node';
import { useLoaderData, Link, Form, useNavigation } from '@remix-run/react';
import { getImageStats, type ImageLookupFailure } from '~/api/image-stats-data';
import { deleteOrphanedImages, resetImageLookup } from '~/api/image-management-data';
import StatCard from '~/components/stat-card';
import { HardDrive, Files, AlertTriangle, Link2Off, Trash2, ImageOff } from 'lucide-react';

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

function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(1024));
  return `${(bytes / Math.pow(1024, i)).toFixed(1)} ${units[i]}`;
}

function groupLookupFailuresByType(failures: ImageLookupFailure[]): Record<string, ImageLookupFailure[]> {
  return failures.reduce<Record<string, ImageLookupFailure[]>>((acc, failure) => {
    const key = failure.entityType;
    if (!acc[key]) acc[key] = [];
    acc[key].push(failure);
    return acc;
  }, {});
}

export default function ImageStatsPage() {
  const { stats } = useLoaderData<typeof loader>();
  const navigation = useNavigation();
  const isDeleting = navigation.state === 'submitting' && navigation.formData?.get('intent') === 'delete-orphaned';
  const isResettingLookup = navigation.state === 'submitting' && navigation.formData?.get('intent') === 'reset-lookup';

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

  const entityTypes = Object.entries(stats.filesByEntityType);

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

      {/* Per Entity Type */}
      {entityTypes.length > 0 && (
        <div>
          <h2 className="mb-4 text-xl font-semibold text-white">By Entity Type</h2>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {entityTypes.map(([type, count]) => (
              <StatCard
                key={type}
                title={`${type.charAt(0).toUpperCase()}${type.slice(1)}`}
                value={`${count.toLocaleString()} files (${formatBytes(stats.sizeByEntityType[type] ?? 0)})`}
                icon={HardDrive}
                colorClass="bg-cyan-500/10 text-cyan-400 border-cyan-500/20"
              />
            ))}
          </div>
        </div>
      )}

      {/* Broken Links */}
      {stats.brokenLinks.length > 0 && (
        <div>
          <h2 className="mb-4 text-xl font-semibold text-white">
            <span className="text-red-400">Broken Links</span>
            <span className="ml-2 text-sm font-normal text-zinc-400">— entities referencing missing image files</span>
          </h2>
          <div className="overflow-x-auto rounded-lg border border-zinc-700">
            <table className="w-full text-sm">
              <thead className="bg-zinc-800 text-left text-zinc-400">
                <tr>
                  <th className="px-4 py-3">Type</th>
                  <th className="px-4 py-3">Title</th>
                  <th className="px-4 py-3">Missing File</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-zinc-700">
                {stats.brokenLinks.map((link) => (
                  <tr key={`${link.entityType}-${link.entityId}`} className="bg-zinc-900 hover:bg-zinc-800">
                    <td className="px-4 py-3 capitalize text-zinc-400">{link.entityType}</td>
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

      {/* Image Lookup Failures */}
      {stats.imageLookupFailures.length > 0 && (
        <div>
          <h2 className="mb-4 text-xl font-semibold text-white">
            <span className="text-orange-400">Image Lookup Failures</span>
            <span className="ml-2 text-sm font-normal text-zinc-400">
              — entities where background image lookup failed
            </span>
          </h2>
          {Object.entries(groupLookupFailuresByType(stats.imageLookupFailures)).map(([entityType, failures]) => (
            <div key={entityType} className="mb-6">
              <div className="mb-2 flex items-center justify-between">
                <h3 className="text-base font-medium capitalize text-zinc-300">{entityType}</h3>
                <Form method="post">
                  <input type="hidden" name="intent" value="reset-lookup" />
                  <input type="hidden" name="entityType" value={entityType} />
                  <input type="hidden" name="entityIds" value={JSON.stringify(failures.map((f) => f.entityId))} />
                  <button type="submit" disabled={isResettingLookup} className="flex items-center gap-2">
                    <ImageOff className="h-4 w-4" />
                    {isResettingLookup ? 'Resetting...' : 'Reset All'}
                  </button>
                </Form>
              </div>
              <div className="overflow-x-auto rounded-lg border border-zinc-700">
                <table className="w-full text-sm">
                  <thead className="bg-zinc-800 text-left text-zinc-400">
                    <tr>
                      <th className="px-4 py-3">Title</th>
                      <th className="px-4 py-3">Attempted At</th>
                      <th className="px-4 py-3">Failure Reason</th>
                      <th className="px-4 py-3">Permanent</th>
                      <th className="px-4 py-3"></th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-zinc-700">
                    {failures.map((failure) => (
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
          ))}
        </div>
      )}

      {/* Orphaned Files */}
      {stats.orphanedFiles.length > 0 && (
        <div>
          <div className="mb-4 flex items-center justify-between">
            <h2 className="text-xl font-semibold text-white">
              <span className="text-yellow-400">Orphaned Files</span>
              <span className="ml-2 text-sm font-normal text-zinc-400">
                — files on disk not referenced by any entity
              </span>
            </h2>
            <Form method="post">
              <input type="hidden" name="intent" value="delete-orphaned" />
              <button
                type="submit"
                disabled={isDeleting}
                className="flex items-center gap-2 rounded-md bg-yellow-600 px-3 py-2 text-sm font-medium text-white hover:bg-yellow-700 disabled:opacity-50"
              >
                <Trash2 className="h-4 w-4" />
                {isDeleting ? 'Cleaning up...' : 'Clean Up Orphaned Files'}
              </button>
            </Form>
          </div>
          <div className="overflow-x-auto rounded-lg border border-zinc-700">
            <table className="w-full text-sm">
              <thead className="bg-zinc-800 text-left text-zinc-400">
                <tr>
                  <th className="px-4 py-3">File Path</th>
                  <th className="px-4 py-3">Size</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-zinc-700">
                {stats.orphanedFiles.map((file) => (
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
    </div>
  );
}
