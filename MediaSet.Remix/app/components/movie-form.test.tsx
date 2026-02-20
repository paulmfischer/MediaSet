import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '~/test/test-utils';
import userEvent from '@testing-library/user-event';
import MovieForm from './movie-form';
import { MovieEntity, Entity } from '~/models';

// Mock the custom input components
vi.mock('~/components/multiselect-input', () => ({
  default: ({ name, selectedValues }: { name: string; selectedValues?: string[] }) => (
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
  default: ({ name, selectedValue }: { name: string; selectedValue?: string }) => (
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

describe('MovieForm', () => {
  const defaultProps = {
    studios: [
      { label: 'Paramount Pictures', value: 'paramount' },
      { label: 'Universal Studios', value: 'universal' },
      { label: 'Warner Bros', value: 'warnerbros' },
    ],
    genres: [
      { label: 'Action', value: 'action' },
      { label: 'Comedy', value: 'comedy' },
      { label: 'Drama', value: 'drama' },
      { label: 'Sci-Fi', value: 'scifi' },
    ],
    formats: [
      { label: 'Blu-ray', value: 'bluray' },
      { label: 'DVD', value: 'dvd' },
      { label: '4K Ultra HD', value: '4k' },
    ],
    isSubmitting: false,
    barcodeLookupAvailable: true,
  };

  const mockMovie: MovieEntity = {
    id: 'movie-1',
    type: Entity.Movies,
    title: 'Inception',
    runtime: 148,
    releaseDate: '2010-07-16',
    isTvSeries: false,
    studios: ['paramount'],
    genres: ['action', 'scifi'],
    format: 'bluray',
    barcode: '9876543210',
    plot: 'A thief who steals corporate secrets through dream-sharing technology is given the inverse task of planting an idea.',
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Form Rendering', () => {
    it('should render all form fields', () => {
      render(<MovieForm {...defaultProps} />);

      expect(screen.getByLabelText('Title')).toBeInTheDocument();
      expect(screen.getByText('Format')).toBeInTheDocument();
      expect(screen.getByLabelText('Runtime')).toBeInTheDocument();
      expect(screen.getByLabelText('Release Date')).toBeInTheDocument();
      expect(screen.getByLabelText('Is TV Series')).toBeInTheDocument();
      expect(screen.getByText('Studios')).toBeInTheDocument();
      expect(screen.getByText('Genres')).toBeInTheDocument();
      expect(screen.getByLabelText('Barcode')).toBeInTheDocument();
      expect(screen.getByLabelText('Plot')).toBeInTheDocument();
    });

    it('should render the hidden id field', () => {
      render(<MovieForm {...defaultProps} movie={mockMovie} />);

      const idInput = screen.getByDisplayValue('movie-1') as HTMLInputElement;
      expect(idInput).toHaveAttribute('type', 'hidden');
      expect(idInput).toHaveAttribute('name', 'id');
    });

    it('should render the barcode lookup button', () => {
      render(<MovieForm {...defaultProps} />);

      const lookupButton = screen.getAllByRole('button', { name: /lookup/i })[1];
      expect(lookupButton).toBeInTheDocument();
      expect(lookupButton).toHaveAttribute('type', 'button');
    });

    it('should render all inputs with aria-labels', () => {
      render(<MovieForm {...defaultProps} />);

      expect(screen.getByLabelText('Title')).toHaveAttribute('aria-label', 'Title');
      expect(screen.getByLabelText('Runtime')).toHaveAttribute('aria-label', 'Runtime');
      expect(screen.getByLabelText('Release Date')).toHaveAttribute('aria-label', 'Release Date');
      expect(screen.getByLabelText('Barcode')).toHaveAttribute('aria-label', 'Barcode');
      expect(screen.getByLabelText('Plot')).toHaveAttribute('aria-label', 'Plot');
      expect(screen.getByLabelText('Is TV Series')).toHaveAttribute('aria-label', 'Is TV Series');
    });

    it('should render checkbox for TV series indicator', () => {
      render(<MovieForm {...defaultProps} />);

      const checkbox = screen.getByLabelText('Is TV Series') as HTMLInputElement;
      expect(checkbox).toHaveAttribute('type', 'checkbox');
      expect(checkbox).toHaveAttribute('name', 'isTvSeries');
    });
  });

  describe('Initial Value Loading', () => {
    it('should populate form fields with movie data', () => {
      render(<MovieForm {...defaultProps} movie={mockMovie} />);

      expect(screen.getByLabelText('Title')).toHaveValue('Inception');
      expect(screen.getByLabelText('Runtime')).toHaveValue(148);
      expect(screen.getByLabelText('Release Date')).toHaveValue('2010-07-16');
      expect(screen.getByLabelText('Barcode')).toHaveValue('9876543210');
      expect(screen.getByLabelText('Plot')).toHaveValue(
        'A thief who steals corporate secrets through dream-sharing technology is given the inverse task of planting an idea.'
      );
    });

    it('should pass initial values to multiselect and singleselect inputs', () => {
      render(<MovieForm {...defaultProps} movie={mockMovie} />);

      expect(screen.getByTestId('studios-multiselect')).toHaveValue('paramount');
      expect(screen.getByTestId('genres-multiselect')).toHaveValue('action,scifi');
      expect(screen.getByTestId('format-singleselect')).toHaveValue('bluray');
    });

    it('should render with empty values when no movie is provided', () => {
      render(<MovieForm {...defaultProps} />);

      expect(screen.getByLabelText('Title')).toHaveValue('');
      expect(screen.getByLabelText('Runtime')).toHaveValue(null);
      expect(screen.getByLabelText('Release Date')).toHaveValue('');
      expect(screen.getByLabelText('Barcode')).toHaveValue('');
      expect(screen.getByLabelText('Plot')).toHaveValue('');
    });

    it('should render with isTvSeries unchecked by default', () => {
      render(<MovieForm {...defaultProps} />);

      const checkbox = screen.getByLabelText('Is TV Series') as HTMLInputElement;
      expect(checkbox.checked).toBe(false);
    });

    it('should render with isTvSeries checked when movie is a TV series', () => {
      const tvSeriesMovie: MovieEntity = {
        ...mockMovie,
        isTvSeries: true,
      };

      render(<MovieForm {...defaultProps} movie={tvSeriesMovie} />);

      const checkbox = screen.getByLabelText('Is TV Series') as HTMLInputElement;
      expect(checkbox.checked).toBe(true);
    });

    it('should handle movie with partial data', () => {
      const partialMovie: MovieEntity = {
        type: Entity.Movies,
        id: 'movie-2',
        title: 'Partial Movie',
      };

      render(<MovieForm {...defaultProps} movie={partialMovie} />);

      expect(screen.getByLabelText('Title')).toHaveValue('Partial Movie');
      expect(screen.getByLabelText('Runtime')).toHaveValue(null);
      expect(screen.getByLabelText('Barcode')).toHaveValue('');
    });
  });

  describe('Input Changes and Validation', () => {
    it('should update text input when user types', async () => {
      const user = userEvent.setup();
      render(<MovieForm {...defaultProps} />);

      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      await user.type(titleInput, 'New Movie Title');

      expect(titleInput.value).toBe('New Movie Title');
    });

    it('should update runtime number input', async () => {
      const user = userEvent.setup();
      render(<MovieForm {...defaultProps} />);

      const runtimeInput = screen.getByLabelText('Runtime') as HTMLInputElement;
      await user.type(runtimeInput, '120');

      expect(runtimeInput.value).toBe('120');
    });

    it('should update release date input', async () => {
      const user = userEvent.setup();
      render(<MovieForm {...defaultProps} />);

      const dateInput = screen.getByLabelText('Release Date') as HTMLInputElement;
      await user.type(dateInput, '2023-12-01');

      expect(dateInput.value).toBe('2023-12-01');
    });

    it('should update barcode input', async () => {
      const user = userEvent.setup();
      render(<MovieForm {...defaultProps} />);

      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;
      await user.type(barcodeInput, '1234567890');

      expect(barcodeInput.value).toBe('1234567890');
    });

    it('should update plot textarea', async () => {
      const user = userEvent.setup();
      render(<MovieForm {...defaultProps} />);

      const plotInput = screen.getByLabelText('Plot') as HTMLTextAreaElement;
      await user.type(plotInput, 'This is an amazing plot.');

      expect(plotInput.value).toBe('This is an amazing plot.');
    });

    it('should toggle TV series checkbox', async () => {
      const user = userEvent.setup();
      render(<MovieForm {...defaultProps} />);

      const checkbox = screen.getByLabelText('Is TV Series') as HTMLInputElement;
      expect(checkbox.checked).toBe(false);

      await user.click(checkbox);
      expect(checkbox.checked).toBe(true);

      await user.click(checkbox);
      expect(checkbox.checked).toBe(false);
    });

    it('should handle clearing text fields', async () => {
      const user = userEvent.setup();
      render(<MovieForm {...defaultProps} movie={mockMovie} />);

      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      await user.clear(titleInput);

      expect(titleInput.value).toBe('');
    });

    it('should accept numbers in runtime field', async () => {
      const user = userEvent.setup();
      render(<MovieForm {...defaultProps} />);

      const runtimeInput = screen.getByLabelText('Runtime') as HTMLInputElement;
      await user.type(runtimeInput, '95');

      expect(runtimeInput.value).toBe('95');
    });

    it('should handle multiline text in plot field', async () => {
      const user = userEvent.setup();
      render(<MovieForm {...defaultProps} />);

      const plotInput = screen.getByLabelText('Plot') as HTMLTextAreaElement;
      await user.type(plotInput, 'Line 1{Enter}Line 2');

      expect(plotInput.value).toContain('Line 1');
      expect(plotInput.value).toContain('Line 2');
    });
  });

  describe('Form Submission', () => {
    it('should call submit when lookup button is clicked with barcode', async () => {
      const user = userEvent.setup();
      render(<MovieForm {...defaultProps} />);

      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;
      await user.type(barcodeInput, '1234567890');

      const lookupButton = screen.getAllByRole('button', { name: /lookup/i })[1];
      await user.click(lookupButton);

      expect(mockSubmit).toHaveBeenCalled();
    });

    it('should pass correct form data to submit on lookup', async () => {
      const user = userEvent.setup();
      render(<MovieForm {...defaultProps} />);

      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;
      await user.type(barcodeInput, '1234567890');

      const lookupButton = screen.getAllByRole('button', { name: /lookup/i })[1];
      await user.click(lookupButton);

      expect(mockSubmit).toHaveBeenCalledWith(expect.any(FormData), expect.objectContaining({ method: 'post' }));

      const callArgs = mockSubmit.mock.calls[0][0] as FormData;
      expect(callArgs.get('intent')).toBe('lookup');
      expect(callArgs.get('fieldName')).toBe('barcode');
      expect(callArgs.get('identifierValue')).toBe('1234567890');
    });

    it('should not call submit if barcode is empty', async () => {
      const user = userEvent.setup();
      render(<MovieForm {...defaultProps} />);

      const lookupButton = screen.getAllByRole('button', { name: /lookup/i })[1];
      await user.click(lookupButton);

      expect(mockSubmit).not.toHaveBeenCalled();
    });

    it('should not call submit if lookup button is disabled during submission', async () => {
      render(<MovieForm {...defaultProps} isSubmitting={true} />);

      const lookupButton = screen.getAllByRole('button', { name: /lookup/i })[1];
      expect(lookupButton).toBeDisabled();
    });

    it('should disable fieldset when isSubmitting is true', () => {
      const { container } = render(<MovieForm {...defaultProps} isSubmitting={true} />);

      const fieldset = container.querySelector('fieldset');
      expect(fieldset).toBeDisabled();
    });

    it('should enable fieldset when isSubmitting is false', () => {
      const { container } = render(<MovieForm {...defaultProps} isSubmitting={false} />);

      const fieldset = container.querySelector('fieldset');
      expect(fieldset).not.toBeDisabled();
    });
  });

  describe('Mock API Calls', () => {
    it('should format lookup request with correct field name', async () => {
      const user = userEvent.setup();
      render(<MovieForm {...defaultProps} />);

      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;
      await user.type(barcodeInput, '9876543210');

      const lookupButton = screen.getAllByRole('button', { name: /lookup/i })[1];
      await user.click(lookupButton);

      const formData = mockSubmit.mock.calls[0][0] as FormData;
      expect(formData.get('fieldName')).toBe('barcode');
    });

    it('should handle multiple lookup calls', async () => {
      const user = userEvent.setup();
      render(<MovieForm {...defaultProps} />);

      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;

      // First lookup
      await user.type(barcodeInput, '1111111111');
      let lookupButton = screen.getAllByRole('button', { name: /lookup/i })[1];
      await user.click(lookupButton);

      expect(mockSubmit).toHaveBeenCalledTimes(1);

      // Clear and second lookup
      await user.clear(barcodeInput);
      await user.type(barcodeInput, '2222222222');
      lookupButton = screen.getAllByRole('button', { name: /lookup/i })[1];
      await user.click(lookupButton);

      expect(mockSubmit).toHaveBeenCalledTimes(2);
    });

    it('should pass correct identifierValue to submit', async () => {
      const user = userEvent.setup();
      render(<MovieForm {...defaultProps} />);

      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;
      const testBarcode = '9999999999';
      await user.type(barcodeInput, testBarcode);

      const lookupButton = screen.getAllByRole('button', { name: /lookup/i })[1];
      await user.click(lookupButton);

      const formData = mockSubmit.mock.calls[0][0] as FormData;
      expect(formData.get('identifierValue')).toBe(testBarcode);
    });

    it('should set intent to lookup for API call', async () => {
      const user = userEvent.setup();
      render(<MovieForm {...defaultProps} />);

      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;
      await user.type(barcodeInput, '3333333333');

      const lookupButton = screen.getAllByRole('button', { name: /lookup/i })[1];
      await user.click(lookupButton);

      const formData = mockSubmit.mock.calls[0][0] as FormData;
      expect(formData.get('intent')).toBe('lookup');
    });
  });

  describe('Integration Tests', () => {
    it('should fill form with initial movie data and allow modifications', async () => {
      const user = userEvent.setup();
      render(<MovieForm {...defaultProps} movie={mockMovie} />);

      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      expect(titleInput).toHaveValue('Inception');

      await user.clear(titleInput);
      await user.type(titleInput, 'New Title');

      expect(titleInput.value).toBe('New Title');
    });

    it('should handle form with all fields populated', async () => {
      const user = userEvent.setup();
      render(<MovieForm {...defaultProps} movie={mockMovie} />);

      // Verify all fields are populated
      expect(screen.getByLabelText('Title')).toHaveValue('Inception');
      expect(screen.getByLabelText('Runtime')).toHaveValue(148);
      expect(screen.getByLabelText('Release Date')).toHaveValue('2010-07-16');
      expect(screen.getByLabelText('Barcode')).toHaveValue('9876543210');
      expect(screen.getByLabelText('Plot')).toHaveValue(
        'A thief who steals corporate secrets through dream-sharing technology is given the inverse task of planting an idea.'
      );

      // Modify one field
      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      await user.clear(titleInput);
      await user.type(titleInput, 'Updated Title');

      expect(titleInput.value).toBe('Updated Title');

      // Verify other fields unchanged
      expect(screen.getByLabelText('Barcode')).toHaveValue('9876543210');
      expect(screen.getByLabelText('Plot')).toHaveValue(
        'A thief who steals corporate secrets through dream-sharing technology is given the inverse task of planting an idea.'
      );
    });

    it('should trigger lookup and maintain form state', async () => {
      const user = userEvent.setup();
      render(<MovieForm {...defaultProps} movie={mockMovie} />);

      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      const initialTitle = titleInput.value;

      const lookupButton = screen.getAllByRole('button', { name: /lookup/i })[1];
      await user.click(lookupButton);

      // Title should remain unchanged after lookup attempt
      expect(screen.getByLabelText('Title')).toHaveValue(initialTitle);
    });

    it('should handle rapid input changes', async () => {
      const user = userEvent.setup();
      render(<MovieForm {...defaultProps} />);

      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;

      await user.type(titleInput, 'Test Movie');
      await user.type(barcodeInput, '1234567890');
      await user.type(titleInput, ' Updated');

      expect(titleInput.value).toBe('Test Movie Updated');
      expect(barcodeInput.value).toBe('1234567890');
    });

    it('should handle TV series checkbox state with other field changes', async () => {
      const user = userEvent.setup();
      render(<MovieForm {...defaultProps} movie={mockMovie} />);

      const checkbox = screen.getByLabelText('Is TV Series') as HTMLInputElement;
      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;

      // Change title
      await user.clear(titleInput);
      await user.type(titleInput, 'New Title');

      // Toggle checkbox
      await user.click(checkbox);

      // Verify both changes
      expect(titleInput.value).toBe('New Title');
      expect(checkbox.checked).toBe(true);
    });
  });

  describe('Accessibility', () => {
    it('should have proper label associations', () => {
      render(<MovieForm {...defaultProps} />);

      const titleLabel = screen.getByText('Title');
      const titleInput = screen.getByLabelText('Title');

      expect(titleLabel).toBeInTheDocument();
      expect(titleInput).toBeInTheDocument();
    });

    it('should have descriptive labels for all inputs', () => {
      render(<MovieForm {...defaultProps} />);

      const labels = [
        'Title',
        'Format',
        'Runtime',
        'Release Date',
        'Is TV Series',
        'Studios',
        'Genres',
        'Barcode',
        'Plot',
      ];

      labels.forEach((label) => {
        expect(screen.getByText(label)).toBeInTheDocument();
      });
    });

    it('should disable all inputs during submission', () => {
      const { container } = render(<MovieForm {...defaultProps} isSubmitting={true} />);

      const fieldset = container.querySelector('fieldset');

      expect(fieldset).toBeDisabled();
    });
  });
});
