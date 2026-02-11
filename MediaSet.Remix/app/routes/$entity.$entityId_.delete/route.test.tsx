import { describe, it, expect, beforeEach, vi } from 'vitest';
import { action } from './route';
import * as entityData from '~/api/entity-data';
import * as helpers from '~/helpers';
import { Entity } from '~/models';
import { redirect } from '@remix-run/node';

// Mock modules
vi.mock('~/api/entity-data');
vi.mock('~/helpers');
vi.mock('@remix-run/node', async () => {
  const actual = await vi.importActual('@remix-run/node');
  return {
    ...actual,
    redirect: vi.fn(),
  };
});

const mockDeleteEntity = vi.mocked(entityData.deleteEntity);
const mockGetEntityFromParams = vi.mocked(helpers.getEntityFromParams);
const mockRedirect = vi.mocked(redirect);

describe('$entity.$entityId.delete route', () => {
  describe('action function', () => {
    beforeEach(() => {
      vi.clearAllMocks();
    });

    describe('delete action', () => {
      it('should delete entity and redirect to entity list', async () => {
        mockGetEntityFromParams.mockReturnValue(Entity.Books);
        mockRedirect.mockReturnValue(new Response(null, { status: 302, headers: { Location: '/books' } }) as any);
        mockDeleteEntity.mockResolvedValueOnce(undefined);

        await action({
          params: { entity: 'books', entityId: '123' },
        } as any);

        expect(mockDeleteEntity).toHaveBeenCalledWith(Entity.Books, '123');
        expect(mockRedirect).toHaveBeenCalledWith('/books');
      });

      it('should delete movie and redirect to movies list', async () => {
        mockGetEntityFromParams.mockReturnValue(Entity.Movies);
        mockRedirect.mockReturnValue(new Response(null, { status: 302, headers: { Location: '/movies' } }) as any);
        mockDeleteEntity.mockResolvedValueOnce(undefined);

        await action({
          params: { entity: 'movies', entityId: 'movie-456' },
        } as any);

        expect(mockDeleteEntity).toHaveBeenCalledWith(Entity.Movies, 'movie-456');
        expect(mockRedirect).toHaveBeenCalledWith('/movies');
      });

      it('should delete game and redirect to games list', async () => {
        mockGetEntityFromParams.mockReturnValue(Entity.Games);
        mockRedirect.mockReturnValue(new Response(null, { status: 302, headers: { Location: '/games' } }) as any);
        mockDeleteEntity.mockResolvedValueOnce(undefined);

        await action({
          params: { entity: 'games', entityId: 'game-789' },
        } as any);

        expect(mockDeleteEntity).toHaveBeenCalledWith(Entity.Games, 'game-789');
        expect(mockRedirect).toHaveBeenCalledWith('/games');
      });

      it('should delete music and redirect to musics list', async () => {
        mockGetEntityFromParams.mockReturnValue(Entity.Musics);
        mockRedirect.mockReturnValue(new Response(null, { status: 302, headers: { Location: '/musics' } }) as any);
        mockDeleteEntity.mockResolvedValueOnce(undefined);

        await action({
          params: { entity: 'musics', entityId: 'music-012' },
        } as any);

        expect(mockDeleteEntity).toHaveBeenCalledWith(Entity.Musics, 'music-012');
        expect(mockRedirect).toHaveBeenCalledWith('/musics');
      });

      it('should redirect to lowercase entity path', async () => {
        mockGetEntityFromParams.mockReturnValue(Entity.Books);
        mockRedirect.mockReturnValue(new Response(null, { status: 302, headers: { Location: '/books' } }) as any);
        mockDeleteEntity.mockResolvedValueOnce(undefined);

        await action({
          params: { entity: 'Books', entityId: '123' },
        } as any);

        // The redirect should be called with the lowercase version
        expect(mockRedirect).toHaveBeenCalledWith('/books');
      });
    });

    describe('parameter validation', () => {
      it('should throw error when entity param is missing', async () => {
        await expect(
          action({
            params: { entityId: '123' },
          } as any)
        ).rejects.toThrow();
      });

      it('should throw error when entityId param is missing', async () => {
        await expect(
          action({
            params: { entity: 'books' },
          } as any)
        ).rejects.toThrow();
      });

      it('should throw error when both params are missing', async () => {
        await expect(
          action({
            params: {},
          } as any)
        ).rejects.toThrow();
      });
    });

    describe('edge cases', () => {
      it('should handle entity ID with special characters', async () => {
        mockGetEntityFromParams.mockReturnValue(Entity.Books);
        mockRedirect.mockReturnValue(new Response(null, { status: 302, headers: { Location: '/books' } }) as any);
        mockDeleteEntity.mockResolvedValueOnce(undefined);

        await action({
          params: { entity: 'books', entityId: 'book-uuid-12345' },
        } as any);

        expect(mockDeleteEntity).toHaveBeenCalledWith(Entity.Books, 'book-uuid-12345');
      });

      it('should handle numeric entity ID', async () => {
        mockGetEntityFromParams.mockReturnValue(Entity.Movies);
        mockRedirect.mockReturnValue(new Response(null, { status: 302, headers: { Location: '/movies' } }) as any);
        mockDeleteEntity.mockResolvedValueOnce(undefined);

        await action({
          params: { entity: 'movies', entityId: '999' },
        } as any);

        expect(mockDeleteEntity).toHaveBeenCalledWith(Entity.Movies, '999');
      });

      it('should propagate deleteEntity errors', async () => {
        const error = new Error('API Error: Failed to delete entity');
        mockGetEntityFromParams.mockReturnValue(Entity.Books);
        mockDeleteEntity.mockRejectedValueOnce(error);

        await expect(
          action({
            params: { entity: 'books', entityId: '123' },
          } as any)
        ).rejects.toThrow('API Error: Failed to delete entity');
      });

      it('should handle delete of non-existent entity', async () => {
        const error = new Response('Entity not found', { status: 404 });
        mockGetEntityFromParams.mockReturnValue(Entity.Books);
        mockDeleteEntity.mockRejectedValueOnce(error);

        await expect(
          action({
            params: { entity: 'books', entityId: 'nonexistent-id' },
          } as any)
        ).rejects.toThrow();
      });

      it('should handle multiple successive deletes', async () => {
        mockGetEntityFromParams.mockReturnValue(Entity.Games);
        mockRedirect.mockReturnValue(new Response(null, { status: 302, headers: { Location: '/games' } }) as any);
        mockDeleteEntity.mockResolvedValue(undefined);

        await action({
          params: { entity: 'games', entityId: 'game-1' },
        } as any);

        await action({
          params: { entity: 'games', entityId: 'game-2' },
        } as any);

        expect(mockDeleteEntity).toHaveBeenCalledTimes(2);
        expect(mockDeleteEntity).toHaveBeenNthCalledWith(1, Entity.Games, 'game-1');
        expect(mockDeleteEntity).toHaveBeenNthCalledWith(2, Entity.Games, 'game-2');
      });
    });

    describe('redirect behavior', () => {
      it('should always call redirect function', async () => {
        mockGetEntityFromParams.mockReturnValue(Entity.Books);
        mockRedirect.mockReturnValue(new Response(null, { status: 302, headers: { Location: '/books' } }) as any);
        mockDeleteEntity.mockResolvedValueOnce(undefined);

        await action({
          params: { entity: 'books', entityId: '123' },
        } as any);

        expect(mockRedirect).toHaveBeenCalled();
      });

      it('should use getEntityFromParams result in redirect path', async () => {
        mockGetEntityFromParams.mockReturnValue(Entity.Musics);
        mockRedirect.mockReturnValue(new Response(null, { status: 302, headers: { Location: '/musics' } }) as any);
        mockDeleteEntity.mockResolvedValueOnce(undefined);

        await action({
          params: { entity: 'musics', entityId: 'album-123' },
        } as any);

        expect(mockRedirect).toHaveBeenCalledWith('/musics');
      });

      it('should return redirect response', async () => {
        mockGetEntityFromParams.mockReturnValue(Entity.Books);
        const redirectResponse = new Response(null, { status: 302, headers: { Location: '/books' } });
        mockRedirect.mockReturnValue(redirectResponse as any);
        mockDeleteEntity.mockResolvedValueOnce(undefined);

        const result = await action({
          params: { entity: 'books', entityId: '123' },
        } as any);

        expect(result).toBe(redirectResponse);
      });
    });
  });
});
