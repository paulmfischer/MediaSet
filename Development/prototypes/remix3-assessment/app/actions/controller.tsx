import { createController } from 'remix/router';

import { assets } from '../assets.ts';
import { searchItems, ENTITIES } from '../data.ts';
import { routes } from '../routes.ts';
import { BarChart } from '../ui/bar-chart.tsx';
import { Document } from '../ui/document.tsx';
import { BarcodeScanner } from '../ui/public/barcode-scanner.tsx';

export default createController(routes, {
  actions: {
    async assets(context) {
      return (await assets.fetch(context.request)) ?? new Response('Not Found', { status: 404 });
    },
    health() {
      return Response.json({ status: 'ok' });
    },
    config() {
      return Response.json(
        {
          apiUrl: process.env.clientApiUrl ?? 'http://localhost:7130',
          showErrorDetails: false,
        },
        { headers: { 'Cache-Control': 'public, max-age=3600' } }
      );
    },
    home(context) {
      return context.render(
        <Document title="MediaSet" path="/">
          <h2 class="text-2xl">Home</h2>
        </Document>
      );
    },
    stats(context) {
      return context.render(
        <Document title="Image Statistics" path="/stats">
          <BarChart
            data={[
              { label: 'Books', value: 12 },
              { label: 'Movies', value: 7 },
              { label: 'Games', value: 3 },
            ]}
          />
        </Document>
      );
    },
    scan(context) {
      return context.render(
        <Document title="Scan" path="/scan">
          <BarcodeScanner />
        </Document>
      );
    },
    async entities(context) {
      let entity = context.params.entity;
      if (!ENTITIES.includes(entity)) {
        return context.render(
          <Document title="Not found" path="">
            <p>Unknown collection: {entity}</p>
          </Document>,
          { status: 404 }
        );
      }
      let search = context.url.searchParams.get('searchText');
      let items = await searchItems(entity, search);
      return context.render(
        <Document title={`${entity} List`} path={context.url.pathname}>
          <form method="get" role="search" class="flex gap-2">
            <input type="search" name="searchText" defaultValue={search ?? ''} aria-label={`Search ${entity}`} />
            <button type="submit">Search</button>
          </form>
          <ul class="mt-4">
            {items.map((i) => (
              <li key={i.id}>{i.title}</li>
            ))}
          </ul>
          {items.length === 0 && <p>No results</p>}
        </Document>
      );
    },
  },
});
