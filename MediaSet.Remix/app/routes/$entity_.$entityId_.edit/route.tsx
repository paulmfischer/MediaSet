import type { MetaFunction, ActionFunctionArgs, LoaderFunctionArgs } from "@remix-run/node";
import { Form, redirect, useActionData, useLoaderData, useNavigate, useNavigation, useSubmit } from "@remix-run/react";
import { addEntity, getEntity, updateEntity } from "~/entity-data";
import Spinner from "~/components/spinner";
import { getAuthors, getFormats, getGenres, getPublishers, getStudios, getDevelopers, getLabels, getGamePublishers } from "~/metadata-data";
import { formToDto, getEntityFromParams, singular } from "~/helpers";
import { BookEntity, Entity, GameEntity, MovieEntity, MusicEntity } from "~/models";
import BookForm from "../../components/book-form";
import MovieForm from "~/components/movie-form";
import GameForm from "~/components/game-form";
import MusicForm from "~/components/music-form";
import invariant from "tiny-invariant";
import { lookup, LookupError } from "~/lookup-data";

export const meta: MetaFunction<typeof loader> = ({ params }) => {
  const entityType = getEntityFromParams(params);
  return [
    { title: `Edit ${singular(entityType)}` },
    { name: "description", content: `Edit ${singular(entityType)}` },
  ];
};

export const loader = async ({ params }: LoaderFunctionArgs) => {
  invariant(params.entity, "Missing entity param");
  invariant(params.entityId, "Missing entityId param");
  const entityType = getEntityFromParams(params);
  const entity = await getEntity(entityType, params.entityId);
  const [genres, formats, authors, publishers, studios, developers, labels] =
   await Promise.all([
    getGenres(entityType),
    getFormats(entityType),
    entity.type === Entity.Books ? getAuthors() : Promise.resolve([]),
    entity.type === Entity.Books ? getPublishers() : entity.type === Entity.Games ? getGamePublishers() : Promise.resolve([]),
    entity.type === Entity.Movies ? getStudios() : Promise.resolve([]),
    entity.type === Entity.Games ? getDevelopers() : Promise.resolve([]),
    entity.type === Entity.Musics ? getLabels() : Promise.resolve([])
  ]);
  return { entity, authors, genres, publishers, formats, entityType, studios, developers, labels };
}

export const action = async ({ request, params }: ActionFunctionArgs) => {
  invariant(params.entity, "Missing entity param");
  invariant(params.entityId, "Missing entityId param");
  const entityType = getEntityFromParams(params);
  const formData = await request.formData();
  const intent = formData.get("intent") as string | null;

  // Lookup handling (does not update the entity)
  if (intent === "lookup-isbn") {
    const isbn = formData.get("isbn") as string;
    if (entityType !== Entity.Books) {
      return { error: { isbn: "ISBN lookup is only available for books" } };
    }
    if (!isbn) {
      return { error: { isbn: "ISBN is required" } };
    }
    const lookupResult = await lookup(Entity.Books, "isbn", isbn);
    return { lookupResult, isbn, lookupType: "isbn" };
  }

  if (intent === "lookup-barcode") {
    const barcode = formData.get("barcode") as string;
    if (entityType !== Entity.Movies) {
      return { error: { barcode: "Barcode lookup is only available for movies" } };
    }
    if (!barcode) {
      return { error: { barcode: "Barcode is required" } };
    }
    const lookupResult = await lookup(Entity.Movies, "upc", barcode);
    return { lookupResult, barcode, lookupType: "barcode" };
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
  const { entity, authors, genres, publishers, formats, entityType, studios, developers, labels } = useLoaderData<typeof loader>();
  const actionData = useActionData<typeof action>();
  const navigate = useNavigate();
  const navigation = useNavigation();
  const submit = useSubmit();
  const isSubmitting = navigation.location?.pathname === `/${entity.type.toLowerCase()}/${entity.id}/edit`;
  const formId = `edit-${singular(entity.type)}`;
  const actionUrl = `/${entity.type.toLowerCase()}/${entity.id}/edit`;
  
  // Extract lookup result/error
  const lookupResult = actionData && 'lookupResult' in (actionData as any) ? (actionData as any).lookupResult : undefined;
  const lookupError = (lookupResult as LookupError)?.error;
  const lookupType = actionData && 'lookupType' in (actionData as any) ? (actionData as any).lookupType : undefined;
  const isLookingUp = isSubmitting && !!lookupType;

  // Merge helpers to preserve existing fields when applying lookup on edit
  const mergeBook = (original: BookEntity, updated?: BookEntity): BookEntity => {
    if (!updated) return original;
    return {
      ...original,
      ...updated,
      authors: updated.authors ?? original.authors,
      genres: updated.genres ?? original.genres,
    };
  };
  const mergeMovie = (original: MovieEntity, updated?: MovieEntity): MovieEntity => {
    if (!updated) return original;
    return {
      ...original,
      ...updated,
      studios: updated.studios ?? original.studios,
      genres: updated.genres ?? original.genres,
    };
  };
  
  // Lookup entities (only of matching type) and merged with current entity
  const lookupBook = entity.type === Entity.Books && !lookupError ? (lookupResult as BookEntity | undefined) : undefined;
  const lookupMovie = entity.type === Entity.Movies && !lookupError ? (lookupResult as MovieEntity | undefined) : undefined;
  const effectiveBook = entity.type === Entity.Books ? mergeBook(entity as BookEntity, lookupBook) : undefined;
  const effectiveMovie = entity.type === Entity.Movies ? mergeMovie(entity as MovieEntity, lookupMovie) : undefined;

  // Lookup handlers
  const handleISBNLookup = (isbn: string) => {
    const fd = new FormData();
    fd.append("isbn", isbn);
    fd.append("intent", "lookup-isbn");
    submit(fd, { method: "post", action: actionUrl });
  };
  const handleBarcodeLookup = (barcode: string) => {
    const fd = new FormData();
    fd.append("barcode", barcode);
    fd.append("intent", "lookup-barcode");
    submit(fd, { method: "post", action: actionUrl });
  };
  
  let formComponent;
  if (entity.type === Entity.Books) {
    formComponent = <BookForm book={effectiveBook} authors={authors} genres={genres} publishers={publishers} formats={formats} isSubmitting={isSubmitting} onLookup={handleISBNLookup} isLookingUp={isLookingUp} />;
  } else if (entity.type === Entity.Movies) {
    formComponent = <MovieForm movie={effectiveMovie} genres={genres} studios={studios} formats={formats} isSubmitting={isSubmitting} onLookup={handleBarcodeLookup} isLookingUp={isLookingUp} />
  } else if (entity.type === Entity.Games) {
    formComponent = <GameForm game={entity as GameEntity} developers={developers} publishers={publishers} genres={genres} formats={formats} isSubmitting={isSubmitting} />
  } else if (entity.type === Entity.Musics) {
    formComponent = <MusicForm music={entity as MusicEntity} genres={genres} formats={formats} labels={labels} isSubmitting={isSubmitting} />
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
          {lookupError && (
            <div className="mb-4 p-4 bg-red-900 border border-red-700 rounded-md">
              <p className="text-red-300">{lookupError.notFound}</p>
            </div>
          )}
          <Form id={formId} method="post" action={actionUrl}>
            <input id="type" name="type" type="hidden" value={entity.type} />
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