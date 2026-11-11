import { clientEntry, on, type Handle } from 'remix/component';

import { Icon } from '../../icon.tsx';
import { ChevronDown } from '../../icons.ts';

interface PlexFilterProps {
  /** The active filter value, `''` for all movies. */
  value: string;
  /** Every choice (`value` `''` is no filter). */
  options: Array<{ value: string; label: string }>;
}

// The movie list's Plex filter. It is a named select inside the search form, so without JavaScript the Search
// button applies it with the search text; with JavaScript choosing a value submits the form at once, so search text typed but not yet submitted is kept.
export const PlexFilterSelect = clientEntry(
  import.meta.url,
  function PlexFilterSelect(handle: Handle<PlexFilterProps>) {
    return () => {
      const { value, options } = handle.props;

      return (
        // The native arrow is hidden and drawn as an icon, so it can sit away from the edge of the input.
        <div class="relative w-full sm:w-auto">
          <select
            name="plex"
            aria-label="Filter by Plex"
            class="w-full appearance-none bg-gray-800 border border-gray-600 text-white rounded-md py-2 pl-3 pr-10"
            mix={on('change', (event) => (event.currentTarget as HTMLSelectElement).form?.requestSubmit())}
          >
            {options.map((option) => (
              <option key={option.value} value={option.value} selected={option.value === value}>
                {option.label}
              </option>
            ))}
          </select>
          <Icon
            icon={ChevronDown}
            size={16}
            class="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-slate-300"
          />
        </div>
      );
    };
  }
);
