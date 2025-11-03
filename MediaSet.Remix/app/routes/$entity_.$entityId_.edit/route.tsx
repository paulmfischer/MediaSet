import type { MetaFunction, ActionFunctionArgs, LoaderFunctionArgs } from "@remix-run/node";
import { Form, redirect, useActionData, useLoaderData, useNavigate, useNavigation } from "@remix-run/react";
import { addEntity, getEntity, updateEntity } from "~/entity-data";
import Spinner from "~/components/spinner";
import { getAuthors, getFormats, getGenres, getPublishers, getStudios, getDevelopers, getLabels, getGamePublishers, getPlatforms } from "~/metadata-data";
import { formToDto, getEntityFromParams, singular } from "~/helpers";
import { BookEntity, Entity, GameEntity, MovieEntity, MusicEntity } from "~/models";
import BookForm from "../../components/book-form";
import MovieForm from "~/components/movie-form";
import GameForm from "~/components/game-form";
import MusicForm from "~/components/music-form";
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
  const entity = await getEntity(entityType, params.entityId);
  const [genres, formats, authors, publishers, studios, developers, labels, platforms] =
   await Promise.all([
    getGenres(entityType),
    getFormats(entityType),
    entity.type === Entity.Books ? getAuthors() : Promise.resolve([]),
    entity.type === Entity.Books ? getPublishers() : entity.type === Entity.Games ? getGamePublishers() : Promise.resolve([]),
    entity.type === Entity.Movies ? getStudios() : Promise.resolve([]),
    entity.type === Entity.Games ? getDevelopers() : Promise.resolve([]),
    entity.type === Entity.Musics ? getLabels() : Promise.resolve([]),
    entity.type === Entity.Games ? getPlatforms() : Promise.resolve([])
  ]);
  return { entity, authors, genres, publishers, formats, entityType, studios, developers, labels, platforms };
}

export const action = async ({ request, params }: ActionFunctionArgs) => {
  invariant(params.entity, "Missing entity param");
  invariant(params.entityId, "Missing entityId param");
  const entityType = getEntityFromParams(params);
  const formData = await request.formData();
  const intent = formData.get("intent") as string;

  if (intent === "lookup") {
    const fieldName = formData.get("fieldName") as string;
    const identifierValue = formData.get("identifierValue") as string;
    if (!identifierValue) {
      return { error: { lookup: "Identifier value is required" } };
    }
    const { lookup, getIdentifierTypeForField } = await import("~/lookup-data.server");
    const identifierType = getIdentifierTypeForField(entityType, fieldName);
    const lookupResult = await lookup(entityType, identifierType, identifierValue);
    return { lookupResult, identifierValue, fieldName };
  }

  const entity = formToDto(formData);
  if (entity) {
    await updateEntity(params.entityId, entity);
    return redirect(`/${entityType.toLowerCase()}/${entity.id}`);
  } else {
    return { invalidObject: `Failed to convert form to a ${entityType}` };
  }
};

export default function Edit() {
  const { entity, authors, genres, publishers, formats, entityType, studios, developers, labels, platforms } = useLoaderData<typeof loader>();
  const actionData = useActionData<typeof action>();
  const navigate = useNavigate();
  const navigation = useNavigation();
  const isSubmitting = navigation.location?.pathname === `/${entity.type.toLowerCase()}/${entity.id}/edit`;
  const formId = `edit-${singular(entity.type)}`;
  const actionUrl = `/${entity.type.toLowerCase()}/${entity.id}/edit`;
  const isLookupError = (r: any): r is { message: string; statusCode: number } => r && typeof r.message === 'string' && typeof r.statusCode === 'number';
  const lookupResult = actionData && 'lookupResult' in actionData ? (actionData as any).lookupResult : undefined;
  const lookupEntity = lookupResult && !isLookupError(lookupResult) ? lookupResult : undefined;
  const lookupError = lookupResult && isLookupError(lookupResult) ? lookupResult.message : undefined;
  
  let formComponent;
  if (entity.type === Entity.Books) {
    formComponent = <BookForm book={(lookupEntity as BookEntity) ?? (entity as BookEntity)} authors={authors} genres={genres} publishers={publishers} formats={formats} isSubmitting={isSubmitting} />;
  } else if (entity.type === Entity.Movies) {
    formComponent = <MovieForm movie={(lookupEntity as MovieEntity) ?? (entity as MovieEntity)} genres={genres} studios={studios} formats={formats} isSubmitting={isSubmitting} />
  } else if (entity.type === Entity.Games) {
    formComponent = <GameForm game={(lookupEntity as GameEntity) ?? (entity as GameEntity)} developers={developers} publishers={publishers} genres={genres} formats={formats} platforms={platforms} isSubmitting={isSubmitting} />
  } else if (entity.type === Entity.Musics) {
    formComponent = <MusicForm music={(lookupEntity as MusicEntity) ?? (entity as MusicEntity)} genres={genres} formats={formats} labels={labels} isSubmitting={isSubmitting} />
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
            <input id="type" name="type" type="hidden" value={entity.type} />
            {lookupError && (
              <div className="mb-4 p-4 bg-yellow-900 border border-yellow-700 rounded-md">
                <p className="text-yellow-300">{lookupError}</p>
              </div>
            )}
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