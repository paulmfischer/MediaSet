import type { MetaFunction, ActionFunctionArgs, LoaderFunctionArgs } from "@remix-run/node";
import { Form, redirect, useActionData, useLoaderData, useNavigate, useNavigation } from "@remix-run/react";
import { useEffect } from "react";
import invariant from "tiny-invariant";
import { addEntity } from "~/entity-data";
import Spinner from "~/components/spinner";
import { getAuthors, getFormats, getGenres, getPublishers, getStudios } from "~/metadata-data";
import { formToDto, getEntityFromParams, singular } from "~/helpers";
import { BookEntity, Entity } from "~/models";
import BookForm from "~/components/book-form";
import MovieForm from "~/components/movie-form";
import { lookup, LookupError } from "~/lookup-data";

export const meta: MetaFunction<typeof loader> = ({ params }) => {
  const entityType = getEntityFromParams(params);
  return [
    { title: `Add a ${singular(entityType)}` },
    { name: "description", content: `Add a ${singular(entityType)}` },
  ];
};

export const loader = async ({ params, request }: LoaderFunctionArgs) => {
  const entityType = getEntityFromParams(params);
  const url = new URL(request.url);
  const barcodeLookup = url.searchParams.get('barcodeLookup') as string | undefined;
  let lookupEntity;
  if (barcodeLookup) {
    lookupEntity = await lookup(entityType, barcodeLookup);
  }
  const [authors, genres, publishers, formats, studios] = await Promise.all([getAuthors(), getGenres(entityType), getPublishers(), getFormats(entityType), getStudios()]);
  let intent: 'barcode' | 'manual' = url.searchParams.get('intent') as 'barcode' | 'manual' ?? 'barcode';
  // if we failed to lookup the barcode, stay on the barcode view and display error.
  if ((lookupEntity as LookupError).error) {
    intent = 'barcode';
  }
  return { authors, genres, publishers, formats, entityType, studios, lookupEntity, barcodeLookup, intent };
};

export const action = async ({ request, params }: ActionFunctionArgs) => {
  invariant(params.entity, "Missing entity param");
  const entityType = getEntityFromParams(params);
  const formData = await request.formData();
  const entity = formToDto(formData);
  if (entity) {
    const newEntity = await addEntity(entity);
    return redirect(`/${entityType.toLowerCase()}/${newEntity.id}`);
  } else {
    return { error: { invalidForm: `Failed to convert form to a ${entityType}` } };
  }
};

export default function Add() {
  const { authors, genres, publishers, formats, entityType, studios, lookupEntity, barcodeLookup, intent } = useLoaderData<typeof loader>();
  const actionData = useActionData<typeof action>();
  const navigate = useNavigate();
  const navigation = useNavigation();
  const isSubmitting = navigation.location?.pathname === `/${entityType.toLowerCase()}/add`;
  const canDoBarcodeLookup = entityType === Entity.Books;
  const lookupError = (lookupEntity as LookupError).error;
  
  useEffect(() => {
    const barcodeInput = document.getElementById('barcodeLookup');
    if (barcodeInput instanceof HTMLInputElement && barcodeLookup) {
      barcodeInput.value = barcodeLookup;
    }
  });
  
  let formComponent;
  if (entityType === Entity.Books) {
    formComponent = <BookForm book={lookupEntity as BookEntity} authors={authors} genres={genres} publishers={publishers} formats={formats} isSubmitting={isSubmitting} />;
  } else if (entityType === Entity.Movies) {
    formComponent = <MovieForm genres={genres} studios={studios} formats={formats} isSubmitting={isSubmitting} />
  }

  return (
    <div className="flex flex-col">
      <div className="flex flex-row items-center justify-between">
        <div className="flex flex-row gap-4 items-end">
          <h2 className="text-2xl">Add a {singular(entityType)}</h2>
          {canDoBarcodeLookup &&
            <Form id="entry-type">
              {intent == 'manual' && <button name="intent" value="barcode" type="submit">ISBN Lookup</button>}
              {intent == 'barcode' && <button name="intent" value="manual" type="submit">Manual Entry</button>}
            </Form>
          }
        </div>
      </div>
      <div className="h-full mt-4">
        <div className="mt-4 flex flex-col gap-2">
          {intent === 'barcode' && canDoBarcodeLookup &&
            <Form id="barcode-lookup">
              <fieldset disabled={isSubmitting} className="flex flex-col gap-2 mb-2">
                <input id="hidden-intent" name="intent" type="hidden" value="manual" />
                <label htmlFor="isbn" className="dark:text-slate-400">Lookup Book by ISBN</label>
                <input id="barcodeLookup" name="barcodeLookup" type="text" placeholder="ISBN" aria-label="ISBN lookup" />
              </fieldset>
              {lookupError && <div className="mb-2">{lookupError.notFound}</div>}
              <button type="submit">Lookup</button>
            </Form>
          }
          {(intent === 'manual' || !canDoBarcodeLookup) &&
            <Form id={`add-${singular(entityType)}`} method="post" action={`/${entityType.toLowerCase()}/add`}>
              <input id="type" name="type" type="hidden" value={entityType} />
              {actionData?.error && <div>{actionData.error.invalidForm}</div>}
              {formComponent}
              <div className="flex flex-row gap-2 mt-3">
                <button type="submit" className="flex flex-row gap-2" disabled={isSubmitting}>
                  {isSubmitting ? <div className="flex items-center"><Spinner /></div> : null}
                  Add
                </button>
                <button type="button" onClick={() => navigate(-1)} className="secondary" disabled={isSubmitting}>Cancel</button>
              </div>
            </Form>
          }
        </div>
      </div>
    </div>
  );
}