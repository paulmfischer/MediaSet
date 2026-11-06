import type { Locator, Page } from 'playwright';

type Entity = 'Book' | 'Movie' | 'Game' | 'Music';

/** The form's submit button: `Add <Entity>` on the add form, `Update` on the edit form (as on the old UI). */
export function submitButton(page: Page, mode: 'add' | 'edit', entity: Entity): Locator {
  return page.getByRole('button', { name: mode === 'add' ? `Add ${entity}` : 'Update', exact: true });
}
