import { describe, it, expect, vi, afterEach } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import { createMemoryRouter, RouterProvider, type LoaderFunctionArgs } from 'react-router';
import CollectionList, { loader as listLoader } from './collection-list';
import CollectionDetail, { loader as detailLoader } from './collection-detail';
import { ErrorBoundary } from './collection';
import { Entity } from '~/models';

const loadList = (args: LoaderFunctionArgs) => listLoader(args as Parameters<typeof listLoader>[0]);
const loadDetail = (args: LoaderFunctionArgs) => detailLoader(args as Parameters<typeof detailLoader>[0]);

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe('collection routes', () => {
  it.each(Object.values(Entity))(
    'should load and render %s list URLs with query-preserving detail links',
    async (type) => {
      vi.mocked(fetch).mockResolvedValueOnce(
        Response.json({
          items: [
            { type, id: 'a'.repeat(24), title: 'A title', artist: 'An artist', platform: 'PC', authors: ['An author'] },
          ],
          page: 2,
          totalPages: 3,
          totalCount: 155,
        })
      );
      const path = `/${type.toLowerCase()}`;
      const router = createMemoryRouter([{ path: '/:entity', loader: loadList, Component: CollectionList }], {
        initialEntries: [`${path}?searchText=A&orderBy=title:desc&page=2`],
      });
      render(<RouterProvider router={router} />);
      expect(await screen.findByRole('heading', { name: type })).toBeInTheDocument();
      expect(screen.getByRole('searchbox')).toHaveValue('A');
      expect(screen.getByRole('search')).toHaveAttribute('action', path);
      expect(screen.getAllByRole('link', { name: 'A title' })[0]).toHaveAttribute(
        'href',
        `${path}/${'a'.repeat(24)}?searchText=A&orderBy=title%3Adesc&page=2`
      );
      expect(screen.getByRole('link', { name: 'Next' })).toHaveAttribute(
        'href',
        `${path}?searchText=A&orderBy=title%3Adesc&page=3`
      );
      expect(within(screen.getByRole('search')).getByDisplayValue('title:desc')).toHaveAttribute('name', 'orderBy');
    }
  );
  it('should show music detail fields, tracks and a link to the originating list', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(
      Response.json({
        type: Entity.Musics,
        id: 'a'.repeat(24),
        title: 'Album',
        artist: 'Artist',
        duration: 185000,
        discList: [{ trackNumber: 1, title: 'Song', duration: 61000 }],
      })
    );
    const router = createMemoryRouter(
      [{ path: '/:entity/:entityId', loader: loadDetail, Component: CollectionDetail }],
      { initialEntries: [`/musics/${'a'.repeat(24)}?page=2`] }
    );
    render(<RouterProvider router={router} />);
    expect(await screen.findByRole('heading', { name: 'Album' })).toBeInTheDocument();
    expect(screen.getByText('3:05')).toBeInTheDocument();
    expect(screen.getByText('Song')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Back to Musics' })).toHaveAttribute('href', '/musics?page=2');
  });
  it.each(['/unknown', '/Books', '/constructor'])(
    'should show a recoverable 404 for %s without requesting the API',
    async (path) => {
      const router = createMemoryRouter(
        [{ path: '/:entity', loader: loadList, Component: CollectionList, ErrorBoundary }],
        { initialEntries: [path] }
      );
      render(<RouterProvider router={router} />);
      expect(await screen.findByRole('alert')).toHaveTextContent('Media not found');
      expect(fetch).not.toHaveBeenCalled();
    }
  );
});
