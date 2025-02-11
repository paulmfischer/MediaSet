import "./tailwind.css";
import {
  Links,
  Meta,
  NavLink,
  Outlet,
  Scripts,
  ScrollRestoration,
  useRouteError,
} from "@remix-run/react";
import { Clapperboard, LibraryBig } from "lucide-react";
import PendingNavigation from "./components/pending-navigation";

const year = new Date().getUTCFullYear();

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
          <div className="dark:bg-zinc-700 min-h-16 flex flex-row gap-16 items-center px-4">
            <h1 className="text-3xl">MediaSet</h1>
            <div className="flex flex-row gap-4 items-center">
              <NavLink to="/" className="p-3 flex items-center rounded-lg">Home</NavLink>
              <NavLink to="/books" className="p-3 flex gap-2 items-center rounded-lg"><LibraryBig /> Books</NavLink>
              <NavLink to="/movies" className="p-3 flex gap-2 items-center rounded-lg"><Clapperboard /> Movies</NavLink>
            </div>
          </div>
          <PendingNavigation />
          <main id="main-content" className="h-full dark:bg-zinc-900 py-4 px-8 overflow-scroll">
            <Outlet />
          </main>
          <footer className="min-h-12 flex flex-row items-center px-4 dark:bg-zinc-700">
            Copyright {year} Paul Fischer
          </footer>
        </div>
        <ScrollRestoration />
        <Scripts />
      </body>
    </html>
  );
}

export function ErrorBoundary() {
  const error = useRouteError();
  console.error('Root Error:', error);
  return (
    <html>
      <head>
        <title>Oh no!</title>
        <Meta />
        <Links />
      </head>
      <body>
        Something bad has happened!<br />
        <Scripts />
      </body>
    </html>
  );
}
