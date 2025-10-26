import type { MetaFunction, ActionFunctionArgs, LoaderFunctionArgs } from "@remix-run/node";
import { Form, redirect, useActionData, useLoaderData, useNavigate, useNavigation, useSubmit } from "@remix-run/react";
import { useEffect, useRef } from "react";
import invariant from "tiny-invariant";
import { addEntity } from "~/entity-data";
import Spinner from "~/components/spinner";
import { getAuthors, getFormats, getGenres, getPublishers, getStudios, getDevelopers, getLabels, getGamePublishers } from "~/metadata-data";
import { formToDto, getEntityFromParams, singular } from "~/helpers";
import { BookEntity, Entity, GameEntity, MovieEntity, MusicEntity } from "~/models";
import BookForm from "~/components/book-form";
import MovieForm from "~/components/movie-form";
import GameForm from "~/components/game-form";
import MusicForm from "~/components/music-form";
import { lookup, LookupError } from "~/lookup-data";

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
  
  // Check if this is a lookup request
  const intent = formData.get("intent") as string;
  
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
  const submit = useSubmit();
  
  const isSubmitting = navigation.state === "submitting";
  const canDoISBNLookup = entityType === Entity.Books;
  const canDoBarcodeLookup = entityType === Entity.Movies;
  
  // Extract lookup result from action
  const lookupResult = actionData && 'lookupResult' in actionData ? actionData.lookupResult : undefined;
  const lookupError = (lookupResult as LookupError)?.error;
  const lookupBook = entityType === Entity.Books && !lookupError ? lookupResult as BookEntity : undefined;
  const lookupMovie = entityType === Entity.Movies && !lookupError ? lookupResult as MovieEntity : undefined;
  const lookupType = actionData && 'lookupType' in actionData ? actionData.lookupType : undefined;
  const isLookingUp = isSubmitting && lookupType;
  
  // Handle form errors
  const formError = actionData && 'error' in actionData ? actionData.error : undefined;

  // Lookup handlers
  const handleISBNLookup = (isbn: string) => {
    const formData = new FormData();
    formData.append("isbn", isbn);
    formData.append("intent", "lookup-isbn");
    submit(formData, { method: "post" });
  };

  const handleBarcodeLookup = (barcode: string) => {
    const formData = new FormData();
    formData.append("barcode", barcode);
    formData.append("intent", "lookup-barcode");
    submit(formData, { method: "post" });
  };

  let formComponent;
  if (entityType === Entity.Books) {
    formComponent = <BookForm book={lookupBook} authors={authors} genres={genres} publishers={publishers} formats={formats} isSubmitting={isSubmitting} onLookup={handleISBNLookup} isLookingUp={isLookingUp} />;
  } else if (entityType === Entity.Movies) {
    formComponent = <MovieForm movie={lookupMovie} genres={genres} studios={studios} formats={formats} isSubmitting={isSubmitting} onLookup={handleBarcodeLookup} isLookingUp={isLookingUp} />
  } else if (entityType === Entity.Games) {
    formComponent = <GameForm developers={developers} publishers={publishers} genres={genres} formats={formats} isSubmitting={isSubmitting} />
  } else if (entityType === Entity.Musics) {
    formComponent = <MusicForm genres={genres} formats={formats} labels={labels} isSubmitting={isSubmitting} />
  }

  return (
    <div className="min-h-screen flex text-white py-4">
      <div className="w-full max-w-3xl mx-auto px-2">
        <h1 className="text-2xl font-bold mb-6 text-white">Add a {singular(entityType)}</h1>
        
        {/* Show lookup error if any */}
        {lookupError && (
          <div className="mb-4 p-4 bg-red-900 border border-red-700 rounded-md">
            <p className="text-red-300">{lookupError.notFound}</p>
          </div>
        )}

        {/* Entity Form Section */}
        <div className="mb-8">
          <Form method="post">
            <input type="hidden" name="type" value={entityType} />
            
            {formError && 'invalidForm' in formError && (
              <div className="mb-4 p-4 bg-red-900 border border-red-700 rounded-md">
                <p className="text-red-300">{formError.invalidForm}</p>
              </div>
            )}
            
            {formError && 'isbn' in formError && (
              <div className="mb-4 p-4 bg-red-900 border border-red-700 rounded-md">
                <p className="text-red-300">{formError.isbn}</p>
              </div>
            )}
            
            {formError && 'barcode' in formError && (
              <div className="mb-4 p-4 bg-red-900 border border-red-700 rounded-md">
                <p className="text-red-300">{formError.barcode}</p>
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