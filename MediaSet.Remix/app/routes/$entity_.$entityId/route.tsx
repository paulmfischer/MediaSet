import type { LoaderFunctionArgs, MetaFunction } from "@remix-run/node";
import invariant from "tiny-invariant";
import { useLoaderData } from "@remix-run/react";
import { getEntity } from "~/entity-data";
import { BaseEntity, BookEntity, Entity, MovieEntity } from "~/models";
import { getEntityFromParams, singular } from "~/helpers";
import Book from "./book";
import Movie from "./movie";

export const meta: MetaFunction<typeof loader> = ({ params, data }) => {
  const entityType = getEntityFromParams(params);
  const entityTitle = (data?.entity as BaseEntity).title ?? entityType;
  return [
    { title: `${entityTitle}` },
    { name: "description", content: `${singular(entityType)} Detail: ${entityTitle}` },
  ];
};

export const loader = async ({ params }: LoaderFunctionArgs) => {
  invariant(params.entity, "Missing entity param");
  invariant(params.entityId, "Missing entityId param");
  const entityType: Entity = getEntityFromParams(params);
  const entity = await getEntity(entityType, params.entityId);
  return { entity, entityType };
};

export default function Detail() {
  const { entity, entityType } = useLoaderData<typeof loader>();

  if (entityType === Entity.Books) {
    return <Book book={entity as BookEntity} />;
  }
  
  if (entityType === Entity.Movies) {
    return <Movie movie={entity as MovieEntity} />;
  }
}