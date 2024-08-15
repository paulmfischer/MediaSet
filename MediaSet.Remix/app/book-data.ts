type BookMutation = {
  id?: string;
  title: string;
  subTitle: string;
  isbn?: string;
  format?: string;
  pages?: number;
  publicationDate?: string;
  author?: string[];
  publisher?: string[];
  genre?: string[];
  plot?: string;
}

export type BookRecord = BookMutation & {
  id: string;
  createdAt: string;
}

const baseUrl = 'http://localhost:7130';

export async function searchBooks(searchText: string = '', orderBy: string = '') {
  const response = await fetch(`${baseUrl}/books/search?searchText=${searchText}&orderBy=${orderBy}`);
  if (response.status >= 300) {
    throw new Response("Error fetching data", { status: 500 });
  }
  return await response.json() as BookRecord[];
}