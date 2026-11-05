import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import config from '../tailwind.config.ts';

describe('tailwind config', () => {
  it('should apply the dark styles from the html.dark class, not the OS colour scheme', () => {
    // The page is dark-only; with the default `media` strategy a light-theme visitor gets light text on white.
    assert.equal(config.darkMode, 'class');
  });
});
