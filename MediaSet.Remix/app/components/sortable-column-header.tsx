import { useLocation, useNavigate } from '@remix-run/react';
import { ChevronUp, ChevronDown } from 'lucide-react';

type Props = {
  label: string;
  field: string;
  currentOrderBy: string;
  searchText?: string | null;
  className?: string;
};

export default function SortableColumnHeader({ label, field, currentOrderBy, searchText, className = '' }: Props) {
  const navigate = useNavigate();
  const { pathname } = useLocation();

  const [currentField, currentDirection] = currentOrderBy.split(':');
  const isActive = currentField?.toLowerCase() === field.toLowerCase();
  const nextDirection = isActive && currentDirection === 'asc' ? 'desc' : 'asc';

  const handleClick = () => {
    const params = new URLSearchParams();
    if (searchText) params.set('searchText', searchText);
    params.set('orderBy', `${field}:${nextDirection}`);
    navigate(`${pathname}?${params.toString()}`);
  };

  return (
    <th
      className={`pl-2 p-1 border-r border-slate-800 cursor-pointer select-none ${className}`}
      onClick={handleClick}
      aria-sort={isActive ? (currentDirection === 'asc' ? 'ascending' : 'descending') : 'none'}
    >
      <span className="flex items-center gap-1">
        {label}
        {isActive && currentDirection === 'asc' && <ChevronUp size={14} />}
        {isActive && currentDirection === 'desc' && <ChevronDown size={14} />}
      </span>
    </th>
  );
}
