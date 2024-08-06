import { type PageProps } from "$fresh/server.ts";
import { Navigation } from "../components/Navigation.tsx";

export default function App({ Component }: PageProps) {
  return (
    <html>
      <head>
        <meta charset="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1.0" />
        <title>MediaSet.Web</title>
        <link rel="stylesheet" href="/styles.css" />
      </head>
      <body className="text-base dark:bg-zinc-900 dark:text-slate-300">
        <div className="flex flex-col min-h-screen 2xl:mx-14">
          <div>
            <div class="lg:hidden flex mx-auto min-w-96 max-w-screen-2xl px-4 py-2 justify-start dark:bg-zinc-800">
              <div class="flex flex-row gap-8">
                <div className="text-xl">MediaSet</div>
                <Navigation className="ml-2" />
              </div>
            </div>
            <nav className="flex-shrink-0 hidden lg:block lg:px-4">
              <div className="fixed pt-4 w-48 flex overflow-hidden dark:bg-zinc-800">
                <div class="flex-1 h-screen overflow-y-auto pb-8 mx-4">
                  <div className="mb-2 text-xl">MediaSet</div>
                  <Navigation className="ml-2" />
                </div>
              </div>
            </nav>
            <div class="w-full min-w-96">
              <main class="lg:ml-52 mt-4 min-w-96 mx-auto">
                <div class="mx-4">
                  <Component />
                </div>
              </main>
            </div>
          </div>
        </div>
      </body>
    </html>
  );
}
