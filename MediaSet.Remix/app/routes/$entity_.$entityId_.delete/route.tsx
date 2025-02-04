import type { ActionFunctionArgs } from "@remix-run/node";
import { redirect } from "@remix-run/node";
import invariant from "tiny-invariant"
import { deleteEntity } from "~/entity-data";
import { getEntityFromParams } from "~/helpers";

export const action = async ({ params }: ActionFunctionArgs) => {
  invariant(params.entity, "Missing entity param");
  invariant(params.entityId, "Missing entityId param");
  const entityName = getEntityFromParams(params);
  await deleteEntity(entityName, params.entityId);
  return redirect(`/${entityName.toLowerCase()}`);
};