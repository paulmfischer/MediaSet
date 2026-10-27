import { index, route, type RouteConfig } from '@react-router/dev/routes';

export default [
  index('routes/home.tsx'),
  route(':entity', 'routes/collection.tsx', [
    index('routes/collection-list.tsx'),
    route(':entityId', 'routes/collection-detail.tsx'),
  ]),
  route('*', 'routes/not-found.tsx'),
] satisfies RouteConfig;
