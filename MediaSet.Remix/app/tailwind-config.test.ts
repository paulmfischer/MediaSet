import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

const css = readFileSync(fileURLToPath(new URL('./tailwind.css', import.meta.url)), 'utf8');

describe('tailwind config', () => {
  it('should apply the dark styles from the html.dark class, not the OS colour scheme', () => {
    // The page is dark-only; with the default `prefers-color-scheme` variant a light-theme visitor gets
    // light text on white.
    assert.match(css, /^@custom-variant dark \(&:where\(\.dark, \.dark \*\)\);$/m);
  });

  it('should keep the dark-only colour scheme on html.dark', () => {
    assert.match(css, /html\.dark\s*\{\s*color-scheme:\s*dark;\s*\}/);
  });

  it('should define the entity colour of each media type', () => {
    for (const [name, channels] of Object.entries({
      books: '74 222 128',
      movies: '248 113 113',
      games: '192 132 252',
      musics: '244 114 182',
    })) {
      assert.match(css, new RegExp(`\\.entity-${name}\\s*\\{\\s*--entity-color:\\s*${channels};`));
    }
  });
});
