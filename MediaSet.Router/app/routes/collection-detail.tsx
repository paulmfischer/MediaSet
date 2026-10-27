import { Link, useLoaderData, useLocation } from 'react-router';
import type { Route } from './+types/collection-detail';
import { readEntity } from '~/api/collections.server';
import { publicApiUrl } from '~/config.server';
import { duration, fieldValue, getCollection } from '~/utils/collections';
import ImageDisplay from '~/components/images/image-display';

export function meta({ data }: Route.MetaArgs) {
  return [{ title: data?.entity.title ?? 'Media detail' }];
}

export async function loader({ request, params }: Route.LoaderArgs) {
  const collection = getCollection(params.entity);
  const entity = await readEntity(collection.type, params.entityId, request.signal);
  return { entity, entityType: collection.type, apiUrl: publicApiUrl() };
}

export default function CollectionDetail() {
  const { entity, entityType, apiUrl } = useLoaderData<typeof loader>();
  const collection = getCollection(entityType.toLowerCase());
  const { search } = useLocation();
  const canonicalSearch = new URLSearchParams(search).toString();
  return (
    <div className={`entity-${entityType.toLowerCase()} flex flex-col`}>
      <Link to={`/${entityType.toLowerCase()}${canonicalSearch ? `?${canonicalSearch}` : ''}`} className="mb-2">
        Back to {entityType}
      </Link>
      <div className="flex flex-row items-center justify-between border-b-2 border-slate-600 pb-2">
        <div className="flex flex-col gap-2 items-baseline">
          <h2 className="text-2xl">{entity.title}</h2>
          {entity.subtitle && <h4 className="text-sm">{entity.subtitle}</h4>}
        </div>
      </div>
      <div className="h-full mt-4">
        <div className="flex flex-col lg:flex-row lg:gap-3">
          <div className="lg:basis-1/5 flex justify-center mb-4 lg:mb-0">
            <ImageDisplay
              key={entity.id}
              imageData={entity.coverImage}
              alt={entity.title ?? 'Media cover'}
              entityType={entityType}
              entityId={entity.id}
              apiUrl={apiUrl}
            />
          </div>
          <div className="lg:basis-4/5">
            <dl>
              {collection.fields.map((field) => (
                <div key={field.key} className="flex flex-col md:flex-row mb-2 md:mb-0">
                  <dt className="basis-2/12 dark:text-slate-400">{field.label}</dt>
                  <dd className="grow basis-3/4">{fieldValue(entity, field.key)}</dd>
                </div>
              ))}
            </dl>
            {entity.discList && entity.discList.length > 0 && (
              <div className="flex flex-col md:flex-row mb-2 md:mb-0 mt-4">
                <span className="basis-2/12 dark:text-slate-400">Disc List</span>
                <div className="grow">
                  <table className="w-full text-sm">
                    <thead className="border-b border-gray-600">
                      <tr>
                        <th className="text-left py-1 px-2">Track #</th>
                        <th className="text-left py-1 px-2">Title</th>
                        <th className="text-left py-1 px-2">Duration</th>
                      </tr>
                    </thead>
                    <tbody>
                      {entity.discList.map((track, index) => (
                        <tr key={index} className="border-b border-gray-700">
                          <td className="py-1 px-2">{track.trackNumber}</td>
                          <td className="py-1 px-2">{track.title}</td>
                          <td className="py-1 px-2">{duration(track.duration)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}
            {entity.imageLookup?.failureReason && (
              <p className="mt-4 text-sm text-gray-400">Image lookup: {entity.imageLookup.failureReason}</p>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
