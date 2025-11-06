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
} from "@remix-run/react";
import { useEffect, useState } from "react";
import { useLocation } from "@remix-run/react";
import { Clapperboard, Gamepad2, Home, LibraryBig, Menu, Music, X } from "lucide-react";

function Header() {
  const [open, setOpen] = useState(false);
  const location = useLocation();

  // Close mobile menu when navigation occurs
  useEffect(() => {
    setOpen(false);
  }, [location]);
  return (
    <header className="dark:bg-zinc-700 p-2 lg:px-8">
      <div className="flex items-center justify-between gap-4">
        <Link to="/">
          <h1 className="text-3xl">MediaSet</h1>
        </Link>

        {/* Desktop nav */}
        <nav className="hidden md:flex flex-row gap-4 items-center">
          <NavLink to="/" className="p-3 flex gap-2 items-center rounded-lg"><Home /> Home</NavLink>
          <NavLink to="/books" className="p-3 flex gap-2 items-center rounded-lg"><LibraryBig /> Books</NavLink>
          <NavLink to="/movies" className="p-3 flex gap-2 items-center rounded-lg"><Clapperboard /> Movies</NavLink>
          <NavLink to="/games" className="p-3 flex gap-2 items-center rounded-lg"><Gamepad2 /> Games</NavLink>
          <NavLink to="/musics" className="p-3 flex gap-2 items-center rounded-lg"><Music /> Music</NavLink>
        </nav>

        {/* Mobile toggle */}
        <button className="md:hidden p-2 rounded-md" onClick={() => setOpen(!open)} aria-label="Toggle menu" aria-expanded={open}>
          {open ? <X /> : <Menu />}
        </button>
      </div>

      {/* Mobile menu */}
      {open && (
        <nav className="md:hidden bg-zinc-700 z-50">
          <div className="flex flex-col gap-2">
            <NavLink to="/" className="p-3 rounded-lg flex gap-2 items-center"><Home /> Home</NavLink>
            <NavLink to="/books" className="p-3 rounded-lg flex gap-2 items-center"><LibraryBig /> Books</NavLink>
            <NavLink to="/movies" className="p-3 rounded-lg flex gap-2 items-center"><Clapperboard /> Movies</NavLink>
            <NavLink to="/games" className="p-3 rounded-lg flex gap-2 items-center"><Gamepad2 /> Games</NavLink>
            <NavLink to="/musics" className="p-3 rounded-lg flex gap-2 items-center"><Music /> Music</NavLink>
          </div>
        </nav>
      )}
    </header>
  );
}

function ErrorHeader() {
  const [open, setOpen] = useState(false);
  const location = useLocation();

  useEffect(() => {
    setOpen(false);
  }, [location]);
  return (
    <header className="dark:bg-zinc-700 min-h-16 px-4">
      <div className="max-w-7xl mx-auto flex items-center justify-between gap-4">
        <Link to="/">
          <h1 className="text-3xl">MediaSet</h1>
        </Link>
        <nav className="hidden md:flex flex-row gap-4 items-center">
          <NavLink to="/" className="p-3 flex gap-2 items-center rounded-lg"><Home /> Home</NavLink>
          <NavLink to="/books" className="p-3 flex gap-2 items-center rounded-lg"><LibraryBig /> Books</NavLink>
          <NavLink to="/movies" className="p-3 flex gap-2 items-center rounded-lg"><Clapperboard /> Movies</NavLink>
          <NavLink to="/games" className="p-3 flex gap-2 items-center rounded-lg"><Gamepad2 /> Games</NavLink>
          <NavLink to="/musics" className="p-3 flex gap-2 items-center rounded-lg"><Music /> Music</NavLink>
        </nav>
        <button className="md:hidden p-2 rounded-md" onClick={() => setOpen(!open)} aria-label="Toggle menu" aria-expanded={open}>
          {open ? <X /> : <Menu />}
        </button>
      </div>

      {open && (
        <nav className="md:hidden px-4 pb-4 bg-zinc-700 z-50">
          <div className="flex flex-col gap-2">
            <NavLink to="/" className="p-3 rounded-lg flex gap-2 items-center"><Home /> Home</NavLink>
            <NavLink to="/books" className="p-3 rounded-lg flex gap-2 items-center"><LibraryBig /> Books</NavLink>
            <NavLink to="/movies" className="p-3 rounded-lg flex gap-2 items-center"><Clapperboard /> Movies</NavLink>
            <NavLink to="/games" className="p-3 rounded-lg flex gap-2 items-center"><Gamepad2 /> Games</NavLink>
            <NavLink to="/musics" className="p-3 rounded-lg flex gap-2 items-center"><Music /> Music</NavLink>
          </div>
        </nav>
      )}
    </header>
  );
}
import PendingNavigation from "./components/pending-navigation";
import Footer from "./components/footer";

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
      <body className="text-base dark:bg-zinc-800 dark:text-slate-300 overflow-hidden">
        <div className="flex flex-col h-screen 2xl:mx-14">
          {/** Responsive header with mobile menu toggle */}
          <Header />
          <PendingNavigation />
          <main id="main-content" className="h-full dark:bg-zinc-900 p-2 lg:py-4 lg:px-8 overflow-scroll">
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

interface RouteError {
  data: string;
  internal: boolean;
  status: number;
  statusText: string;
};

export function ErrorBoundary() {
  const error = useRouteError() as RouteError;
  console.error('Root Error:', error);
  return (
    <html lang="en" className="dark">
      <head>
        <meta charSet="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <Meta />
        <Links />
      </head>
      <body className="text-base dark:bg-zinc-800 dark:text-slate-300 overflow-hidden">
        <div className="flex flex-col h-screen 2xl:mx-14">
          <ErrorHeader />
          <PendingNavigation />
          <main id="main-content" className="h-full dark:bg-zinc-900 py-2 px-4 lg:py-4 lg:px-8 overflow-scroll flex justify-center">
            An Error was experienced! <br />
            {error.data} <br />
            {error.status} <br />
            {error.statusText} <br />
          </main>
          <Footer version={appVersion} />
        </div>
        <ScrollRestoration />
        <Scripts />
      </body>
    </html>
  );
}
