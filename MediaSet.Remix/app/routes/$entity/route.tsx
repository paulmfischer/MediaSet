
import type { LoaderFunctionArgs } from "@remix-run/node";
import { Form, Link, Outlet, useLoaderData, useSubmit } from "@remix-run/react";
import { useEffect } from "react";
import { Plus, X } from "lucide-react";
import { toTitleCase } from "~/helpers";
import { Entity } from "~/models";

export const loader = ({ request, params }: LoaderFunctionArgs) => {
  const url = new URL(request.url);
  const searchText = url.searchParams.get("searchText");
  const entity: Entity = Entity[toTitleCase(params.entity) as keyof typeof Entity];
  return { searchText, entity };
};

export default function Index() {
  const { entity, searchText } = useLoaderData<typeof loader>();
  const submit = useSubmit();

  useEffect(() => {
    const searchField = document.getElementById("search");
    if (searchField instanceof HTMLInputElement) {
      searchField.value = searchText || '';
    }
  }, [searchText]);

  return (
    <div className="flex flex-col">
      <div className="flex flex-col sm:flex-row gap-2 sm:gap-0 sm:items-center justify-between">
        <div className="flex flex-row gap-4 items-end">
          <h2 className="text-2xl">{entity}</h2>
        </div>
        <div className="flex flex-col sm:flex-row gap-2 sm:gap-6 sm:items-center">
          <Link to="/books/add" className="flex gap-1 items-center"><Plus size={18} /> Add</Link>
          <Form id="search-form" role="search" className="flex gap-2">
            <div className="flex gap-2 z-20 bg-white rounded-sm">
              <input
                id="search"
                type="search"
                defaultValue={searchText || ''}
                placeholder="Search Books"
                aria-label="Search Books"
                name="searchText"
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