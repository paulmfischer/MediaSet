import type { LoaderFunctionArgs, MetaFunction } from "@remix-run/node";
import { Form, Link, useLoaderData, useNavigate } from "@remix-run/react";
import { useEffect } from "react";
import { Plus, X } from "lucide-react";
import { searchEntities } from "~/api/entity-data";
import { BookEntity, Entity, GameEntity, MovieEntity, MusicEntity } from "~/models";
import { getEntityFromParams } from "~/utils/helpers";
import { clientApiUrl } from "~/constants.server";
import Books from "./components/books";
import Movies from "./components/movies";
import Games from "./components/games";
import Musics from "./components/musics";
import invariant from "tiny-invariant";

export const meta: MetaFunction = (loader) => {
  const entityType = getEntityFromParams(loader.params);
  return [{ title: `${entityType} List` }, { name: "description", content: `${entityType} List` }];
};

export const loader = async ({ request, params }: LoaderFunctionArgs) => {
  invariant(params.entity, "Missing entity param");
  const url = new URL(request.url);
  const searchText = url.searchParams.get("searchText");
  const entityType: Entity = getEntityFromParams(params);

  const entities = await searchEntities(entityType, searchText);
  const apiUrl = clientApiUrl;
  return { entities, entityType, searchText, apiUrl };
};

export default function Index() {
  const { entities, entityType, searchText, apiUrl } = useLoaderData<typeof loader>();
  const navigate = useNavigate();

  useEffect(() => {
    const searchField = document.getElementById("search");
    if (searchField instanceof HTMLInputElement) {
      searchField.value = searchText || "";
    }
  }, [searchText]);

  return (
    <div className="flex flex-col px-2">
      <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex flex-row items-center gap-2">
          <h2 className="text-2xl mb-1 sm:mb-0">{entityType}</h2>
          <Link to={`/${entityType.toLowerCase()}/add`} className="flex gap-1 items-center">
            <Plus size={18} /> Add
          </Link>
        </div>
        <div className="flex flex-row w-full sm:w-auto gap-2 items-center">
          <Form id="search-form" role="search" className="flex flex-1 sm:flex-none gap-2">
            <div className="flex flex-1 gap-0 z-20">
              <input
                id="search"
                type="search"
                defaultValue={searchText || ""}
                placeholder={`Search ${entityType}`}
                aria-label={`Search ${entityType}`}
                name="searchText"
                className={`w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400 ${searchText ? "border-r-0 rounded-l-md rounded-r-none" : "rounded-md"}`}
              />
              {searchText && (
                <button
                  type="button"
                  className="text-icon rounded-r-md"
                  aria-label="Clear search"
                  title="Clear search"
                  onClick={() => {
                    const searchEl = document.getElementById("search") as HTMLInputElement;
                    if (searchEl) {
                      searchEl.value = "";
                    }
                    navigate(`/${entityType.toLowerCase()}`);
                  }}
                >
                  <X />
                </button>
              )}
            </div>
            <button type="submit" aria-label="Search">
              Search
            </button>
          </Form>
        </div>
      </div>
      <div className="h-full mt-4">
        {entities == null || entities.length === 0 ? (
          <div className="flex justify-center text-center">
            You don&apos;t appear to have any {entityType}!<br />
            Add to your collection by clicking the &apos;Add&apos; link up above!
          </div>
        ) : (
          <>
            {entityType === Entity.Books && <Books books={entities as BookEntity[]} apiUrl={apiUrl} />}
            {entityType === Entity.Movies && <Movies movies={entities as MovieEntity[]} apiUrl={apiUrl} />}
            {entityType === Entity.Games && <Games games={entities as GameEntity[]} apiUrl={apiUrl} />}
            {entityType === Entity.Musics && <Musics musics={entities as MusicEntity[]} apiUrl={apiUrl} />}
          </>
        )}
      </div>
    </div>
  );
}
