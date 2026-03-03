import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '~/test/test-utils';
import SortableColumnHeader from './sortable-column-header';

const mockNavigate = vi.fn();

vi.mock('@remix-run/react', async () => {
  const actual = await vi.importActual('@remix-run/react');
  return {
    ...actual,
    useNavigate: () => mockNavigate,
    useLocation: () => ({ pathname: '/books' }),
  };
});

describe('SortableColumnHeader', () => {
  beforeEach(() => {
    mockNavigate.mockClear();
  });

  it('should render label text', () => {
    render(
      <table>
        <thead>
          <tr>
            <SortableColumnHeader label="Title" field="title" currentOrderBy="title:asc" />
          </tr>
        </thead>
      </table>
    );

    expect(screen.getByText('Title')).toBeInTheDocument();
  });

  it('should show ascending indicator when active and ascending', () => {
    const { container } = render(
      <table>
        <thead>
          <tr>
            <SortableColumnHeader label="Title" field="title" currentOrderBy="title:asc" />
          </tr>
        </thead>
      </table>
    );

    expect(container.querySelector('svg')).toBeInTheDocument();
  });

  it('should show descending indicator when active and descending', () => {
    const { container } = render(
      <table>
        <thead>
          <tr>
            <SortableColumnHeader label="Title" field="title" currentOrderBy="title:desc" />
          </tr>
        </thead>
      </table>
    );

    expect(container.querySelector('svg')).toBeInTheDocument();
  });

  it('should not show indicator when column is not active', () => {
    const { container } = render(
      <table>
        <thead>
          <tr>
            <SortableColumnHeader label="Format" field="format" currentOrderBy="title:asc" />
          </tr>
        </thead>
      </table>
    );

    expect(container.querySelector('svg')).not.toBeInTheDocument();
  });

  it('should navigate ascending on first click when column is not active', () => {
    render(
      <table>
        <thead>
          <tr>
            <SortableColumnHeader label="Format" field="format" currentOrderBy="title:asc" />
          </tr>
        </thead>
      </table>
    );

    fireEvent.click(screen.getByText('Format'));

    expect(mockNavigate).toHaveBeenCalledWith('/books?orderBy=format%3Aasc');
  });

  it('should navigate descending when clicking active ascending column', () => {
    render(
      <table>
        <thead>
          <tr>
            <SortableColumnHeader label="Title" field="title" currentOrderBy="title:asc" />
          </tr>
        </thead>
      </table>
    );

    fireEvent.click(screen.getByText('Title'));

    expect(mockNavigate).toHaveBeenCalledWith('/books?orderBy=title%3Adesc');
  });

  it('should navigate ascending when clicking active descending column', () => {
    render(
      <table>
        <thead>
          <tr>
            <SortableColumnHeader label="Title" field="title" currentOrderBy="title:desc" />
          </tr>
        </thead>
      </table>
    );

    fireEvent.click(screen.getByText('Title'));

    expect(mockNavigate).toHaveBeenCalledWith('/books?orderBy=title%3Aasc');
  });

  it('should preserve searchText in navigation URL', () => {
    render(
      <table>
        <thead>
          <tr>
            <SortableColumnHeader label="Format" field="format" currentOrderBy="title:asc" searchText="gatsby" />
          </tr>
        </thead>
      </table>
    );

    fireEvent.click(screen.getByText('Format'));

    expect(mockNavigate).toHaveBeenCalledWith('/books?searchText=gatsby&orderBy=format%3Aasc');
  });

  it('should have aria-sort attribute reflecting current sort state', () => {
    render(
      <table>
        <thead>
          <tr>
            <SortableColumnHeader label="Title" field="title" currentOrderBy="title:asc" />
          </tr>
        </thead>
      </table>
    );

    expect(screen.getByRole('columnheader')).toHaveAttribute('aria-sort', 'ascending');
  });

  it('should have aria-sort none when column is not active', () => {
    render(
      <table>
        <thead>
          <tr>
            <SortableColumnHeader label="Format" field="format" currentOrderBy="title:asc" />
          </tr>
        </thead>
      </table>
    );

    expect(screen.getByRole('columnheader')).toHaveAttribute('aria-sort', 'none');
  });
});
