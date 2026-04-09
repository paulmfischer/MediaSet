import './tailwind.css';
import {
  Link,
  Links,
  Meta,
  NavLink,
  Outlet,
  Scripts,
  ScrollRestoration,
  useRouteError,
  isRouteErrorResponse,
  useLocation,
} from '@remix-run/react';
import { useEffect, useRef, useState } from 'react';
import { Clapperboard, Gamepad2, HardDrive, Home, LibraryBig, Menu, Music, Settings, X } from 'lucide-react';
import ErrorScreen from './components/error-screen';
import PendingNavigation from './components/pending-navigation';
import Footer from './components/footer';

function Header() {
  const [open, setOpen] = useState(false);
  const [toolsOpen, setToolsOpen] = useState(false);
  const toolsRef = useRef<HTMLDivElement>(null);
  const location = useLocation();

  // Close menus when navigation occurs
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setOpen(false);

    setToolsOpen(false);
  }, [location]);

  // Close tools dropdown when clicking outside
  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (toolsRef.current && !toolsRef.current.contains(event.target as Node)) {
        setToolsOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  return (
    <header className="sticky top-0 z-30 dark:bg-zinc-700 p-2 lg:px-8">
      <div className="flex items-center justify-between gap-4">
        <Link to="/">
          <h1 className="text-3xl">MediaSet</h1>
        </Link>

        {/* Desktop nav */}
        <nav className="hidden md:flex flex-row gap-4 items-center">
          <NavLink to="/" className="p-3 flex gap-2 items-center rounded-lg">
            <Home /> Home
          </NavLink>
          <NavLink to="/books" className="entity-books p-3 flex gap-2 items-center rounded-lg">
            <LibraryBig className="text-entity" /> Books
          </NavLink>
          <NavLink to="/movies" className="entity-movies p-3 flex gap-2 items-center rounded-lg">
            <Clapperboard className="text-entity" /> Movies
          </NavLink>
          <NavLink to="/games" className="entity-games p-3 flex gap-2 items-center rounded-lg">
            <Gamepad2 className="text-entity" /> Games
          </NavLink>
          <NavLink to="/musics" className="entity-musics p-3 flex gap-2 items-center rounded-lg">
            <Music className="text-entity" /> Music
          </NavLink>

          {/* Tools dropdown */}
          <div ref={toolsRef} className="relative">
            <button
              className="p-3 flex gap-2 items-center rounded-lg tertiary"
              onClick={() => setToolsOpen(!toolsOpen)}
              aria-label="Tools"
              aria-expanded={toolsOpen}
            >
              <Settings />
            </button>
            {toolsOpen && (
              <div className="absolute right-0 top-full mt-1 w-48 rounded-lg border border-zinc-600 dark:bg-zinc-800 shadow-lg z-50">
                <NavLink
                  to="/image-stats"
                  className="flex gap-2 items-center px-4 py-3 rounded-lg hover:dark:bg-zinc-700"
                >
                  <HardDrive className="h-4 w-4" /> Image Statistics
                </NavLink>
              </div>
            )}
          </div>
        </nav>

        {/* Mobile toggle */}
        <button
          className="md:hidden p-2 rounded-md"
          onClick={() => setOpen(!open)}
          aria-label="Toggle menu"
          aria-expanded={open}
        >
          {open ? <X /> : <Menu />}
        </button>
      </div>

      {/* Mobile menu */}
      {open && (
        <nav className="md:hidden bg-zinc-700 z-50">
          <div className="flex flex-col gap-2">
            <NavLink to="/" className="p-3 rounded-lg flex gap-2 items-center">
              <Home /> Home
            </NavLink>
            <NavLink to="/books" className="entity-books p-3 rounded-lg flex gap-2 items-center">
              <LibraryBig className="text-entity" /> Books
            </NavLink>
            <NavLink to="/movies" className="entity-movies p-3 rounded-lg flex gap-2 items-center">
              <Clapperboard className="text-entity" /> Movies
            </NavLink>
            <NavLink to="/games" className="entity-games p-3 rounded-lg flex gap-2 items-center">
              <Gamepad2 className="text-entity" /> Games
            </NavLink>
            <NavLink to="/musics" className="entity-musics p-3 rounded-lg flex gap-2 items-center">
              <Music className="text-entity" /> Music
            </NavLink>
            <hr className="border-zinc-600 mx-3" />
            <NavLink to="/image-stats" className="p-3 rounded-lg flex gap-2 items-center">
              <HardDrive className="h-4 w-4" /> Image Statistics
            </NavLink>
          </div>
        </nav>
      )}
    </header>
  );
}

const appVersion = import.meta.env.VITE_APP_VERSION || '0.0.0-local';

export default function App() {
  return (
    <html lang="en" className="dark">
      <head>
        <meta charSet="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <Meta />
        <Links />
      </head>
      <body className="text-base dark:bg-zinc-800 dark:text-slate-300">
        <div className="flex flex-col min-h-screen 2xl:mx-14">
          {/** Responsive header with mobile menu toggle */}
          <Header />
          <PendingNavigation />
          <main id="main-content" className="flex-1 dark:bg-zinc-900 p-2 lg:py-4 lg:px-8">
            <Outlet />
          </main>
          <Footer version={appVersion} />
        </div>
        <ScrollRestoration />
        <Scripts />
      </body>
    </html>
  );
}

export function ErrorBoundary() {
  const error = useRouteError();
  const routeError = isRouteErrorResponse(error);

  const statusCode = routeError ? error.status : 500;
  const statusText = routeError ? error.statusText : 'Unexpected error';
  const errorData = routeError ? error.data : error;

  const title = statusCode === 404 ? 'Page not found' : 'Something went wrong';
  const message =
    statusCode === 404
      ? 'We could not find the page you were looking for.'
      : 'An unexpected error occurred. You can head back to the dashboard while we look into it.';

  const showErrorDetails = (import.meta.env.VITE_SHOW_ERROR_DETAILS ?? '').toString().toLowerCase() === 'true';

  console.error('Root Error:', error);
  return (
    <html lang="en" className="dark">
      <head>
        <meta charSet="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <Meta />
        <Links />
      </head>
      <body className="text-base dark:bg-zinc-800 dark:text-slate-300">
        <div className="flex flex-col min-h-screen 2xl:mx-14">
          <Header />
          <PendingNavigation />
          <main
            id="main-content"
            className="flex flex-1 items-center justify-center bg-transparent p-2 dark:bg-zinc-900 lg:py-4 lg:px-8"
          >
            <ErrorScreen
              title={title}
              message={message}
              statusCode={statusCode}
              statusText={statusText}
              data={errorData}
              showDetails={showErrorDetails}
              onRetry={() => window.location.reload()}
            />
          </main>
          <Footer version={appVersion} />
        </div>
        <ScrollRestoration />
        <Scripts />
      </body>
    </html>
  );
}
