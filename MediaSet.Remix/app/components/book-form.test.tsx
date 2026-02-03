import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '~/test/test-utils';
import userEvent from '@testing-library/user-event';
import BookForm from './book-form';
import { BookEntity } from '~/models';

// Mock the custom input components
vi.mock('~/components/multiselect-input', () => ({
  default: ({ name, selectedValues, options }: any) => (
    <input
      data-testid={`${name}-multiselect`}
      type="hidden"
      name={name}
      value={selectedValues?.join(',') || ''}
      aria-label={name}
    />
  ),
}));

vi.mock('~/components/singleselect-input', () => ({
  default: ({ name, selectedValue, options }: any) => (
    <input
      data-testid={`${name}-singleselect`}
      type="hidden"
      name={name}
      value={selectedValue || ''}
      aria-label={name}
    />
  ),
}));

// Mock useSubmit
const mockSubmit = vi.fn();
vi.mock('@remix-run/react', async () => {
  const actual = await vi.importActual('@remix-run/react');
  return {
    ...actual,
    useSubmit: () => mockSubmit,
  };
});

describe('BookForm', () => {
  const defaultProps = {
    authors: [
      { label: 'F. Scott Fitzgerald', value: 'fitzgerald' },
      { label: 'Stephen King', value: 'king' },
    ],
    genres: [
      { label: 'Fiction', value: 'fiction' },
      { label: 'Mystery', value: 'mystery' },
      { label: 'Science Fiction', value: 'scifi' },
    ],
    publishers: [
      { label: 'Penguin Books', value: 'penguin' },
      { label: 'Random House', value: 'random' },
    ],
    formats: [
      { label: 'Hardcover', value: 'hardcover' },
      { label: 'Paperback', value: 'paperback' },
      { label: 'Ebook', value: 'ebook' },
    ],
    isSubmitting: false,
    isbnLookupAvailable: true,
  };

  const mockBook: BookEntity = {
    id: 'book-1',
    type: 'Books' as any,
    title: 'The Great Gatsby',
    subtitle: 'A Novel',
    isbn: '978-0743273565',
    pages: 180,
    publicationDate: '1925-04-10',
    authors: ['fitzgerald'],
    genres: ['fiction'],
    publisher: 'penguin',
    format: 'hardcover',
    plot: 'A classic American novel about the Jazz Age.',
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Form Rendering', () => {
    it('should render all form fields', () => {
      render(<BookForm {...defaultProps} />);

      expect(screen.getByLabelText('Title')).toBeInTheDocument();
      expect(screen.getByLabelText('Subtitle')).toBeInTheDocument();
      expect(screen.getByText('Format')).toBeInTheDocument();
      expect(screen.getByLabelText('Pages')).toBeInTheDocument();
      expect(screen.getByLabelText('Publication Date')).toBeInTheDocument();
      expect(screen.getByText('Authors')).toBeInTheDocument();
      expect(screen.getByText('Genres')).toBeInTheDocument();
      expect(screen.getByText('Publisher')).toBeInTheDocument();
      expect(screen.getByLabelText('ISBN')).toBeInTheDocument();
      expect(screen.getByLabelText('Plot')).toBeInTheDocument();
    });

    it('should render the hidden id field', () => {
      render(<BookForm {...defaultProps} book={mockBook} />);

      const idInput = screen.getByDisplayValue('book-1') as HTMLInputElement;
      expect(idInput).toHaveAttribute('type', 'hidden');
      expect(idInput).toHaveAttribute('name', 'id');
    });

    it('should render the ISBN lookup button', () => {
      render(<BookForm {...defaultProps} />);

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      expect(lookupButton).toBeInTheDocument();
      expect(lookupButton).toHaveAttribute('type', 'button');
    });

    it('should render all inputs with aria-labels', () => {
      render(<BookForm {...defaultProps} />);

      expect(screen.getByLabelText('Title')).toHaveAttribute('aria-label', 'Title');
      expect(screen.getByLabelText('Subtitle')).toHaveAttribute('aria-label', 'Subtitle');
      expect(screen.getByLabelText('Pages')).toHaveAttribute('aria-label', 'Pages');
      expect(screen.getByLabelText('Publication Date')).toHaveAttribute('aria-label', 'Publication Date');
      expect(screen.getByLabelText('ISBN')).toHaveAttribute('aria-label', 'ISBN');
      expect(screen.getByLabelText('Plot')).toHaveAttribute('aria-label', 'Plot');
    });
  });

  describe('Initial Value Loading', () => {
    it('should populate form fields with book data', () => {
      render(<BookForm {...defaultProps} book={mockBook} />);

      expect(screen.getByLabelText('Title')).toHaveValue('The Great Gatsby');
      expect(screen.getByLabelText('Subtitle')).toHaveValue('A Novel');
      expect(screen.getByLabelText('Pages')).toHaveValue(180);
      expect(screen.getByLabelText('Publication Date')).toHaveValue('1925-04-10');
      expect(screen.getByLabelText('ISBN')).toHaveValue('978-0743273565');
      expect(screen.getByLabelText('Plot')).toHaveValue(
        'A classic American novel about the Jazz Age.'
      );
    });

    it('should pass initial values to multiselect and singleselect inputs', () => {
      render(<BookForm {...defaultProps} book={mockBook} />);

      expect(screen.getByTestId('authors-multiselect')).toHaveValue('fitzgerald');
      expect(screen.getByTestId('genres-multiselect')).toHaveValue('fiction');
      expect(screen.getByTestId('publisher-singleselect')).toHaveValue('penguin');
      expect(screen.getByTestId('format-singleselect')).toHaveValue('hardcover');
    });

    it('should render with empty values when no book is provided', () => {
      render(<BookForm {...defaultProps} />);

      expect(screen.getByLabelText('Title')).toHaveValue('');
      expect(screen.getByLabelText('Subtitle')).toHaveValue('');
      expect(screen.getByLabelText('Pages')).toHaveValue(null);
      expect(screen.getByLabelText('ISBN')).toHaveValue('');
      expect(screen.getByLabelText('Plot')).toHaveValue('');
    });

    it('should handle book with partial data', () => {
      const partialBook: BookEntity = {
        type: 'Books' as any,
        id: 'book-2',
        title: 'Partial Book',
      };

      render(<BookForm {...defaultProps} book={partialBook} />);

      expect(screen.getByLabelText('Title')).toHaveValue('Partial Book');
      expect(screen.getByLabelText('Subtitle')).toHaveValue('');
      expect(screen.getByLabelText('ISBN')).toHaveValue('');
    });
  });

  describe('Input Changes and Validation', () => {
    it('should update text input when user types', async () => {
      const user = userEvent.setup();
      render(<BookForm {...defaultProps} />);

      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      await user.type(titleInput, 'New Book Title');

      expect(titleInput.value).toBe('New Book Title');
    });

    it('should update subtitle input', async () => {
      const user = userEvent.setup();
      render(<BookForm {...defaultProps} />);

      const subtitleInput = screen.getByLabelText('Subtitle') as HTMLInputElement;
      await user.type(subtitleInput, 'A Subtitle');

      expect(subtitleInput.value).toBe('A Subtitle');
    });

    it('should update pages number input', async () => {
      const user = userEvent.setup();
      render(<BookForm {...defaultProps} />);

      const pagesInput = screen.getByLabelText('Pages') as HTMLInputElement;
      await user.type(pagesInput, '350');

      expect(pagesInput.value).toBe('350');
    });

    it('should update publication date input', async () => {
      const user = userEvent.setup();
      render(<BookForm {...defaultProps} />);

      const dateInput = screen.getByLabelText('Publication Date') as HTMLInputElement;
      await user.type(dateInput, '2023-06-15');

      expect(dateInput.value).toBe('2023-06-15');
    });

    it('should update ISBN input', async () => {
      const user = userEvent.setup();
      render(<BookForm {...defaultProps} />);

      const isbnInput = screen.getByLabelText('ISBN') as HTMLInputElement;
      await user.type(isbnInput, '978-1234567890');

      expect(isbnInput.value).toBe('978-1234567890');
    });

    it('should update plot textarea', async () => {
      const user = userEvent.setup();
      render(<BookForm {...defaultProps} />);

      const plotInput = screen.getByLabelText('Plot') as HTMLTextAreaElement;
      await user.type(plotInput, 'This is a great plot.');

      expect(plotInput.value).toBe('This is a great plot.');
    });

    it('should handle clearing text fields', async () => {
      const user = userEvent.setup();
      render(<BookForm {...defaultProps} book={mockBook} />);

      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      await user.clear(titleInput);

      expect(titleInput.value).toBe('');
    });

    it('should accept numbers in pages field', async () => {
      const user = userEvent.setup();
      render(<BookForm {...defaultProps} />);

      const pagesInput = screen.getByLabelText('Pages') as HTMLInputElement;
      await user.type(pagesInput, '123');

      expect(pagesInput.value).toBe('123');
    });

    it('should allow special characters in ISBN', async () => {
      const user = userEvent.setup();
      render(<BookForm {...defaultProps} />);

      const isbnInput = screen.getByLabelText('ISBN') as HTMLInputElement;
      await user.type(isbnInput, '978-0-123-45678-9');

      expect(isbnInput.value).toBe('978-0-123-45678-9');
    });

    it('should handle multiline text in plot field', async () => {
      const user = userEvent.setup();
      render(<BookForm {...defaultProps} />);

      const plotInput = screen.getByLabelText('Plot') as HTMLTextAreaElement;
      await user.type(plotInput, 'Line 1{Enter}Line 2');

      expect(plotInput.value).toContain('Line 1');
      expect(plotInput.value).toContain('Line 2');
    });
  });

  describe('Form Submission', () => {
    it('should call submit when lookup button is clicked with ISBN', async () => {
      const user = userEvent.setup();
      render(<BookForm {...defaultProps} />);

      const isbnInput = screen.getByLabelText('ISBN') as HTMLInputElement;
      await user.type(isbnInput, '978-0743273565');

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      expect(mockSubmit).toHaveBeenCalled();
    });

    it('should pass correct form data to submit on lookup', async () => {
      const user = userEvent.setup();
      render(<BookForm {...defaultProps} />);

      const isbnInput = screen.getByLabelText('ISBN') as HTMLInputElement;
      await user.type(isbnInput, '978-0743273565');

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      expect(mockSubmit).toHaveBeenCalledWith(
        expect.any(FormData),
        expect.objectContaining({ method: 'post' })
      );

      const callArgs = mockSubmit.mock.calls[0][0] as FormData;
      expect(callArgs.get('intent')).toBe('lookup');
      expect(callArgs.get('fieldName')).toBe('isbn');
      expect(callArgs.get('identifierValue')).toBe('978-0743273565');
    });

    it('should not call submit if ISBN is empty', async () => {
      const user = userEvent.setup();
      render(<BookForm {...defaultProps} />);

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      expect(mockSubmit).not.toHaveBeenCalled();
    });

    it('should not call submit if lookup button is disabled during submission', async () => {
      const user = userEvent.setup();
      render(<BookForm {...defaultProps} isSubmitting={true} />);

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      expect(lookupButton).toBeDisabled();
    });

    it('should disable fieldset when isSubmitting is true', () => {
      const { container } = render(<BookForm {...defaultProps} isSubmitting={true} />);

      const fieldset = container.querySelector('fieldset');
      expect(fieldset).toBeDisabled();
    });

    it('should enable fieldset when isSubmitting is false', () => {
      const { container } = render(<BookForm {...defaultProps} isSubmitting={false} />);

      const fieldset = container.querySelector('fieldset');
      expect(fieldset).not.toBeDisabled();
    });
  });

  describe('Mock API Calls', () => {
    it('should format lookup request with correct field name', async () => {
      const user = userEvent.setup();
      render(<BookForm {...defaultProps} />);

      const isbnInput = screen.getByLabelText('ISBN') as HTMLInputElement;
      await user.type(isbnInput, '978-1111111111');

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      const formData = mockSubmit.mock.calls[0][0] as FormData;
      expect(formData.get('fieldName')).toBe('isbn');
    });

    it('should handle multiple lookup calls', async () => {
      const user = userEvent.setup();
      render(<BookForm {...defaultProps} />);

      const isbnInput = screen.getByLabelText('ISBN') as HTMLInputElement;

      // First lookup
      await user.type(isbnInput, '978-1111111111');
      let lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      expect(mockSubmit).toHaveBeenCalledTimes(1);

      // Clear and second lookup
      await user.clear(isbnInput);
      await user.type(isbnInput, '978-2222222222');
      lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      expect(mockSubmit).toHaveBeenCalledTimes(2);
    });

    it('should pass correct identifierValue to submit', async () => {
      const user = userEvent.setup();
      render(<BookForm {...defaultProps} />);

      const isbnInput = screen.getByLabelText('ISBN') as HTMLInputElement;
      const testIsbn = '978-9999999999';
      await user.type(isbnInput, testIsbn);

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      const formData = mockSubmit.mock.calls[0][0] as FormData;
      expect(formData.get('identifierValue')).toBe(testIsbn);
    });

    it('should set intent to lookup for API call', async () => {
      const user = userEvent.setup();
      render(<BookForm {...defaultProps} />);

      const isbnInput = screen.getByLabelText('ISBN') as HTMLInputElement;
      await user.type(isbnInput, '978-3333333333');

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      const formData = mockSubmit.mock.calls[0][0] as FormData;
      expect(formData.get('intent')).toBe('lookup');
    });
  });

  describe('Integration Tests', () => {
    it('should fill form with initial book data and allow modifications', async () => {
      const user = userEvent.setup();
      render(<BookForm {...defaultProps} book={mockBook} />);

      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      expect(titleInput).toHaveValue('The Great Gatsby');

      await user.clear(titleInput);
      await user.type(titleInput, 'New Title');

      expect(titleInput.value).toBe('New Title');
    });

    it('should handle form with all fields populated', async () => {
      const user = userEvent.setup();
      render(<BookForm {...defaultProps} book={mockBook} />);

      // Verify all fields are populated
      expect(screen.getByLabelText('Title')).toHaveValue('The Great Gatsby');
      expect(screen.getByLabelText('Subtitle')).toHaveValue('A Novel');
      expect(screen.getByLabelText('Pages')).toHaveValue(180);
      expect(screen.getByLabelText('Publication Date')).toHaveValue('1925-04-10');
      expect(screen.getByLabelText('ISBN')).toHaveValue('978-0743273565');
      expect(screen.getByLabelText('Plot')).toHaveValue(
        'A classic American novel about the Jazz Age.'
      );

      // Modify one field
      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      await user.clear(titleInput);
      await user.type(titleInput, 'Updated Title');

      expect(titleInput.value).toBe('Updated Title');

      // Verify other fields unchanged
      expect(screen.getByLabelText('ISBN')).toHaveValue('978-0743273565');
      expect(screen.getByLabelText('Plot')).toHaveValue(
        'A classic American novel about the Jazz Age.'
      );
    });

    it('should trigger lookup and maintain form state', async () => {
      const user = userEvent.setup();
      render(<BookForm {...defaultProps} book={mockBook} />);

      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      const initialTitle = titleInput.value;

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      // Title should remain unchanged after lookup attempt
      expect(screen.getByLabelText('Title')).toHaveValue(initialTitle);
    });

    it('should handle rapid input changes', async () => {
      const user = userEvent.setup();
      render(<BookForm {...defaultProps} />);

      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      const isbnInput = screen.getByLabelText('ISBN') as HTMLInputElement;

      await user.type(titleInput, 'Test Book');
      await user.type(isbnInput, '978-1234567890');
      await user.type(titleInput, ' Updated');

      expect(titleInput.value).toBe('Test Book Updated');
      expect(isbnInput.value).toBe('978-1234567890');
    });
  });

  describe('Accessibility', () => {
    it('should have proper label associations', () => {
      render(<BookForm {...defaultProps} />);

      const titleLabel = screen.getByText('Title');
      const titleInput = screen.getByLabelText('Title');

      expect(titleLabel).toBeInTheDocument();
      expect(titleInput).toBeInTheDocument();
    });

    it('should have descriptive labels for all inputs', () => {
      render(<BookForm {...defaultProps} />);

      const labels = [
        'Title',
        'Subtitle',
        'Format',
        'Pages',
        'Publication Date',
        'Authors',
        'Genres',
        'Publisher',
        'ISBN',
        'Plot',
      ];

      labels.forEach((label) => {
        expect(screen.getByText(label)).toBeInTheDocument();
      });
    });

    it('should disable all inputs during submission', () => {
      const { container } = render(<BookForm {...defaultProps} isSubmitting={true} />);

      const inputs = container.querySelectorAll('input, textarea, button');
      const fieldset = container.querySelector('fieldset');

      expect(fieldset).toBeDisabled();
    });
  });
});
