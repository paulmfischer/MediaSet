import type { Handle } from 'remix/component';

import type { Integration } from '../api/integrations-data.ts';
import { routes } from '../routes.ts';
import { AttributionBadges } from '../ui/attribution-badges.tsx';
import { Dashboard } from '../ui/dashboard/dashboard.tsx';
import type { DashboardStats } from '../ui/dashboard/stats.ts';
import { Document } from '../ui/document.tsx';
import { ErrorPage } from '../ui/error-page.tsx';

interface HomePageProps {
  path: string;
  /** Null when the stats could not be loaded. */
  stats: DashboardStats | null;
  integrations: Integration[];
  /** Message and stack of the failure; only set when the server shows error details. */
  errorDetails?: string;
}

export function HomePage(handle: Handle<HomePageProps>) {
  return () => {
    const { path, stats, integrations, errorDetails } = handle.props;
    return (
      <Document path={path} title="Dashboard | MediaSet" description="Your personal media collection dashboard">
        <div class="space-y-8">
          {stats ? <h1 class="sr-only">Dashboard</h1> : null}
          {stats ? (
            <Dashboard stats={stats} />
          ) : (
            <ErrorPage
              heading="Couldn't load the dashboard"
              message="Something went wrong while loading your collection. Please try again."
              backHref={routes.entityLists.books.href()}
              backLabel="Browse your collection"
              details={errorDetails}
            />
          )}
          <AttributionBadges integrations={integrations} />
        </div>
      </Document>
    );
  };
}
