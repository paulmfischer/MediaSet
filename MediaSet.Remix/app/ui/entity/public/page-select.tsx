import { clientEntry, navigate, on, type Handle } from 'remix/component';

interface PageSelectProps {
  page: number;
  totalPages: number;
  /** Href of every page, indexed from page 1. */
  hrefs: string[];
}

// Jump-to-page control. Without JavaScript the Prev/Next links still page through the list.
export const PageSelect = clientEntry(import.meta.url, function PageSelect(handle: Handle<PageSelectProps>) {
  return () => {
    const { page, totalPages, hrefs } = handle.props;

    return (
      <select
        aria-label="Select page"
        class="bg-gray-800 border border-gray-600 text-white rounded px-2 py-1 text-sm"
        mix={on('change', (event) => {
          const target = event.currentTarget as HTMLSelectElement;
          const href = hrefs[Number(target.value) - 1];
          if (href) void navigate(href);
        })}
      >
        {Array.from({ length: totalPages }, (_, index) => index + 1).map((value) => (
          <option key={value} value={value} selected={value === page}>
            Page {value} of {totalPages}
          </option>
        ))}
      </select>
    );
  };
});
