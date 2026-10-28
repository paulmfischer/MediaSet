import type { ActionFunctionArgs } from 'react-router';
import { redirect } from 'react-router';
import invariant from 'tiny-invariant';
import { deleteEntity } from '~/api/entity-data';
import { getEntityFromParams } from '~/utils/helpers';
export const action = async ({ params }: ActionFunctionArgs) => {
  invariant(params.entity, 'Missing entity param');
  invariant(params.entityId, 'Missing entityId param');
  const entityType = getEntityFromParams(params);

  await deleteEntity(entityType, params.entityId);
  return redirect(`/${entityType.toLowerCase()}`);
};
