
import type { LoaderFunctionArgs } from "@remix-run/node";
import { useLoaderData } from "@remix-run/react";
import { searchEntities } from "~/entity-data";
import { Entity } from "~/constants";
import { BookEntity, MovieEntity } from "~/models";
import { toTitleCase } from "~/helpers";
import Books from "./books";
import Movies from "./movies";

async function search(entity: Entity, searchText: string | null) {
  if (entity === Entity.Books) {
    return await searchEntities<BookEntity>(Entity.Books, searchText);
  } else if (entity === Entity.Movies) {
    return await searchEntities<MovieEntity>(Entity.Movies, searchText);
  }
}

export const loader = async ({ request, params }: LoaderFunctionArgs) => {
  const url = new URL(request.url);
  const searchText = url.searchParams.get("searchText");
  const entity: Entity = Entity[toTitleCase(params.entity) as keyof typeof Entity];
  const entityResult = await search(entity, searchText);

  return { entities: entityResult, entity };
};

export default function Index() {
  const { entities, entity } = useLoaderData<typeof loader>();

  if (entity === Entity.Books) {
    return <Books books={entities as BookEntity[]} />;
  }

  if (entity === Entity.Movies) {
    return <Movies movies={entities as MovieEntity[]} />;
  }
}