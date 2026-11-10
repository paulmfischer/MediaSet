import type { InferOutput } from 'remix/data-schema';
import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { Entity, type BookEntity, type GameEntity, type MovieEntity, type MusicEntity } from '../../../models.ts';
import { toBookUpdate, type bookForm } from './book-form.ts';
import { toGameUpdate, type gameForm } from './game-form.ts';
import { toMovieUpdate, type movieForm } from './movie-form.ts';
import { toMusicEntity, toMusicUpdate, type musicForm } from './music-form.ts';

const image = {
  fileName: 'cover.jpg',
  contentType: 'image/jpeg',
  fileSize: 1,
  filePath: 'cover.jpg',
  createdAt: '2024-01-01',
  updatedAt: '2024-01-01',
};

// The payload is sent as JSON, so compare what is serialized: cleared fields must not be present.
const wire = (entity: object): Record<string, unknown> => JSON.parse(JSON.stringify(entity)) as Record<string, unknown>;

describe('toBookUpdate', () => {
  const existing: BookEntity = {
    type: Entity.Books,
    id: 'b1',
    title: 'Old',
    isbn: '123',
    pages: 100,
    imageUrl: 'https://img/old.jpg',
    coverImage: image,
    authors: ['A'],
    genres: [],
  };
  const parsed: InferOutput<typeof bookForm> = {
    title: 'New',
    isbn: '',
    subtitle: '',
    format: '',
    pages: undefined,
    publicationDate: '',
    authors: ['B'],
    genres: ['G'],
    publisher: '',
    plot: '',
    imageUrl: 'https://img/old.jpg',
  };

  it('should apply the form fields and drop cleared ones', () => {
    assert.deepEqual(wire(toBookUpdate(existing, parsed)), {
      type: Entity.Books,
      id: 'b1',
      title: 'New',
      imageUrl: 'https://img/old.jpg',
      coverImage: image,
      authors: ['B'],
      genres: ['G'],
    });
  });

  it('should drop the cover image and an unchanged image URL when removing the image', () => {
    const update = wire(toBookUpdate(existing, parsed, true));
    assert.equal('coverImage' in update, false);
    assert.equal('imageUrl' in update, false);
  });

  it('should keep a newly entered image URL when removing the image', () => {
    const update = wire(toBookUpdate(existing, { ...parsed, imageUrl: 'https://img/new.jpg' }, true));
    assert.deepEqual(update, {
      type: Entity.Books,
      id: 'b1',
      title: 'New',
      imageUrl: 'https://img/new.jpg',
      authors: ['B'],
      genres: ['G'],
    });
  });
});

describe('toMovieUpdate', () => {
  const existing: MovieEntity = {
    type: Entity.Movies,
    id: 'm1',
    title: 'Old',
    runtime: 90,
    plot: 'Plot',
    coverImage: image,
    imageUrl: 'https://img/old.jpg',
  };
  const parsed: InferOutput<typeof movieForm> = {
    title: 'New',
    barcode: '',
    format: 'DVD',
    runtime: undefined,
    releaseDate: '',
    isTvSeries: true,
    studios: [],
    genres: ['G'],
    plot: '',
    imageUrl: 'https://img/old.jpg',
  };

  it('should apply the form fields and drop cleared ones', () => {
    assert.deepEqual(wire(toMovieUpdate(existing, parsed)), {
      type: Entity.Movies,
      id: 'm1',
      title: 'New',
      format: 'DVD',
      isTvSeries: true,
      studios: [],
      genres: ['G'],
      coverImage: image,
      imageUrl: 'https://img/old.jpg',
    });
  });

  it('should keep an existing externalId when saving an edit', () => {
    const update = wire(toMovieUpdate({ ...existing, externalId: 'tmdbid:1234' }, parsed));
    assert.equal(update.externalId, 'tmdbid:1234');
  });

  it('should not add an externalId when the movie has none', () => {
    assert.equal('externalId' in wire(toMovieUpdate(existing, parsed)), false);
  });

  it('should not send the read-only Plex data back when saving an edit', () => {
    const plex = {
      movieId: 'm1',
      plexItemId: '1001',
      onPlex: true,
      linkSource: 'auto',
      missCount: 0,
      broken: false,
      hdr: false,
      subtitleLanguages: [],
      watched: false,
      viewCount: 0,
      lastSyncedAt: '2024-04-01T00:00:00Z',
    };
    assert.equal('plex' in wire(toMovieUpdate({ ...existing, plex }, parsed)), false);
  });

  it('should drop the cover image and an unchanged image URL when removing the image', () => {
    const update = wire(toMovieUpdate(existing, parsed, true));
    assert.equal('coverImage' in update, false);
    assert.equal('imageUrl' in update, false);
  });
});

