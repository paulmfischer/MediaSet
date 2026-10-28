import type { Handle, RemixNode } from 'remix/component';
import { ImportMap } from 'remix/component/server';

import { scriptEntry } from '../assets.ts';

interface DocumentProps {
  title: string;
  description?: string;
  children?: RemixNode;
}

export function Document(handle: Handle<DocumentProps>) {
  return () => {
    const { children, description, title } = handle.props;
    const { href, importMap, preloads } = scriptEntry;

    return (
      <html lang="en" class="dark">
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
            <main id="main-content" class="flex-1 dark:bg-zinc-900 p-2 lg:py-4 lg:px-8">
              {children}
            </main>
          </div>
        </body>
      </html>
    );
  };
}
