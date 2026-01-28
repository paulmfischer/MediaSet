import type { ActionFunctionArgs } from "@remix-run/node";
import { redirect } from "@remix-run/node";
import invariant from "tiny-invariant"
import { deleteEntity } from "~/entity-data";
import { getEntityFromParams } from "~/helpers";
import { serverLogger } from "~/utils/serverLogger";

export const action = async ({ params }: ActionFunctionArgs) => {
  invariant(params.entity, "Missing entity param");
  invariant(params.entityId, "Missing entityId param");
  const entityType = getEntityFromParams(params);
  
  try {
    await deleteEntity(entityType, params.entityId);
    return redirect(`/${entityType.toLowerCase()}`);
  } catch (error) {
    throw error;
  }
};