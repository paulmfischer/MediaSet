import { clientEntry, on, type Handle } from 'remix/component';

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
        <select
          name="plex"
          aria-label="Filter by Plex"
          class="w-full sm:w-auto bg-gray-800 border border-gray-600 text-white rounded-md px-2 py-2"
          mix={on('change', (event) => (event.currentTarget as HTMLSelectElement).form?.requestSubmit())}
        >
          {options.map((option) => (
            <option key={option.value} value={option.value} selected={option.value === value}>
              {option.label}
            </option>
          ))}
        </select>
      );
    };
  }
);
