type BookMutation = {
  id?: string;
  title?: string;
  subtitle?: string;
  isbn?: string;
  format?: string;
  pages?: number;
  publicationDate?: string;
  author?: string[];
  publisher?: string[];
  genre?: string[];
  plot?: string;
}

export type BookRecord = BookMutation;

const baseUrl = 'http://localhost:7130';

export async function searchBooks(searchText: string | null, orderBy: string = '') {
  const response = await fetch(`${baseUrl}/books/search?searchText=${searchText ?? ''}&orderBy=${orderBy}`);
  if (!response.ok) {
    throw new Response("Error fetching data", { status: 500 });
  }
  return await response.json() as BookRecord[];
}

export async function getBook(id: string) {
  const response = await fetch(`${baseUrl}/books/${id}`);
  if (response.status == 404) {
    throw new Response("Book not found", { status: 404 });
  }

  return await response.json() as BookRecord;
}

export async function updatebook(id: string, book: BookMutation) {
  const response = await fetch(`${baseUrl}/books/${id}`, {
    method: 'PUT',
    body: JSON.stringify(book),
    headers: {
      "Content-Type": "application/json",
    }
  });

  if (!response.ok) {
    throw new Response("Error updating a book", { status: 500 });
  }
}

export async function addBook(book: BookMutation) {
  const response = await fetch(`${baseUrl}/books`, {
    method: 'POST',
    body: JSON.stringify(book),
    headers: { "Content-Type": "application/json" }
  });

  if (!response.ok) {
    throw new Response("Error creating a new book", { status: 500 });
  }

  return await response.json() as BookRecord;
}

export async function deleteBook(id: string) {
  const response = await fetch(`${baseUrl}/books/${id}`, { method: 'DELETE' });

  if (!response.ok) {
    throw new Response(`Error deleting a book ${id}`, { status: 500 });
  }
}