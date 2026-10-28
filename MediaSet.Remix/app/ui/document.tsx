import type { Handle, RemixNode } from 'remix/component';
import { ImportMap } from 'remix/component/server';

import { scriptEntry } from '../assets.ts';
import { getAppVersion } from '../config.server.ts';
import { Footer } from './footer.tsx';
import { Header } from './header.tsx';
import { PendingNavigation } from './public/pending-navigation.tsx';

interface DocumentProps {
  /** Request path, used by the header to mark the active link. */
  path: string;
  title: string;
  description?: string;
  children?: RemixNode;
}

export function Document(handle: Handle<DocumentProps>) {
  return () => {
    const { children, description, path, title } = handle.props;
    const { href, importMap, preloads } = scriptEntry;

    return (
      <html lang="en">
        <head>
          <meta charSet="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>{title}</title>
          {description ? <meta name="description" content={description} /> : null}
          <link rel="stylesheet" href="/app.css" />
          <ImportMap value={importMap} />
          {preloads.map((preload) => (
            <link key={preload} rel="modulepreload" href={preload} />
          ))}
          <script type="module" src={href}></script>
        </head>
        <body class="text-base dark:bg-zinc-800 dark:text-slate-300">
          <div class="flex flex-col min-h-screen 2xl:mx-14">
            <a
              href="#main-content"
              class="sr-only focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus:z-50 focus:p-3 focus:rounded-lg focus:bg-zinc-200 focus:dark:bg-zinc-800"
            >
              Skip to main content
            </a>
            <Header path={path} />
            <PendingNavigation />
            <main id="main-content" class="flex-1 dark:bg-zinc-900 p-2 lg:py-4 lg:px-8">
              {children}
            </main>
            <Footer version={getAppVersion()} />
          </div>
        </body>
      </html>
    );
  };
}