describe('toGameUpdate', () => {
  const existing: GameEntity = {
    type: Entity.Games,
    id: 'g1',
    title: 'Old',
    platform: 'PC',
    coverImage: image,
    imageUrl: 'https://img/old.jpg',
  };
  const parsed: InferOutput<typeof gameForm> = {
    title: 'New',
    barcode: '',
    format: '',
    platform: '',
    releaseDate: '',
    rating: 'E',
    developers: ['D'],
    publishers: [],
    genres: [],
    description: '',
    imageUrl: 'https://img/old.jpg',
  };

  it('should apply the form fields and drop cleared ones', () => {
    assert.deepEqual(wire(toGameUpdate(existing, parsed)), {
      type: Entity.Games,
      id: 'g1',
      title: 'New',
      rating: 'E',
      developers: ['D'],
      publishers: [],
      genres: [],
      coverImage: image,
      imageUrl: 'https://img/old.jpg',
    });
  });

  it('should drop the cover image and an unchanged image URL when removing the image', () => {
    const update = wire(toGameUpdate(existing, parsed, true));
    assert.equal('coverImage' in update, false);
    assert.equal('imageUrl' in update, false);
  });
});

describe('toMusicUpdate', () => {
  const existing: MusicEntity = {
    type: Entity.Musics,
    id: 'x1',
    title: 'Old',
    artist: 'Artist',
    label: 'Label',
    duration: 65432,
    discList: [{ trackNumber: 1, title: 'One', duration: 3500 }],
    coverImage: image,
    imageUrl: 'https://img/old.jpg',
  };
  const parsed: InferOutput<typeof musicForm> = {
    title: 'New',
    artist: 'Artist',
    barcode: '',
    format: '',
    releaseDate: '',
    label: '',
    tracks: undefined,
    discs: undefined,
    durationMinutes: 1,
    durationSeconds: 5,
    genres: [],
    imageUrl: 'https://img/old.jpg',
  };

  it('should drop cleared fields, keep an unchanged duration and keep unchanged saved tracks', () => {
    const update = toMusicUpdate(existing, parsed, [{ trackNumber: 1, title: 'One', duration: 3000 }]);
    assert.deepEqual(wire(update), {
      type: Entity.Musics,
      id: 'x1',
      title: 'New',
      artist: 'Artist',
      duration: 65432,
      discList: [{ trackNumber: 1, title: 'One', duration: 3500 }],
      genres: [],
      coverImage: image,
      imageUrl: 'https://img/old.jpg',
    });
  });

  it('should replace a changed duration and remove the track list when none is submitted', () => {
    const update = toMusicUpdate(existing, { ...parsed, durationSeconds: 6 }, []);
    assert.equal(update.duration, 66000);
    assert.equal('discList' in wire(update), false);
  });

  it('should drop the cover image and an unchanged image URL when removing the image', () => {
    const update = wire(toMusicUpdate(existing, parsed, [], true));
    assert.equal('coverImage' in update, false);
    assert.equal('imageUrl' in update, false);
  });

  it('should leave empty optional fields out of a new album', () => {
    assert.deepEqual(wire(toMusicEntity(parsed)), {
      type: Entity.Musics,
      title: 'New',
      artist: 'Artist',
      duration: 65000,
      genres: [],
      imageUrl: 'https://img/old.jpg',
    });
  });
});
