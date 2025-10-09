import { ActionFunctionArgs, LoaderFunctionArgs } from "@remix-run/node";
import { Form, useActionData, useLoaderData, useNavigation, useSubmit } from "@remix-run/react";
import { Entity } from "~/models";
import { useEffect, useRef } from "react";
import { LookupError, lookup } from "~/lookup-data";
import { singular } from "~/helpers";

export async function loader({ params }: LoaderFunctionArgs) {
  if (params.entity?.toLowerCase() !== Entity.Books.toLowerCase()) {
    throw new Response(null, { status: 404 });
  }
  return { entity: params.entity };
}

export async function action({ request, params }: ActionFunctionArgs) {
  if (params.entity?.toLowerCase() !== Entity.Books.toLowerCase()) {
    throw new Response(null, { status: 404 });
  }

  const formData = await request.formData();
  const isbn = formData.get("isbn") as string;

  if (!isbn) {
    return { error: { isbn: "ISBN is required" } };
  }

  const result = await lookup(Entity.Books, isbn);
  return result;
}

export default function LookupRoute() {
  const data = useActionData<typeof action>();
  const { entity } = useLoaderData<typeof loader>();
  const navigation = useNavigation();
  const formRef = useRef<HTMLFormElement>(null);
  const submit = useSubmit();
  const isSubmitting = navigation.state === "submitting";

  useEffect(() => {
    if (!isSubmitting && (!data || !('error' in data))) {
      formRef.current?.reset();
    }
  }, [isSubmitting, data]);

  function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    submit(event.currentTarget);
  }

  return (
    <div className="container mx-auto px-4 py-8 text-white min-h-screen">
      <h1 className="text-2xl font-bold mb-6 text-white">ISBN Lookup</h1>
      <Form ref={formRef} method="post" onSubmit={handleSubmit} className="max-w-md">
        <div className="mb-4">
          <label htmlFor="isbn" className="block text-sm font-medium text-gray-200 mb-1">
            ISBN
          </label>
          <input
            type="text"
            id="isbn"
            name="isbn"
            className="w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400"
            placeholder="Enter ISBN"
            required
          />
        </div>
        <button
          type="submit"
          disabled={isSubmitting}
          className="w-full bg-blue-600 text-white py-2 px-4 rounded-md hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-400 focus:ring-offset-2 focus:ring-offset-gray-900 disabled:opacity-50"
        >
          {isSubmitting ? "Looking up..." : "Look up"}
        </button>
      </Form>

      {data && 'error' in data && 'notFound' in data.error && (
        <div className="mt-6 p-4 bg-red-900 border border-red-700 rounded-md">
          <p className="text-red-300">{data.error.notFound}</p>
        </div>
      )}

      {data && !('error' in data) && (
        <div className="mt-6 p-4 bg-gray-800 border border-gray-700 rounded-md">
          <h2 className="text-xl font-semibold mb-2 text-white">{data.title}</h2>
          {data.subtitle && <p className="text-gray-300 mb-4 italic">{data.subtitle}</p>}
          <dl className="grid grid-cols-1 md:grid-cols-2 gap-x-4 gap-y-3">
            {data.isbn && (
              <>
                <dt className="text-gray-400 font-medium">ISBN</dt>
                <dd className="text-gray-200">{data.isbn}</dd>
              </>
            )}
            {data.authors && data.authors.length > 0 && (
              <>
                <dt className="text-gray-400 font-medium">Authors</dt>
                <dd className="text-gray-200">{data.authors.join(", ")}</dd>
              </>
            )}
            {data.publisher && (
              <>
                <dt className="text-gray-400 font-medium">Publisher</dt>
                <dd className="text-gray-200">{data.publisher}</dd>
              </>
            )}
            {data.publicationDate && (
              <>
                <dt className="text-gray-400 font-medium">Publication Date</dt>
                <dd className="text-gray-200">{data.publicationDate}</dd>
              </>
            )}
            {data.pages && (
              <>
                <dt className="text-gray-400 font-medium">Pages</dt>
                <dd className="text-gray-200">{data.pages}</dd>
              </>
            )}
            {data.genres && data.genres.length > 0 && (
              <>
                <dt className="text-gray-400 font-medium">Genres/Subjects</dt>
                <dd className="text-gray-200">
                  <div className="flex flex-wrap gap-1">
                    {data.genres.map((genre, index) => (
                      <span
                        key={index}
                        className="inline-block bg-blue-800 text-blue-100 text-sm px-2 py-1 rounded"
                      >
                        {genre}
                      </span>
                    ))}
                  </div>
                </dd>
              </>
            )}
            {data.format && (
              <>
                <dt className="text-gray-400 font-medium">Format</dt>
                <dd className="text-gray-200">{data.format}</dd>
              </>
            )}
          </dl>
          {data.plot && (
            <div className="mt-4">
              <dt className="text-gray-400 font-medium mb-2">Plot</dt>
              <dd className="text-gray-200 leading-relaxed">{data.plot}</dd>
            </div>
          )}
        </div>
      )}
    </div>
  );
}