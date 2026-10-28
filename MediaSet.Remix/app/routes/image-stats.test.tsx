import { beforeEach, describe, expect, it, vi } from 'vitest';
import { Entity } from '~/models';
import { deleteOrphanedImages, resetImageLookup } from '~/api/image-management-data';
import { action } from './image-stats';

vi.mock('~/api/image-management-data');
vi.mock('~/api/image-stats-data');

function submit(formData: FormData) {
  const request = new Request('http://localhost/image-stats', { method: 'POST', body: formData });
  return action({ request, url: new URL(request.url), pattern: '/image-stats', params: {}, context: {} });
}

function resetForm(entityType: string, entityIds = '["entity-1"]'): FormData {
  const formData = new FormData();
  formData.set('intent', 'reset-lookup');
  formData.set('entityType', entityType);
  formData.set('entityIds', entityIds);
  return formData;
}

describe('image-stats action', () => {
  beforeEach(() => vi.clearAllMocks());

  it.each([
    ['books', Entity.Books],
    ['movies', Entity.Movies],
    ['games', Entity.Games],
    ['musics', Entity.Musics],
  ])('should reset lookup for supported %s entities', async (input, entityType) => {
    const result = await submit(resetForm(input));

    expect(result).toBeNull();
    expect(resetImageLookup).toHaveBeenCalledWith(['entity-1'], entityType);
  });

  it.each(['', 'unknown'])('should reject unsupported entity type %j before resetting', async (entityType) => {
    await expect(submit(resetForm(entityType))).rejects.toMatchObject({ status: 404 });

    expect(resetImageLookup).not.toHaveBeenCalled();
  });

  it.each(['invalid JSON', '{}', '[1]'])('should reject invalid entity IDs %j before resetting', async (entityIds) => {
    const result = await submit(resetForm('books', entityIds));

    expect(result).toEqual({ error: 'Invalid entity IDs format.' });
    expect(resetImageLookup).not.toHaveBeenCalled();
  });

  it('should delete orphaned images when requested', async () => {
    const formData = new FormData();
    formData.set('intent', 'delete-orphaned');

    const result = await submit(formData);

    expect(result).toBeNull();
    expect(deleteOrphanedImages).toHaveBeenCalledOnce();
    expect(resetImageLookup).not.toHaveBeenCalled();
  });
});
