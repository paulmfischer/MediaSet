import type { Route } from './+types/home';
import { getApiHealth } from '../api.server';
import { publicApiUrl } from '../config.server';

export function meta() {
  return [{ title: 'MediaSet' }];
}

export async function loader({ request }: Route.LoaderArgs) {
  return {
    health: await getApiHealth(request.signal),
    publicApiUrl: publicApiUrl(),
  };
}

export default function Home({ loaderData }: Route.ComponentProps) {
  return (
    <section className="flex flex-col gap-4">
      <h2 className="text-2xl">MediaSet</h2>
      <p>
        API status: <span data-testid="api-status">{loaderData.health.status}</span>
      </p>
    </section>
  );
}
