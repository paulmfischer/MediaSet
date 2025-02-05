import type { LoaderFunctionArgs, MetaFunction } from "@remix-run/node";
import { useLoaderData } from "@remix-run/react";
import { searchEntities } from "~/entity-data";
import { BookEntity, Entity, MovieEntity } from "~/models";
import { getEntityFromParams } from "~/helpers";
import Books from "./books";
import Movies from "./movies";
import invariant from "tiny-invariant";

export const meta: MetaFunction = (loader) => {
  const entityType = getEntityFromParams(loader.params);
  return [
    { title: `${entityType} List` },
    { name: "description", content: `${entityType} List` },
  ];
};

export const loader = async ({ request, params }: LoaderFunctionArgs) => {
  invariant(params.entity, "Missing entity param");
  const url = new URL(request.url);
  const searchText = url.searchParams.get("searchText");
  const entityType: Entity = getEntityFromParams(params);
  const entities = await searchEntities(entityType, searchText);

  return { entities, entityType };
};

export default function Index() {
  const { entities, entityType } = useLoaderData<typeof loader>();
  
  if (entities == null || entities.length === 0) {
    return (
      <div className="flex justify-center text-center">
        You don't appear to have any {entityType}!<br />
        Add to your collection by clicking the 'Add' link up above!
      </div>
    );
  }

  if (entityType === Entity.Books) {
    return <Books books={entities as BookEntity[]} />;
  }

  if (entityType === Entity.Movies) {
    return <Movies movies={entities as MovieEntity[]} />;
  }
}