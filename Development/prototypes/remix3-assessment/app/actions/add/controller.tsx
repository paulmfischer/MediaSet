import * as s from 'remix/data-schema';
import { formData } from 'remix/middleware/form-data';
import { redirect } from 'remix/response/redirect';
import { createController } from 'remix/router';

import { addItem, ENTITIES } from '../../data.ts';
import { routes } from '../../routes.ts';
import { AddPage } from './page.tsx';
import { itemForm } from './schema.ts';

const text = (data: FormData, name: string) => {
  let value = data.get(name);
  return typeof value === 'string' ? value : '';
};

export default createController(routes.add, {
  middleware: [formData()],
  actions: {
    index(context) {
      let { entity } = context.params;
      if (!ENTITIES.includes(entity)) return new Response('Not found', { status: 404 });
      return context.render(<AddPage entity={entity} />);
    },
    action(context) {
      let { entity } = context.params;
      if (!ENTITIES.includes(entity)) return new Response('Not found', { status: 404 });

      let parsed = s.parseSafe(itemForm, context.formData);
      if (!parsed.success) {
        let values = {
          title: text(context.formData, 'title'),
          year: text(context.formData, 'year'),
          genres: context.formData.getAll('genres').filter((g): g is string => typeof g === 'string'),
        };
        return context.render(<AddPage entity={entity} values={values} issues={parsed.issues} />, { status: 400 });
      }

      addItem(entity, parsed.value);
      return redirect(routes.entities.href({ entity }), 303);
    },
  },
});
