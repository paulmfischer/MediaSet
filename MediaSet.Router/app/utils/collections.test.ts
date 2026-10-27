import { describe, it, expect } from 'vitest';
import { collectionQuery, getCollection, duration, fieldValue } from './collections';
import { Entity } from '~/models';

describe('collection contracts', () => {
  it.each(['books', 'movies', 'games', 'musics'])(
    'should resolve lowercase %s URLs and preserve list query state',
    (name) => {
      const collection = getCollection(name);
      expect(collection.type.toLowerCase()).toBe(name);
      expect(collectionQuery(new URL('http://test/?searchText=A%26B&orderBy=title:desc&page=2'), collection)).toEqual({
        searchText: 'A&B',
        orderBy: 'title:desc',
        page: 2,
      });
    }
  );
  it.each(['Books', 'unknown', 'constructor', '__proto__', undefined])(
    'should reject invalid collection %s',
    (name) => {
      expect(() => getCollection(name)).toThrow();
    }
  );
  it.each(['0', '-1', 'NaN', 'Infinity', '2.5', '99999999999999999'])('should normalize invalid page %s', (page) => {
    expect(collectionQuery(new URL(`http://test/?page=${page}&orderBy=invalid:desc`), getCollection('books'))).toEqual({
      searchText: '',
      orderBy: 'title:asc',
      page: 1,
    });
  });
  it('should format media-specific arrays, durations and zero values', () => {
    expect(fieldValue({ type: Entity.Books, authors: ['Author  ', 'Second'], pages: 0 }, 'authors')).toBe(
      'Author, Second'
    );
    expect(fieldValue({ type: Entity.Books, pages: 0 }, 'pages')).toBe('0');
    expect(duration(185000)).toBe('3:05');
    expect(duration(null)).toBe('');
  });
});
