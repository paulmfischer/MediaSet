import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent } from '~/test/test-utils';
import Books from './books';
import { BookEntity } from '~/models';
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

describe('Books component', () => {
  const mockBooks: BookEntity[] = [
    {
      type: Entity.Books,
      id: '1',
      title: 'The Great Gatsby',
      authors: ['F. Scott Fitzgerald'],
      format: 'Hardcover',
      pages: 180,
    },
    {
      type: Entity.Books,
      id: '2',
      title: 'To Kill a Mockingbird',
      authors: ['Harper Lee'],
      format: 'Paperback',
      pages: 324,
    },
    {
      type: Entity.Books,
      id: '3',
      title: 'Multiple Authors Book',
      authors: ['Author One', 'Author Two', 'Author Three'],
      format: 'eBook',
      pages: 250,
    },
  ];

  describe('rendering', () => {
    it('should render a table with correct headers and all books', () => {
      render(<Books books={mockBooks} />);

      expect(screen.getByRole('table')).toBeInTheDocument();
      expect(screen.getByText('Title')).toBeInTheDocument();
      expect(screen.getByText('Cover')).toBeInTheDocument();
      expect(screen.getByText('Authors')).toBeInTheDocument();
      expect(screen.getByText('Format')).toBeInTheDocument();
      expect(screen.getByText('Pages')).toBeInTheDocument();

      expect(screen.getByText('The Great Gatsby')).toBeInTheDocument();
      expect(screen.getByText('To Kill a Mockingbird')).toBeInTheDocument();
      expect(screen.getByText('Multiple Authors Book')).toBeInTheDocument();
    });

    it('should display book titles as links to detail pages', () => {
      render(<Books books={mockBooks} />);

      expect(screen.getByText('The Great Gatsby')).toHaveAttribute('href', '/books/1');
      expect(screen.getByText('To Kill a Mockingbird')).toHaveAttribute('href', '/books/2');
    });

    it('should display authors and format correctly', () => {
      render(<Books books={mockBooks} />);

      expect(screen.getByText('F. Scott Fitzgerald')).toBeInTheDocument();
      expect(screen.getByText('Harper Lee')).toBeInTheDocument();
      expect(screen.getByText('Hardcover')).toBeInTheDocument();
      expect(screen.getByText('Paperback')).toBeInTheDocument();
      expect(screen.getByText('eBook')).toBeInTheDocument();
    });

    it('should format multiple authors comma-separated', () => {
      render(<Books books={mockBooks} />);

      expect(screen.getByText('Author One,Author Two,Author Three')).toBeInTheDocument();
    });

    it('should display page count for each book', () => {
      render(<Books books={mockBooks} />);

      expect(screen.getByText('180')).toBeInTheDocument();
      expect(screen.getByText('324')).toBeInTheDocument();
      expect(screen.getByText('250')).toBeInTheDocument();
    });

    it('should render edit and delete actions for each book', () => {
      render(<Books books={mockBooks} />);

      const editLinks = screen.getAllByRole('link', { name: /edit/i });
      expect(editLinks).toHaveLength(mockBooks.length);
      expect(editLinks[0]).toHaveAttribute('href', '/books/1/edit');
      expect(editLinks[1]).toHaveAttribute('href', '/books/2/edit');

      const deleteButtons = screen.getAllByRole('button', { name: /delete/i });
      expect(deleteButtons).toHaveLength(mockBooks.length);
    });

    it('should handle book with subtitle', () => {
      const booksWithSubtitle: BookEntity[] = [
        {
          type: Entity.Books,
          id: '1',
          title: 'A Book',
          subtitle: 'The Subtitle',
          authors: ['Author'],
        },
      ];

      render(<Books books={booksWithSubtitle} />);

      expect(screen.getByText('A Book: The Subtitle')).toBeInTheDocument();
    });

    it('should handle book without subtitle', () => {
      const booksWithoutSubtitle: BookEntity[] = [
        {
          type: Entity.Books,
          id: '1',
          title: 'A Book',
          authors: ['Author'],
        },
      ];

      render(<Books books={booksWithoutSubtitle} />);

      expect(screen.getByText('A Book')).toBeInTheDocument();
    });

    it('should handle empty authors array', () => {
      const booksNoAuthors: BookEntity[] = [
        {
          type: Entity.Books,
          id: '1',
          title: 'Anonymous Book',
          authors: [],
        },
      ];

      render(<Books books={booksNoAuthors} />);

      expect(screen.getByText('Anonymous Book')).toBeInTheDocument();
    });

    it('should handle book with undefined authors', () => {
      const booksNoAuthors: BookEntity[] = [
        {
          type: Entity.Books,
          id: '1',
          title: 'Anonymous Book',
        },
      ];

      render(<Books books={booksNoAuthors} />);

      expect(screen.getByText('Anonymous Book')).toBeInTheDocument();
    });
  });

  describe('delete dialog interactions', () => {
    it('should not show delete dialog initially', () => {
      render(<Books books={mockBooks} />);

      expect(screen.queryByTestId('delete-dialog')).not.toBeInTheDocument();
    });

    it('should show delete dialog when delete button is clicked', () => {
      render(<Books books={mockBooks} />);

      const firstDeleteButton = screen.getAllByRole('button', { name: /delete/i })[0];
      fireEvent.click(firstDeleteButton);

      expect(screen.getByTestId('delete-dialog')).toBeInTheDocument();
      expect(screen.getByText('Delete: The Great Gatsby')).toBeInTheDocument();
    });

    it('should close delete dialog when cancel is clicked', () => {
      render(<Books books={mockBooks} />);

      const firstDeleteButton = screen.getAllByRole('button', { name: /delete/i })[0];
      fireEvent.click(firstDeleteButton);

      expect(screen.getByTestId('delete-dialog')).toBeInTheDocument();

      const cancelButton = screen.getByText('Cancel');
      fireEvent.click(cancelButton);

      expect(screen.queryByTestId('delete-dialog')).not.toBeInTheDocument();
    });

    it('should have correct delete action link', () => {
      render(<Books books={mockBooks} />);

      const firstDeleteButton = screen.getAllByRole('button', { name: /delete/i })[0];
      fireEvent.click(firstDeleteButton);

      const deleteLink = screen.getByText('Confirm Delete');
      expect(deleteLink).toHaveAttribute('href', '/books/1/delete');
    });

    it('should show correct book title in delete dialog for different books', () => {
      render(<Books books={mockBooks} />);

      const deleteButtons = screen.getAllByRole('button', { name: /delete/i });
      fireEvent.click(deleteButtons[1]);

      expect(screen.getByText('Delete: To Kill a Mockingbird')).toBeInTheDocument();
    });

    it('should allow deleting multiple books sequentially', () => {
      render(<Books books={mockBooks} />);

      const deleteButtons = screen.getAllByRole('button', { name: /delete/i });

      // First delete
      fireEvent.click(deleteButtons[0]);
      expect(screen.getByText(/Delete: The Great Gatsby/)).toBeInTheDocument();

      // Cancel first delete
      fireEvent.click(screen.getByText('Cancel'));
      expect(screen.queryByTestId('delete-dialog')).not.toBeInTheDocument();

      // Second delete - need to re-query buttons since they may have changed
      const newDeleteButtons = screen.getAllByRole('button', { name: /delete/i });
      fireEvent.click(newDeleteButtons[2]); // Third book is now at index 2
      expect(screen.getByText(/Delete: Multiple Authors Book/)).toBeInTheDocument();
    });
  });

  describe('edge cases', () => {
    it('should handle empty books array', () => {
      render(<Books books={[]} />);

      const rows = screen.getAllByRole('row');
      // Only header row
      expect(rows).toHaveLength(1);
    });

    it('should handle single book', () => {
      const singleBook: BookEntity[] = [mockBooks[0]];
      render(<Books books={singleBook} />);

      expect(screen.getByText('The Great Gatsby')).toBeInTheDocument();
      expect(screen.getAllByRole('row')).toHaveLength(2); // header + 1 book
    });

    it('should handle very long title', () => {
      const longTitleBook: BookEntity[] = [
        {
          type: Entity.Books,
          id: '1',
          title: 'This is a very long book title that should still render properly in the table without breaking the layout',
          authors: ['Author'],
        },
      ];

      render(<Books books={longTitleBook} />);

      expect(screen.getByText(/This is a very long book title/)).toBeInTheDocument();
    });

    it('should trim trailing spaces in author names', () => {
      const booksWithSpacedAuthors: BookEntity[] = [
        {
          type: Entity.Books,
          id: '1',
          title: 'Test Book',
          authors: ['Author One   ', 'Author Two  '],
        },
      ];

      render(<Books books={booksWithSpacedAuthors} />);

      expect(screen.getByText('Author One,Author Two')).toBeInTheDocument();
    });
  });

  describe('accessibility', () => {
    it('should have proper aria labels on edit buttons', () => {
      render(<Books books={mockBooks} />);

      const editButtons = screen.getAllByRole('link', { name: /edit/i });
      expect(editButtons).toHaveLength(mockBooks.length);

      editButtons.forEach((btn: HTMLElement) => {
        expect(btn).toHaveAttribute('aria-label', 'Edit');
      });
    });

    it('should have proper aria labels on delete buttons', () => {
      render(<Books books={mockBooks} />);

      const deleteButtons = screen.getAllByRole('button', { name: /delete/i });
      expect(deleteButtons).toHaveLength(mockBooks.length);

      deleteButtons.forEach((btn: HTMLElement) => {
        expect(btn).toHaveAttribute('aria-label', 'Delete');
      });
    });

    it('should have title attributes for action buttons', () => {
      render(<Books books={mockBooks} />);

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
      render(<Books books={mockBooks} />);

      const table = screen.getByRole('table');
      expect(table).toBeInTheDocument();

      const thead = table.querySelector('thead');
      expect(thead).toBeInTheDocument();

      const tbody = table.querySelector('tbody');
      expect(tbody).toBeInTheDocument();
    });
  });
});
