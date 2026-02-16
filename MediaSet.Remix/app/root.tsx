import "./tailwind.css";
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
} from "@remix-run/react";
import { useEffect, useState } from "react";
import { Clapperboard, Gamepad2, Home, LibraryBig, Menu, Music, X } from "lucide-react";
import ErrorScreen from "./components/error-screen";
import PendingNavigation from "./components/pending-navigation";
import Footer from "./components/footer";

function Header() {
  const [open, setOpen] = useState(false);
  const location = useLocation();

  // Close mobile menu when navigation occurs
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setOpen(false);
  }, [location]);
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
          <NavLink to="/books" className="p-3 flex gap-2 items-center rounded-lg">
            <LibraryBig /> Books
          </NavLink>
          <NavLink to="/movies" className="p-3 flex gap-2 items-center rounded-lg">
            <Clapperboard /> Movies
          </NavLink>
          <NavLink to="/games" className="p-3 flex gap-2 items-center rounded-lg">
            <Gamepad2 /> Games
          </NavLink>
          <NavLink to="/musics" className="p-3 flex gap-2 items-center rounded-lg">
            <Music /> Music
          </NavLink>
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
            <NavLink to="/books" className="p-3 rounded-lg flex gap-2 items-center">
              <LibraryBig /> Books
            </NavLink>
            <NavLink to="/movies" className="p-3 rounded-lg flex gap-2 items-center">
              <Clapperboard /> Movies
            </NavLink>
            <NavLink to="/games" className="p-3 rounded-lg flex gap-2 items-center">
              <Gamepad2 /> Games
            </NavLink>
            <NavLink to="/musics" className="p-3 rounded-lg flex gap-2 items-center">
              <Music /> Music
            </NavLink>
          </div>
        </nav>
      )}
    </header>
  );
}

const appVersion = import.meta.env.VITE_APP_VERSION || "0.0.0-local";

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
  const statusText = routeError ? error.statusText : "Unexpected error";
  const errorData = routeError ? error.data : error;

  const title = statusCode === 404 ? "Page not found" : "Something went wrong";
  const message =
    statusCode === 404
      ? "We could not find the page you were looking for."
      : "An unexpected error occurred. You can head back to the dashboard while we look into it.";

  const showErrorDetails = (import.meta.env.VITE_SHOW_ERROR_DETAILS ?? "").toString().toLowerCase() === "true";

  console.error("Root Error:", error);
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
