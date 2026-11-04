import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import type { BookEntity, MovieEntity, MusicEntity } from '../../../models.ts';
import { toBookValues } from './book-form.ts';
import { toMovieValues } from './movie-form.ts';
import { toMusicValues } from './music-form.ts';

// The API serializes an unset number as `null`, although the models type it as optional.
const apiNull = null as unknown as undefined;

describe('form values from an item with unset numbers', () => {
  it('should leave a book page count blank', () => {
    assert.equal(toBookValues({ title: 'Book', pages: apiNull } as BookEntity).pages, '');
  });

  it('should leave a movie runtime blank', () => {
    assert.equal(toMovieValues({ title: 'Movie', runtime: apiNull } as MovieEntity).runtime, '');
  });

  it('should leave music track and disc counts blank', () => {
    const values = toMusicValues({ title: 'Album', tracks: apiNull, discs: apiNull } as MusicEntity);

    assert.equal(values.tracks, '');
    assert.equal(values.discs, '');
  });

  it('should keep a number that is set, including zero', () => {
    assert.equal(toBookValues({ title: 'Book', pages: 0 } as BookEntity).pages, '0');
  });
});
