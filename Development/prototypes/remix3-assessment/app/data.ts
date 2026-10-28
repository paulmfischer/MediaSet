import { apiFetch } from './utils/apiFetch.server.ts';

export interface Item {
  id: string;
  title: string;
}

const ITEMS: Record<string, Item[]> = {
  books: [
    { id: '1', title: 'Dune' },
    { id: '2', title: 'Neuromancer' },
    { id: '3', title: 'Hyperion' },
  ],
  movies: [
    { id: '1', title: 'Alien' },
    { id: '2', title: 'Arrival' },
  ],
  games: [{ id: '1', title: 'Hades' }],
  musics: [{ id: '1', title: 'Kid A' }],
};

export const ENTITIES = Object.keys(ITEMS);

// Stand-in for the .NET API; goes through apiFetch only when API_URL is set so the trace header can be observed.
export async function searchItems(entity: string, search: string | null): Promise<Item[]> {
  if (process.env.API_URL) {
    let response = await apiFetch(`${process.env.API_URL}/${entity}?search=${encodeURIComponent(search ?? '')}`);
    return (await response.json()) as Item[];
  }
  await new Promise((resolve) => setTimeout(resolve, 400)); // make pending UI observable
  let needle = (search ?? '').toLowerCase();
  return (ITEMS[entity] ?? []).filter((i) => i.title.toLowerCase().includes(needle));
}

export function addItem(entity: string, item: Omit<Item, 'id'>): Item {
  let list = (ITEMS[entity] ??= []);
  let created = { ...item, id: String(list.length + 1) };
  list.push(created);
  return created;
}
