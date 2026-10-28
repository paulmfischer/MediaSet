import { form, get, route } from 'remix/routes';

export const routes = route({
  assets: get('/assets/*path'),
  health: get('/health'),
  config: get('/config.json'),
  home: get('/'),
  stats: get('/stats'),
  scan: get('/scan'),
  entities: get('/:entity'),
  add: form('/:entity/add'),
});
