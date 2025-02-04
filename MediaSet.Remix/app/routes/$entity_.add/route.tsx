import type { MetaFunction, ActionFunctionArgs, LoaderFunctionArgs } from "@remix-run/node";
import { Form, redirect, useLoaderData, useNavigate, useNavigation } from "@remix-run/react";
import { addEntity } from "~/entity-data";
import Spinner from "~/components/spinner";
import { getAuthors, getFormats, getGenres, getPublishers, getStudios } from "~/metadata-data";
import { formToDto, getEntityFromParams, singular } from "~/helpers";
import { Entity } from "~/models";
import BookForm from "../../components/book-form";
import MovieForm from "~/components/movie-form";
import invariant from "tiny-invariant";

export const meta: MetaFunction<typeof loader> = ({ params }) => {
  const entityName = getEntityFromParams(params);
  return [
    { title: `Add a ${singular(entityName)}` },
    { name: "description", content: `Add a ${singular(entityName)}` },
  ];
};

export const loader = async ({ params }: LoaderFunctionArgs) => {
  const entityName = getEntityFromParams(params);
  const [authors, genres, publishers, formats, studios] = await Promise.all([getAuthors(), getGenres(entityName), getPublishers(), getFormats(entityName), getStudios()]);
  return { authors, genres, publishers, formats, entityName, studios };
}

export const action = async ({ request, params }: ActionFunctionArgs) => {
  invariant(params.entity, "Missing entity param");
  const entityName = getEntityFromParams(params);
  const formData = await request.formData();
  const entity = formToDto(entityName, formData);
  const newEntity = await addEntity(entity);

  return redirect(`/${entityName.toLowerCase()}/${newEntity.id}`);
};

export default function Add() {
  const { authors, genres, publishers, formats, entityName, studios } = useLoaderData<typeof loader>();
  const navigate = useNavigate();
  const navigation = useNavigation();
  const isSubmitting = navigation.location?.pathname === `/${entityName.toLowerCase()}/add`;
  const formId = `add-${singular(entityName)}`;
  const actionUrl = `/${entityName.toLowerCase()}/add`;
  
  let formComponent;
  if (entityName === Entity.Books) {
    formComponent = <BookForm authors={authors} genres={genres} publishers={publishers} formats={formats} isSubmitting={isSubmitting} />;
  } else if (entityName === Entity.Movies) {
    formComponent = <MovieForm genres={genres} studios={studios} formats={formats} isSubmitting={isSubmitting} />
  }

  return (
    <div className="flex flex-col">
      <div className="flex flex-row items-center justify-between">
        <div className="flex flex-row gap-4 items-end">
          <h2 className="text-2xl">Add a {singular(entityName)}</h2>
        </div>
      </div>
      <div className="h-full mt-4">
        <div className="mt-4 flex flex-col gap-2">
          <Form id={formId} method="post" action={actionUrl}>
            {formComponent}
            <div className="flex flex-row gap-2 mt-3">
              <button type="submit" className="flex flex-row gap-2" disabled={isSubmitting}>
                {isSubmitting ? <div className="flex items-center"><Spinner /></div> : null}
                Add
              </button>
              <button type="button" onClick={() => navigate(-1)} className="secondary" disabled={isSubmitting}>Cancel</button>
            </div>
          </Form>
        </div>
      </div>
    </div>
  );
}