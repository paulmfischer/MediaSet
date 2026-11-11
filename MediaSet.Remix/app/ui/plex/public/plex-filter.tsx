import { clientEntry, navigate, on, type Handle } from 'remix/component';

interface PlexFilterProps {
  /** The active filter value, `''` for all movies. */
  value: string;
  /** Every choice with the list URL it leads to (`value` `''` is no filter), carrying the current search and sort. */
  options: Array<{ value: string; label: string; href: string }>;
}

// The movie list's Plex filter. It is a named select inside the search form, so without JavaScript the Search
// button applies it with the search text; with JavaScript choosing a value goes straight to that list URL.
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
          mix={on('change', (event) => {
            const href = options.find(
              (option) => option.value === (event.currentTarget as HTMLSelectElement).value
            )?.href;
            if (href) void navigate(href);
          })}
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
