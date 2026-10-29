import type { Option } from '../../../models.ts';

/**
 * Options matching the typed filter, plus an "add new" entry for text that is neither an existing
 * option nor already selected.
 */
export function filterOptions(
  options: Option[],
  filterText: string,
  addLabel: string,
  selectedValues: string[] = []
): Option[] {
  const filter = filterText.trim();
  if (!filter) return options;
  const lower = filter.toLowerCase();
  const matches = options.filter((option) => option.label.toLowerCase().includes(lower));
  const exists = [...options.map((option) => option.value), ...selectedValues].some(
    (value) => value.toLowerCase() === lower
  );
  return exists ? matches : [...matches, { label: `${addLabel} ${filter}`, value: filter, isNew: true }];
}
