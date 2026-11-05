import * as assert from 'remix/assert';
import { describe, it } from 'remix/test';

import { seqEnvironmentName } from './serverLogger.ts';

describe('seqEnvironmentName', () => {
  it('should match the ASP.NET environment names the API sends', () => {
    assert.equal(seqEnvironmentName('development'), 'Development');
    assert.equal(seqEnvironmentName('production'), 'Production');
    assert.equal(seqEnvironmentName('test'), 'Testing');
  });

  it('should default to Development when NODE_ENV is unset or empty', () => {
    assert.equal(seqEnvironmentName(undefined), 'Development');
    assert.equal(seqEnvironmentName(''), 'Development');
  });

  it('should capitalize any other value', () => {
    assert.equal(seqEnvironmentName('staging'), 'Staging');
  });
});
