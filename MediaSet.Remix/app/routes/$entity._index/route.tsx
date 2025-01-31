
import type { LoaderFunctionArgs } from "@remix-run/node";
import { useLoaderData } from "@remix-run/react";
import { searchEntities } from "~/entity-data";
import { BookEntity, Entity, MovieEntity } from "~/models";
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
  console.log('entity result', entityResult?.length);

  return { entities: entityResult, entity };
};

export default function Index() {
  const { entities, entity } = useLoaderData<typeof loader>();
  
  if (entities == null || entities.length === 0) {
    return (
      <div className="flex justify-center text-center">
        You don't appear to have any {entity}!<br />
        Add to your collection by clicking the 'Add' link up above!
      </div>
    );
  }

  if (entity === Entity.Books) {
    return <Books books={entities as BookEntity[]} />;
  }

  if (entity === Entity.Movies) {
    return <Movies movies={entities as MovieEntity[]} />;
  }
}