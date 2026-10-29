import type { Option } from '../../../models.ts';

/** Options matching the typed filter, plus an "add new" entry for text that matches none exactly. */
export function filterOptions(options: Option[], filterText: string, addLabel: string): Option[] {
  const filter = filterText.trim();
  if (!filter) return options;
  const lower = filter.toLowerCase();
  const matches = options.filter((option) => option.label.toLowerCase().includes(lower));
  const exists = options.some((option) => option.value.toLowerCase() === lower);
  return exists ? matches : [...matches, { label: `${addLabel} ${filter}`, value: filter, isNew: true }];
}
