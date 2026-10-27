import { isRouteErrorResponse, Link, Outlet, useRouteError } from 'react-router';

export default function CollectionLayout() {
  return <Outlet />;
}

export function ErrorBoundary() {
  const error = useRouteError();
  const status = isRouteErrorResponse(error) ? error.status : 500;
  return (
    <section className="p-4" role="alert">
      <h2 className="text-2xl">{status === 404 ? 'Media not found' : 'Unable to load collection'}</h2>
      <p className="my-4">
        {status === 404
          ? 'This collection or item does not exist.'
          : 'The collection service is unavailable. Please try again.'}
      </p>
      <button type="button" onClick={() => window.location.reload()}>
        Try again
      </button>
      <Link to="/" className="ml-4">
        Dashboard
      </Link>
    </section>
  );
}
