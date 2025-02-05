import type { MetaFunction, ActionFunctionArgs, LoaderFunctionArgs } from "@remix-run/node";
import { Form, redirect, useLoaderData, useNavigate, useNavigation } from "@remix-run/react";
import { addEntity, getEntity, updateEntity } from "~/entity-data";
import Spinner from "~/components/spinner";
import { getAuthors, getFormats, getGenres, getPublishers, getStudios } from "~/metadata-data";
import { formToDto, getEntityFromParams, singular } from "~/helpers";
import { BookEntity, Entity, MovieEntity } from "~/models";
import BookForm from "../../components/book-form";
import MovieForm from "~/components/movie-form";
import invariant from "tiny-invariant";

export const meta: MetaFunction<typeof loader> = ({ params }) => {
  const entityType = getEntityFromParams(params);
  return [
    { title: `Add a ${singular(entityType)}` },
    { name: "description", content: `Add a ${singular(entityType)}` },
  ];
};

export const loader = async ({ params }: LoaderFunctionArgs) => {
  invariant(params.entity, "Missing entity param");
  invariant(params.entityId, "Missing entityId param");
  const entityType = getEntityFromParams(params);
  const [entity, authors, genres, publishers, formats, studios] =
   await Promise.all([getEntity(entityType, params.entityId), getAuthors(), getGenres(entityType), getPublishers(), getFormats(entityType), getStudios()]);
  return { entity, authors, genres, publishers, formats, entityType, studios };
}

export const action = async ({ request, params }: ActionFunctionArgs) => {
  invariant(params.entity, "Missing entity param");
  invariant(params.entityId, "Missing entityId param");
  const entityType = getEntityFromParams(params);
  const formData = await request.formData();
  const entity = formToDto(formData);
  if (entity) {
    await updateEntity(params.entityId, entity);
    return redirect(`/${entityType.toLowerCase()}/${entity.id}`);
  } else {
    return { invalidObject: `Failed to convert form to a ${entityType}` };
  }
};

export default function Edit() {
  const { entity, authors, genres, publishers, formats, entityType, studios } = useLoaderData<typeof loader>();
  const navigate = useNavigate();
  const navigation = useNavigation();
  const isSubmitting = navigation.location?.pathname === `/${entityType.toLowerCase()}/${entity.id}/edit`;
  const formId = `edit-${singular(entityType)}`;
  const actionUrl = `/${entityType.toLowerCase()}/${entity.id}/edit`;
  
  let formComponent;
  if (entityType === Entity.Books) {
    formComponent = <BookForm book={entity as BookEntity} authors={authors} genres={genres} publishers={publishers} formats={formats} isSubmitting={isSubmitting} />;
  } else if (entityType === Entity.Movies) {
    formComponent = <MovieForm movie={entity as MovieEntity} genres={genres} studios={studios} formats={formats} isSubmitting={isSubmitting} />
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