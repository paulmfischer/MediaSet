import type { LoaderFunctionArgs, MetaFunction } from "@remix-run/node";
import { Form, Link, Outlet, useLoaderData, useSubmit } from "@remix-run/react";
import { useEffect } from "react";
import { Plus, X } from "lucide-react";
import { getEntityFromParams, toTitleCase } from "~/helpers";
import { Entity } from "~/models";
import invariant from "tiny-invariant";

export const meta: MetaFunction = ({ params }) => {
  const entityType = getEntityFromParams(params);
  return [
    { title: entityType },
    { name: "description", content: `List of ${entityType.toLowerCase()}` },
  ];
};

export const loader = ({ request, params }: LoaderFunctionArgs) => {
  invariant(params.entity, "Missing entity param");
  const url = new URL(request.url);
  const searchText = url.searchParams.get("searchText");
  const entityName: Entity = Entity[toTitleCase(params.entity) as keyof typeof Entity];
  return { searchText, entityName };
};

export default function Index() {
  const { entityName, searchText } = useLoaderData<typeof loader>();
  const submit = useSubmit();

  useEffect(() => {
    const searchField = document.getElementById("search");
    if (searchField instanceof HTMLInputElement) {
      searchField.value = searchText || '';
    }
  }, [searchText]);

  return (
    <div className="flex flex-col px-2">
      <div className="flex flex-col sm:flex-row gap-2 sm:gap-0 sm:items-center justify-between">
        <div className="flex flex-row gap-4 items-end">
          <h2 className="text-2xl">{entityName}</h2>
        </div>
        <div className="flex flex-col sm:flex-row gap-2 sm:gap-6 sm:items-center">
          <Link to={`/${entityName.toLowerCase()}/add`} className="flex gap-1 items-center"><Plus size={18} /> Add</Link>
          <Form id="search-form" role="search" className="flex gap-2">
            <div className="flex gap-2 z-20 bg-gray-800 rounded-md">
              <input
                id="search"
                type="search"
                defaultValue={searchText || ''}
                placeholder={`Search ${entityName}`}
                aria-label={`Search ${entityName}`}
                name="searchText"
                className="w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400"
              />
              {searchText && 
                <button className="text-icon"
                  onClick={() => {
                    const searchEl = document.getElementById('search') as HTMLInputElement;
                    if (searchEl) {
                      searchEl.value = '';
                      submit(searchEl);
                    }
                  }}
                >
                  <X />
                </button>
              }
            </div>
            <button type="submit" aria-label="Search">Search</button>
          </Form>
        </div>
      </div>
      <div className="h-full mt-4">
        <Outlet />
      </div>
    </div>
  );
};