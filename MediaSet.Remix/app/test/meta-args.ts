import type { Params } from '@remix-run/react';

export function createMetaArgs(params: Params<string>) {
  return {
    params,
    location: { pathname: '/', search: '', hash: '', state: null, key: 'test' },
    matches: [],
    data: undefined,
    error: undefined,
  };
}
