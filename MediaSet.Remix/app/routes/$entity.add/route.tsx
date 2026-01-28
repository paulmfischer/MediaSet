import type { MetaFunction, ActionFunctionArgs, LoaderFunctionArgs } from "@remix-run/node";
import { Form, redirect, useActionData, useLoaderData, useNavigate, useNavigation, useSubmit } from "@remix-run/react";
import { useEffect, useRef } from "react";
import invariant from "tiny-invariant";
import { addEntity } from "~/entity-data";
import { getAuthors, getFormats, getGenres, getPublishers, getStudios, getDevelopers, getLabels, getGamePublishers, getPlatforms } from "~/metadata-data";
import { formToDto, getEntityFromParams, singular } from "~/helpers";
import { BookEntity, Entity, GameEntity, MusicEntity, MovieEntity } from "~/models";
import { serverLogger } from "~/utils/serverLogger";
import BookForm from "~/components/book-form";
import MovieForm from "~/components/movie-form";
import GameForm from "~/components/game-form";
import MusicForm from "~/components/music-form";
// Server-only lookup utilities are imported dynamically inside the action to avoid client bundling

function isLookupError(result: any): result is { message: string; statusCode: number } {
  return result && typeof result.message === "string" && typeof result.statusCode === "number";
}

export const meta: MetaFunction<typeof loader> = ({ params }) => {
  const entityType = getEntityFromParams(params);
  return [
    { title: `Add a ${singular(entityType)}` },
    { name: "description", content: `Add a ${singular(entityType)}` },
  ];
};

export const loader = async ({ params }: LoaderFunctionArgs) => {
  const entityType = getEntityFromParams(params);
  try {
    const [genres, formats, authors, publishers, studios, developers, labels, platforms] = await Promise.all([
      getGenres(entityType),
      getFormats(entityType),
      entityType === Entity.Books ? getAuthors() : Promise.resolve([]),
      entityType === Entity.Books ? getPublishers() : entityType === Entity.Games ? getGamePublishers() : Promise.resolve([]),
      entityType === Entity.Movies ? getStudios() : Promise.resolve([]),
      entityType === Entity.Games ? getDevelopers() : Promise.resolve([]),
      entityType === Entity.Musics ? getLabels() : Promise.resolve([]),
      entityType === Entity.Games ? getPlatforms() : Promise.resolve([])
    ]);
    return { authors, genres, publishers, formats, entityType, studios, developers, labels, platforms };
  } catch (error) {
    throw error;
  }
};

export const action = async ({ request, params }: ActionFunctionArgs) => {
  invariant(params.entity, "Missing entity param");
  const entityType = getEntityFromParams(params);
  const formData = await request.formData();
  
  const intent = formData.get("intent") as string;
  
  if (intent === "lookup") {
    const fieldName = formData.get("fieldName") as string;
    const identifierValue = formData.get("identifierValue") as string;
    
    serverLogger.info("Action: Performing entity lookup", { entityType, fieldName, identifierValue });
    
    if (!identifierValue) {
      serverLogger.warn("Action: Lookup failed - missing identifier value", { entityType, fieldName });
      return { error: { lookup: "Identifier value is required" } };
    }
    
    try {
      const { lookup, getIdentifierTypeForField } = await import("~/lookup-data.server");
      const identifierType = getIdentifierTypeForField(entityType, fieldName);
      const lookupResult = await lookup(entityType, identifierType, identifierValue);
      serverLogger.info("Action: Entity lookup successful", { entityType, fieldName, identifierValue });
      // Include a lookup timestamp so the UI can force a remount for consecutive lookups
      return { lookupResult, identifierValue, fieldName, lookupTimestamp: Date.now() };
    } catch (error) {
      serverLogger.error("Action: Entity lookup failed", { entityType, fieldName, identifierValue, error: String(error) });
      throw error;
    }
  }
  
  // Otherwise, this is an entity creation request
  const entity = formToDto(formData);
  if (!entity) {
    serverLogger.error("Action: Failed to convert form data to entity", { entityType });
    return { error: { invalidForm: `Failed to convert form to a ${entityType}` } };
  }

  // Check if there's an image file to send as multipart/form-data
  const coverImageFile = formData.get("coverImage") as File | null;
  let apiFormData: FormData | undefined;
  
  if (coverImageFile && coverImageFile.size > 0) {
    // Create FormData to send to the backend API with entity as JSON and image file
    apiFormData = new FormData();
    apiFormData.append("entity", JSON.stringify(entity));
    apiFormData.append("coverImage", coverImageFile);
  }

  try {
    const newEntity = await addEntity(entity, apiFormData);
    return redirect(`/${entityType.toLowerCase()}/${newEntity.id}`);
  } catch (error) {
    throw error;
  }
};

export default function Add() {
  const { authors, genres, publishers, formats, entityType, studios, developers, labels, platforms } = useLoaderData<typeof loader>();
  const actionData = useActionData<typeof action>();
  const navigate = useNavigate();
  const navigation = useNavigation();
  
  const isSubmitting = navigation.state === "submitting";
  
  // Extract lookup result from action
  const lookupResult = actionData && 'lookupResult' in actionData ? actionData.lookupResult : undefined;
  const lookupEntity = lookupResult && !isLookupError(lookupResult) ? lookupResult : undefined;
  const lookupError = lookupResult && isLookupError(lookupResult) ? lookupResult.message : undefined;
  const lookupTimestamp = actionData && 'lookupTimestamp' in actionData ? actionData.lookupTimestamp : undefined;
  
  // Handle form errors
  const formError = actionData && 'error' in actionData ? actionData.error : undefined;

  // Use a key to force form remount when lookup data changes
  // This ensures defaultValue props are re-applied with new lookup data
  const identifierValue = actionData && 'identifierValue' in actionData ? actionData.identifierValue : undefined;
  const fieldName = actionData && 'fieldName' in actionData ? actionData.fieldName : undefined;
  const formKey = lookupEntity && identifierValue && fieldName ? `lookup-${identifierValue}-${fieldName}-${lookupTimestamp ?? '0'}` : 'empty';

  let formComponent;
  if (entityType === Entity.Books) {
    formComponent = <BookForm key={formKey} book={lookupEntity as BookEntity} authors={authors} genres={genres} publishers={publishers} formats={formats} isSubmitting={isSubmitting} />;
  } else if (entityType === Entity.Movies) {
    formComponent = <MovieForm key={formKey} movie={lookupEntity as MovieEntity} genres={genres} studios={studios} formats={formats} isSubmitting={isSubmitting} />
  } else if (entityType === Entity.Games) {
    formComponent = <GameForm key={formKey} game={lookupEntity as GameEntity} developers={developers} publishers={publishers} genres={genres} formats={formats} platforms={platforms} isSubmitting={isSubmitting} />
  } else if (entityType === Entity.Musics) {
    formComponent = <MusicForm key={formKey} music={lookupEntity as MusicEntity} genres={genres} formats={formats} labels={labels} isSubmitting={isSubmitting} />
  }

  return (
    <div className="min-h-screen flex text-white py-4">
      <div className="w-full max-w-3xl mx-auto px-2">
        <h1 className="text-2xl font-bold mb-6 text-white">Add a {singular(entityType)}</h1>
        
        <div className="mb-8">
          <Form method="post" encType="multipart/form-data">
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
                className="flex flex-row gap-2" 
                disabled={isSubmitting}
              >
                Add {singular(entityType)}
              </button>
              <button 
                type="button" 
                onClick={() => navigate(-1)} 
                className="secondary" 
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