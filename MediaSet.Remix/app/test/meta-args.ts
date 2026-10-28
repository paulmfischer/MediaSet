import type { Params } from 'react-router';

export function createMetaArgs(params: Params<string>) {
  return {
    params,
    location: { pathname: '/', search: '', hash: '', state: null, key: 'test' },
    matches: [],
    data: undefined,
    loaderData: undefined,
    error: undefined,
  };
}
