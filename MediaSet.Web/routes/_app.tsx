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
      <body className="text-5xl lg:text-base text-slate-900 bg-zinc-200 dark:text-slate-300 dark:bg-zinc-800">
        <div className="max-w-8xl mx-auto px-4 sm:px-6 md:px-8">
          <div className="min-h-screen flex">
            <div id="sidebar" className="max-w-lg hidden lg:flex flex-col fixed inset-0 h-full lg:max-w-64 bg-zinc-200 dark:bg-zinc-800 z-50">
              <div className="mt-8 ml-8 mr-2">
                <div className="my-2">MediaSet</div>
                <Navigation />
              </div>
            </div>
            <div id="main" className="max-w-full grow flex flex-col lg:pl-56 bg-zinc-100 dark:bg-zinc-900">
              <div id="content" className="ml-10 m-5 mr-12 text-3xl lg:text-lg" data-pagefind-body>
                <Component />
              </div>
            </div>
          </div>
        </div>
      </body>
    </html>
  );
}
