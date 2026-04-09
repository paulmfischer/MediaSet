import { Link, useNavigate } from '@remix-run/react';
import { ChevronLeft, ChevronRight } from 'lucide-react';

type PaginationProps = {
  page: number;
  totalPages: number;
  totalCount: number;
  searchText?: string | null;
  orderBy?: string;
  basePath: string;
};

export default function Pagination({ page, totalPages, totalCount, searchText, orderBy, basePath }: PaginationProps) {
  const navigate = useNavigate();

  if (totalPages <= 1) return null;

  const buildUrl = (targetPage: number) => {
    const params = new URLSearchParams();
    if (searchText) params.set('searchText', searchText);
    if (orderBy) params.set('orderBy', orderBy);
    params.set('page', String(targetPage));
    return `${basePath}?${params.toString()}`;
  };

  return (
    <div className="sticky bottom-2 mb-2 flex items-center justify-center gap-6 py-3 px-4 bg-entity/20 border border-entity/30 rounded-lg backdrop-blur-sm">
      <span className="text-sm text-gray-400">{totalCount} items</span>
      {page > 1 ? (
        <Link to={buildUrl(page - 1)} className="flex items-center gap-1">
          <ChevronLeft size={16} /> Prev
        </Link>
      ) : (
        <span className="flex items-center gap-1 text-gray-600 cursor-not-allowed select-none">
          <ChevronLeft size={16} /> Prev
        </span>
      )}
      <select
        value={page}
        onChange={(e) => navigate(buildUrl(Number(e.target.value)))}
        className="bg-gray-800 border border-gray-600 text-white dark:text-white rounded px-2 py-1 text-sm"
        aria-label="Select page"
      >
        {Array.from({ length: totalPages }, (_, i) => i + 1).map((p) => (
          <option key={p} value={p}>
            Page {p} of {totalPages}
          </option>
        ))}
      </select>
      {page < totalPages ? (
        <Link to={buildUrl(page + 1)} className="flex items-center gap-1">
          Next <ChevronRight size={16} />
        </Link>
      ) : (
        <span className="flex items-center gap-1 text-gray-600 cursor-not-allowed select-none">
          Next <ChevronRight size={16} />
        </span>
      )}
    </div>
  );
}
