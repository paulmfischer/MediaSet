import type { Handle } from 'remix/component';

import { Document } from '../ui/document.tsx';

export function HomePage(handle: Handle<{ path: string }>) {
  return () => (
    <Document
      path={handle.props.path}
      title="Dashboard | MediaSet"
      description="Your personal media collection dashboard"
    >
      <h1 class="text-2xl font-bold">Dashboard</h1>
    </Document>
  );
}
