import { Outlet } from 'react-router';

/**
 * Parent layout route for all entity routes.
 * Provides router context without visual nesting.
 * Each child route renders its own complete UI.
 */
export default function EntityLayout() {
  return <Outlet />;
}
