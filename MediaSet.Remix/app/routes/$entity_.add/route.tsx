import type { MetaFunction, ActionFunctionArgs, LoaderFunctionArgs } from "@remix-run/node";
import { Form, redirect, useActionData, useLoaderData, useNavigate, useNavigation, useSubmit } from "@remix-run/react";
import { useEffect, useRef } from "react";
import invariant from "tiny-invariant";
import { addEntity } from "~/entity-data";
import Spinner from "~/components/spinner";
import { getAuthors, getFormats, getGenres, getPublishers, getStudios, getDevelopers, getLabels, getGamePublishers } from "~/metadata-data";
import { formToDto, getEntityFromParams, singular } from "~/helpers";
import { BookEntity, Entity, GameEntity, MusicEntity, MovieEntity } from "~/models";
import BookForm from "~/components/book-form";
import MovieForm from "~/components/movie-form";
import GameForm from "~/components/game-form";
import MusicForm from "~/components/music-form";
import { lookup, isLookupError, getIdentifierTypeForField } from "~/lookup-data";

export const meta: MetaFunction<typeof loader> = ({ params }) => {
  const entityType = getEntityFromParams(params);
  return [
    { title: `Add a ${singular(entityType)}` },
    { name: "description", content: `Add a ${singular(entityType)}` },
  ];
};

export const loader = async ({ params }: LoaderFunctionArgs) => {
  const entityType = getEntityFromParams(params);
  const [genres, formats, authors, publishers, studios, developers, labels] = await Promise.all([
    getGenres(entityType),
    getFormats(entityType),
    entityType === Entity.Books ? getAuthors() : Promise.resolve([]),
    entityType === Entity.Books ? getPublishers() : entityType === Entity.Games ? getGamePublishers() : Promise.resolve([]),
    entityType === Entity.Movies ? getStudios() : Promise.resolve([]),
    entityType === Entity.Games ? getDevelopers() : Promise.resolve([]),
    entityType === Entity.Musics ? getLabels() : Promise.resolve([])
  ]);
  return { authors, genres, publishers, formats, entityType, studios, developers, labels };
};

export const action = async ({ request, params }: ActionFunctionArgs) => {
  invariant(params.entity, "Missing entity param");
  const entityType = getEntityFromParams(params);
  const formData = await request.formData();
  
  const intent = formData.get("intent") as string;
  
  if (intent === "lookup") {
    const fieldName = formData.get("fieldName") as string;
    const identifierValue = formData.get("identifierValue") as string;
    
    if (!identifierValue) {
      return { error: { lookup: "Identifier value is required" } };
    }
    
    const identifierType = getIdentifierTypeForField(entityType, fieldName);
    const lookupResult = await lookup(entityType, identifierType, identifierValue);
    return { lookupResult, identifierValue, fieldName };
  }
  
  // Otherwise, this is an entity creation request
  const entity = formToDto(formData);
  if (entity) {
    const newEntity = await addEntity(entity);
    return redirect(`/${entityType.toLowerCase()}/${newEntity.id}`);
  } else {
    return { error: { invalidForm: `Failed to convert form to a ${entityType}` } };
  }
};

export default function Add() {
  const { authors, genres, publishers, formats, entityType, studios, developers, labels } = useLoaderData<typeof loader>();
  const actionData = useActionData<typeof action>();
  const navigate = useNavigate();
  const navigation = useNavigation();
  
  const isSubmitting = navigation.state === "submitting";
  
  // Extract lookup result from action
  const lookupResult = actionData && 'lookupResult' in actionData ? actionData.lookupResult : undefined;
  const lookupEntity = lookupResult && !isLookupError(lookupResult) ? lookupResult : undefined;
  const lookupError = lookupResult && isLookupError(lookupResult) ? lookupResult.message : undefined;
  
  // Handle form errors
  const formError = actionData && 'error' in actionData ? actionData.error : undefined;

  let formComponent;
  if (entityType === Entity.Books) {
    formComponent = <BookForm book={lookupEntity as BookEntity} authors={authors} genres={genres} publishers={publishers} formats={formats} isSubmitting={isSubmitting} />;
  } else if (entityType === Entity.Movies) {
    formComponent = <MovieForm movie={lookupEntity as MovieEntity} genres={genres} studios={studios} formats={formats} isSubmitting={isSubmitting} />
  } else if (entityType === Entity.Games) {
    formComponent = <GameForm game={lookupEntity as GameEntity} developers={developers} publishers={publishers} genres={genres} formats={formats} isSubmitting={isSubmitting} />
  } else if (entityType === Entity.Musics) {
    formComponent = <MusicForm music={lookupEntity as MusicEntity} genres={genres} formats={formats} labels={labels} isSubmitting={isSubmitting} />
  }

  return (
    <div className="min-h-screen flex text-white py-4">
      <div className="w-full max-w-3xl mx-auto px-2">
        <h1 className="text-2xl font-bold mb-6 text-white">Add a {singular(entityType)}</h1>
        
        <div className="mb-8">
          <Form method="post">
            <input type="hidden" name="type" value={entityType} />
            
            {formError && 'invalidForm' in formError && (
              <div className="mb-4 p-4 bg-red-900 border border-red-700 rounded-md">
                <p className="text-red-300">{formError.invalidForm}</p>
              </div>
            )}
            
            {formError && 'lookup' in formError && (
              <div className="mb-4 p-4 bg-red-900 border border-red-700 rounded-md">
                <p className="text-red-300">{formError.lookup}</p>
              </div>
            )}
            
            {lookupError && (
              <div className="mb-4 p-4 bg-yellow-900 border border-yellow-700 rounded-md">
                <p className="text-yellow-300">{lookupError}</p>
              </div>
            )}
            
            {formComponent}
            
            <div className="flex flex-row gap-2 mt-6">
              <button 
                type="submit" 
                className="flex flex-row gap-2 bg-green-600 text-white py-2 px-4 rounded-md hover:bg-green-700 focus:outline-none focus:ring-2 focus:ring-green-400 focus:ring-offset-2 focus:ring-offset-gray-900 disabled:opacity-50" 
                disabled={isSubmitting}
              >
                {isSubmitting && <div className="flex items-center"><Spinner /></div>}
                Add {singular(entityType)}
              </button>
              <button 
                type="button" 
                onClick={() => navigate(-1)} 
                className="bg-gray-600 text-white py-2 px-4 rounded-md hover:bg-gray-700 focus:outline-none focus:ring-2 focus:ring-gray-400 focus:ring-offset-2 focus:ring-offset-gray-900 disabled:opacity-50" 
                disabled={isSubmitting}
              >
                Cancel
              </button>
            </div>
          </Form>
        </div>
      </div>
    </div>
  );
}