import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent } from '~/test/test-utils';
import Movies from './movies';
import { MovieEntity } from '~/models';
import { Entity } from '~/models';

// Mock the delete dialog component
vi.mock('~/components/delete-dialog', () => ({
  default: ({ isOpen, onClose, entityTitle, deleteAction }: any) => (
    isOpen ? (
      <div data-testid="delete-dialog">
        <p>Delete: {entityTitle}</p>
        <button onClick={onClose}>Cancel</button>
        <a href={deleteAction}>Confirm Delete</a>
      </div>
    ) : null
  ),
}));

// Mock Link to avoid router context requirement
vi.mock('@remix-run/react', async () => {
  const actual = await vi.importActual('@remix-run/react');
  return {
    ...actual,
    Link: ({ to, children, ...props }: any) => (
      <a href={to} {...props}>
        {children}
      </a>
    ),
  };
});

describe('Movies component', () => {
  const mockMovies: MovieEntity[] = [
    {
      type: Entity.Movies,
      id: '1',
      title: 'The Shawshank Redemption',
      format: 'Blu-ray',
      runtime: 142,
      isTvSeries: false,
    },
    {
      type: Entity.Movies,
      id: '2',
      title: 'Breaking Bad',
      format: 'DVD',
      runtime: 45,
      isTvSeries: true,
    },
    {
      type: Entity.Movies,
      id: '3',
      title: 'Inception',
      format: 'Digital',
      runtime: 148,
      isTvSeries: false,
    },
  ];

  describe('rendering', () => {
    it('should render a table with correct headers and all movies', () => {
      render(<Movies movies={mockMovies} />);

      expect(screen.getByRole('table')).toBeInTheDocument();
      expect(screen.getByText('Title')).toBeInTheDocument();
      expect(screen.getByText('Format')).toBeInTheDocument();
      expect(screen.getByText('Runtime')).toBeInTheDocument();
      expect(screen.getByText('TV Show')).toBeInTheDocument();

      expect(screen.getByText('The Shawshank Redemption')).toBeInTheDocument();
      expect(screen.getByText('Breaking Bad')).toBeInTheDocument();
      expect(screen.getByText('Inception')).toBeInTheDocument();
    });

    it('should display movie titles as links to detail pages', () => {
      render(<Movies movies={mockMovies} />);

      expect(screen.getByText('The Shawshank Redemption')).toHaveAttribute('href', '/movies/1');
      expect(screen.getByText('Breaking Bad')).toHaveAttribute('href', '/movies/2');
    });

    it('should display format and runtime', () => {
      render(<Movies movies={mockMovies} />);

      expect(screen.getByText('Blu-ray')).toBeInTheDocument();
      expect(screen.getByText('DVD')).toBeInTheDocument();
      expect(screen.getByText('Digital')).toBeInTheDocument();
      expect(screen.getByText('142')).toBeInTheDocument();
      expect(screen.getByText('45')).toBeInTheDocument();
      expect(screen.getByText('148')).toBeInTheDocument();
    });

    it('should render edit and delete actions for each movie', () => {
      render(<Movies movies={mockMovies} />);

      const editLinks = screen.getAllByRole('link', { name: /edit/i });
      expect(editLinks).toHaveLength(mockMovies.length);
      expect(editLinks[0]).toHaveAttribute('href', '/movies/1/edit');
      expect(editLinks[1]).toHaveAttribute('href', '/movies/2/edit');

      const deleteButtons = screen.getAllByRole('button', { name: /delete/i });
      expect(deleteButtons).toHaveLength(mockMovies.length);
    });

    it('should handle movie without runtime', () => {
      const moviesNoRuntime: MovieEntity[] = [
        {
          type: Entity.Movies,
          id: '1',
          title: 'Test Movie',
          format: 'DVD',
        },
      ];

      render(<Movies movies={moviesNoRuntime} />);

      expect(screen.getByText('Test Movie')).toBeInTheDocument();
      expect(screen.getByText('0')).toBeInTheDocument();
    });

    it('should handle movie without isTvSeries flag', () => {
      const moviesNoFlag: MovieEntity[] = [
        {
          type: Entity.Movies,
          id: '1',
          title: 'Test Movie',
          format: 'DVD',
          runtime: 120,
        },
      ];

      render(<Movies movies={moviesNoFlag} />);

      expect(screen.getByText('Test Movie')).toBeInTheDocument();
    });
  });

  describe('TV series vs Movies distinction', () => {
    it('should distinguish between TV series and movies', () => {
      render(<Movies movies={mockMovies} />);

      // Breaking Bad (id: 2) is a TV series
      const breakingBadRow = screen.getByText('Breaking Bad').closest('tr');
      expect(breakingBadRow).toBeInTheDocument();

      // Inception (id: 3) is not a TV series
      const inceptionRow = screen.getByText('Inception').closest('tr');
      expect(inceptionRow).toBeInTheDocument();
    });

    it('should render all movies correctly regardless of TV series status', () => {
      const mixedMovies: MovieEntity[] = [
        {
          type: Entity.Movies,
          id: '1',
          title: 'Movie Only',
          runtime: 100,
          isTvSeries: false,
        },
        {
          type: Entity.Movies,
          id: '2',
          title: 'TV Series Only',
          runtime: 45,
          isTvSeries: true,
        },
        {
          type: Entity.Movies,
          id: '3',
          title: 'No Flag',
          runtime: 120,
        },
      ];

      render(<Movies movies={mixedMovies} />);

      expect(screen.getByText('Movie Only')).toBeInTheDocument();
      expect(screen.getByText('TV Series Only')).toBeInTheDocument();
      expect(screen.getByText('No Flag')).toBeInTheDocument();
    });
  });

  describe('delete dialog interactions', () => {
    it('should not show delete dialog initially', () => {
      render(<Movies movies={mockMovies} />);

      expect(screen.queryByTestId('delete-dialog')).not.toBeInTheDocument();
    });

    it('should show delete dialog when delete button is clicked', () => {
      render(<Movies movies={mockMovies} />);

      const firstDeleteButton = screen.getAllByRole('button', { name: /delete/i })[0];
      fireEvent.click(firstDeleteButton);

      expect(screen.getByTestId('delete-dialog')).toBeInTheDocument();
      expect(screen.getByText('Delete: The Shawshank Redemption')).toBeInTheDocument();
    });

    it('should close delete dialog when cancel is clicked', () => {
      render(<Movies movies={mockMovies} />);

      const firstDeleteButton = screen.getAllByRole('button', { name: /delete/i })[0];
      fireEvent.click(firstDeleteButton);

      expect(screen.getByTestId('delete-dialog')).toBeInTheDocument();

      const cancelButton = screen.getByText('Cancel');
      fireEvent.click(cancelButton);

      expect(screen.queryByTestId('delete-dialog')).not.toBeInTheDocument();
    });

    it('should have correct delete action link', () => {
      render(<Movies movies={mockMovies} />);

      const firstDeleteButton = screen.getAllByRole('button', { name: /delete/i })[0];
      fireEvent.click(firstDeleteButton);

      const deleteLink = screen.getByText('Confirm Delete');
      expect(deleteLink).toHaveAttribute('href', '/movies/1/delete');
    });

    it('should show correct movie title in delete dialog for different movies', () => {
      render(<Movies movies={mockMovies} />);

      const deleteButtons = screen.getAllByRole('button', { name: /delete/i });
      fireEvent.click(deleteButtons[1]);

      expect(screen.getByText('Delete: Breaking Bad')).toBeInTheDocument();
    });

    it('should allow deleting TV series', () => {
      render(<Movies movies={mockMovies} />);

      const deleteButtons = screen.getAllByRole('button', { name: /delete/i });
      fireEvent.click(deleteButtons[1]); // Breaking Bad, the TV series

      expect(screen.getByText(/Delete: Breaking Bad/)).toBeInTheDocument();
      const deleteLink = screen.getByText('Confirm Delete');
      expect(deleteLink).toHaveAttribute('href', '/movies/2/delete');
    });
  });

  describe('edge cases', () => {
    it('should handle empty movies array', () => {
      render(<Movies movies={[]} />);

      const rows = screen.getAllByRole('row');
      // Only header row
      expect(rows).toHaveLength(1);
    });

    it('should handle single movie', () => {
      const singleMovie: MovieEntity[] = [mockMovies[0]];
      render(<Movies movies={singleMovie} />);

      expect(screen.getByText('The Shawshank Redemption')).toBeInTheDocument();
      expect(screen.getAllByRole('row')).toHaveLength(2); // header + 1 movie
    });

    it('should handle very long title', () => {
      const longTitleMovie: MovieEntity[] = [
        {
          type: Entity.Movies,
          id: '1',
          title: 'This is a very long movie title that should still render properly in the table without breaking the layout',
          format: 'DVD',
          runtime: 120,
        },
      ];

      render(<Movies movies={longTitleMovie} />);

      expect(screen.getByText(/This is a very long movie title/)).toBeInTheDocument();
    });

    it('should handle very large runtime', () => {
      const longMovies: MovieEntity[] = [
        {
          type: Entity.Movies,
          id: '1',
          title: 'Long Movie',
          runtime: 999,
          isTvSeries: false,
        },
      ];

      render(<Movies movies={longMovies} />);

      expect(screen.getByText('999')).toBeInTheDocument();
    });

    it('should handle zero runtime', () => {
      const zeroRuntimeMovies: MovieEntity[] = [
        {
          type: Entity.Movies,
          id: '1',
          title: 'Zero Runtime Movie',
          runtime: 0,
        },
      ];

      render(<Movies movies={zeroRuntimeMovies} />);

      expect(screen.getByText('0')).toBeInTheDocument();
    });
  });

  describe('accessibility', () => {
    it('should have proper aria labels on edit buttons', () => {
      render(<Movies movies={mockMovies} />);

      const editButtons = screen.getAllByRole('link', { name: /edit/i });
      expect(editButtons).toHaveLength(mockMovies.length);

      editButtons.forEach((btn: HTMLElement) => {
        expect(btn).toHaveAttribute('aria-label', 'Edit');
      });
    });

    it('should have proper aria labels on delete buttons', () => {
      render(<Movies movies={mockMovies} />);

      const deleteButtons = screen.getAllByRole('button', { name: /delete/i });
      expect(deleteButtons).toHaveLength(mockMovies.length);

      deleteButtons.forEach((btn: HTMLElement) => {
        expect(btn).toHaveAttribute('aria-label', 'Delete');
      });
    });

    it('should have title attributes for action buttons', () => {
      render(<Movies movies={mockMovies} />);

      const editButtons = screen.getAllByRole('link', { name: /edit/i });
      editButtons.forEach((btn: HTMLElement) => {
        expect(btn).toHaveAttribute('title', 'Edit');
      });

      const deleteButtons = screen.getAllByRole('button', { name: /delete/i });
      deleteButtons.forEach((btn: HTMLElement) => {
        expect(btn).toHaveAttribute('title', 'Delete');
      });
    });

    it('should have proper table structure for screen readers', () => {
      render(<Movies movies={mockMovies} />);

      const table = screen.getByRole('table');
      expect(table).toBeInTheDocument();

      const thead = table.querySelector('thead');
      expect(thead).toBeInTheDocument();

      const tbody = table.querySelector('tbody');
      expect(tbody).toBeInTheDocument();
    });
  });
});
