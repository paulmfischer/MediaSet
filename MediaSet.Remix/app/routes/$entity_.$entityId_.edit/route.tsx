import type { MetaFunction, ActionFunctionArgs, LoaderFunctionArgs } from "@remix-run/node";
import { Form, redirect, useLoaderData, useNavigate, useNavigation } from "@remix-run/react";
import { addEntity, getEntity } from "~/entity-data";
import Spinner from "~/components/spinner";
import { getAuthors, getFormats, getGenres, getPublishers } from "~/metadata-data";
import { formToDto, getEntityFromParams, singular } from "~/helpers";
import { BookEntity, Entity, MovieEntity } from "~/models";
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
  invariant(params.entity, "Missing entity param");
  invariant(params.entityId, "Missing entityId param");
  const entityName = getEntityFromParams(params);
  const [entity, authors, genres, publishers, formats] =
   await Promise.all([getEntity(entityName, params.entityId), getAuthors(), getGenres(), getPublishers(), getFormats()]);
  return { entity, authors, genres, publishers, formats, entityName };
}

export const action = async ({ request, params }: ActionFunctionArgs) => {
  invariant(params.entity, "Missing entity param");
  invariant(params.entityId, "Missing entityId param");
  const entityName = getEntityFromParams(params);
  const formData = await request.formData();
  const entity = formToDto(entityName, formData);
  const newEntity = await addEntity(entity);

  return redirect(`/${entityName.toLowerCase()}/${newEntity.id}`);
};

export default function Edit() {
  const { entity, authors, genres, publishers, formats, entityName } = useLoaderData<typeof loader>();
  const navigate = useNavigate();
  const navigation = useNavigation();
  const isSubmitting = navigation.location?.pathname === `/${entityName.toLowerCase()}/${entity.id}/edit`;
  const formId = `edit-${singular(entityName)}`;
  const actionUrl = `/${entityName.toLowerCase()}/${entity.id}/edit`;
  
  let formComponent;
  if (entityName === Entity.Books) {
    formComponent = <BookForm book={entity as BookEntity} authors={authors} genres={genres} publishers={publishers} formats={formats} isSubmitting={isSubmitting} />;
  } else if (entityName === Entity.Movies) {
    formComponent = <MovieForm movie={entity as MovieEntity} genres={genres} studios={[]} formats={formats} isSubmitting={isSubmitting} />
  }

  return (
    <div className="flex flex-col">
      <div className="flex flex-row items-center justify-between">
        <div className="flex flex-row gap-4 items-end">
          <h2 className="text-2xl">Editing {entity.title}</h2>
        </div>
      </div>
      <div className="h-full mt-4">
        <div className="mt-4 flex flex-col gap-2">
          <Form id={formId} method="post" action={actionUrl}>
            {formComponent}
            <div className="flex flex-row gap-2 mt-3">
              <button type="submit" className="flex flex-row gap-2" disabled={isSubmitting}>
                {isSubmitting ? <div className="flex items-center"><Spinner /></div> : null}
                Update
              </button>
              <button type="button" onClick={() => navigate(-1)} className="secondary" disabled={isSubmitting}>Cancel</button>
            </div>
          </Form>
        </div>
      </div>
    </div>
  );
}