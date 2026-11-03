import type { Handle } from 'remix/component';

import type { Integration } from '../api/integrations-data.ts';
import { AttributionBadges } from '../ui/attribution-badges.tsx';
import { Dashboard } from '../ui/dashboard/dashboard.tsx';
import type { DashboardStats } from '../ui/dashboard/stats.ts';
import { Document } from '../ui/document.tsx';

interface HomePageProps {
  path: string;
  /** Null when the stats could not be loaded. */
  stats: DashboardStats | null;
  integrations: Integration[];
}

export function HomePage(handle: Handle<HomePageProps>) {
  return () => {
    const { path, stats, integrations } = handle.props;
    return (
      <Document path={path} title="Dashboard | MediaSet" description="Your personal media collection dashboard">
        <div class="space-y-8">
          {stats ? <h1 class="sr-only">Dashboard</h1> : null}
          {stats ? (
            <Dashboard stats={stats} />
          ) : (
            <div class="flex flex-col items-center gap-3 py-8 text-center">
              <h1 class="text-xl">Couldn't load the dashboard</h1>
              <p>Something went wrong while loading your collection. Please try again.</p>
            </div>
          )}
          <AttributionBadges integrations={integrations} />
        </div>
      </Document>
    );
  };
}
