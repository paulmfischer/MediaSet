import type { MetaFunction, ActionFunctionArgs, LoaderFunctionArgs } from "@remix-run/node";
import { Form, redirect, useActionData, useLoaderData, useNavigate, useNavigation, useSubmit } from "@remix-run/react";
import { useEffect, useRef } from "react";
import invariant from "tiny-invariant";
import { addEntity } from "~/entity-data";
import Spinner from "~/components/spinner";
import { getAuthors, getFormats, getGenres, getPublishers, getStudios, getDevelopers } from "~/metadata-data";
import { formToDto, getEntityFromParams, singular } from "~/helpers";
import { BookEntity, Entity, GameEntity } from "~/models";
import BookForm from "~/components/book-form";
import MovieForm from "~/components/movie-form";
import GameForm from "~/components/game-form";
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
  const [authors, genres, publishers, formats, studios, developers] = await Promise.all([
    getAuthors(), 
    getGenres(entityType), 
    getPublishers(), 
    getFormats(entityType), 
    getStudios(),
    getDevelopers()
  ]);
  return { authors, genres, publishers, formats, entityType, studios, developers };
};

export const action = async ({ request, params }: ActionFunctionArgs) => {
  invariant(params.entity, "Missing entity param");
  const entityType = getEntityFromParams(params);
  const formData = await request.formData();
  
  // Check if this is an ISBN lookup request
  const isbn = formData.get("isbn") as string;
  const intent = formData.get("intent") as string;
  
  if (intent === "lookup" && isbn) {
    if (entityType !== Entity.Books) {
      return { error: { isbn: "ISBN lookup is only available for books" } };
    }
    
    if (!isbn) {
      return { error: { isbn: "ISBN is required" } };
    }
    
    const lookupResult = await lookup(entityType, isbn);
    return { lookupResult, isbn };
  }
  
  // Otherwise, this is a book creation request
  const entity = formToDto(formData);
  if (entity) {
    const newEntity = await addEntity(entity);
    return redirect(`/${entityType.toLowerCase()}/${newEntity.id}`);
  } else {
    return { error: { invalidForm: `Failed to convert form to a ${entityType}` } };
  }
};

export default function Add() {
  const { authors, genres, publishers, formats, entityType, studios, developers } = useLoaderData<typeof loader>();
  const actionData = useActionData<typeof action>();
  const navigate = useNavigate();
  const navigation = useNavigation();
  const lookupFormRef = useRef<HTMLFormElement>(null);
  
  const isSubmitting = navigation.state === "submitting";
  const canDoISBNLookup = entityType === Entity.Books;
  
  // Extract lookup result and book data from action
  const lookupResult = actionData && 'lookupResult' in actionData ? actionData.lookupResult : undefined;
  const lookupError = (lookupResult as LookupError)?.error;
  const lookupBook = !lookupError ? lookupResult as BookEntity : undefined;
  const submittedISBN = actionData && 'isbn' in actionData ? actionData.isbn : undefined;
  
  // Handle form errors
  const formError = actionData && 'error' in actionData ? actionData.error : undefined;

  let formComponent;
  if (entityType === Entity.Books) {
    formComponent = <BookForm book={lookupBook as BookEntity} authors={authors} genres={genres} publishers={publishers} formats={formats} isSubmitting={isSubmitting} />;
  } else if (entityType === Entity.Movies) {
    formComponent = <MovieForm genres={genres} studios={studios} formats={formats} isSubmitting={isSubmitting} />
  } else if (entityType === Entity.Games) {
    formComponent = <GameForm developers={developers} publishers={publishers} genres={genres} formats={formats} isSubmitting={isSubmitting} />
  }

  return (
    <div className="min-h-screen flex text-white py-4">
      <div className="w-full max-w-3xl mx-auto px-2">
        <h1 className="text-2xl font-bold mb-6 text-white">Add a {singular(entityType)}</h1>
        
        {/* ISBN Lookup Section - Only for Books */}
        {canDoISBNLookup && (
          <div className="mb-8">
            <div className="mb-2">Search for a book by ISBN value to prefill the form below and allow editing the book before adding.  You can also manually enter a book by filling in the form below without looking up by ISBN.</div>
            <Form ref={lookupFormRef} method="post">
              <div className="mb-4">
                <label htmlFor="isbn" className="block text-sm font-medium text-gray-200 mb-1">
                  ISBN
                </label>
                <input
                  type="text"
                  id="isbn"
                  name="isbn"
                  defaultValue={submittedISBN}
                  className="w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400"
                  placeholder="Enter ISBN"
                  required
                />
                <input type="hidden" name="intent" value="lookup" />
              </div>
              <button
                type="submit"
                disabled={isSubmitting}
                className="w-full bg-blue-600 text-white py-2 px-4 rounded-md hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-400 focus:ring-offset-2 focus:ring-offset-gray-900 disabled:opacity-50"
              >
                {isSubmitting ? "Looking up..." : "Look up"}
              </button>
            </Form>
            
            {lookupError && (
              <div className="mt-4 p-4 bg-red-900 border border-red-700 rounded-md">
                <p className="text-red-300">{lookupError.notFound}</p>
              </div>
            )}
          </div>
        )}

        {/* Book Form Section */}
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