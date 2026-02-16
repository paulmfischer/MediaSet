import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '~/test/test-utils';
import userEvent from '@testing-library/user-event';
import GameForm from './game-form';
import { GameEntity, Entity } from '~/models';

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

describe('GameForm', () => {
  const defaultProps = {
    developers: [
      { label: 'CD Projekt Red', value: 'cdprojekt' },
      { label: 'Naughty Dog', value: 'naughtydog' },
    ],
    publishers: [
      { label: 'Sony Interactive', value: 'sony' },
      { label: 'Microsoft', value: 'microsoft' },
    ],
    genres: [
      { label: 'Action', value: 'action' },
      { label: 'RPG', value: 'rpg' },
      { label: 'Adventure', value: 'adventure' },
    ],
    formats: [
      { label: 'Physical', value: 'physical' },
      { label: 'Digital', value: 'digital' },
    ],
    platforms: [
      { label: 'PlayStation 5', value: 'ps5' },
      { label: 'Xbox Series X', value: 'xsx' },
      { label: 'PC', value: 'pc' },
    ],
    isSubmitting: false,
    barcodeLookupAvailable: true,
  };

  const mockGame: GameEntity = {
    id: 'game-1',
    type: Entity.Games,
    title: 'The Witcher 3: Wild Hunt',
    platform: 'ps5',
    format: 'physical',
    releaseDate: '2015-05-19',
    rating: 'M',
    developers: ['cdprojekt'],
    publishers: ['sony'],
    genres: ['action', 'rpg', 'adventure'],
    barcode: '9120000000001',
    description: 'An open-world action RPG based on the novels by Andrzej Sapkowski.',
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Form Rendering', () => {
    it('should render all form fields', () => {
      render(<GameForm {...defaultProps} />);

      expect(screen.getByLabelText('Title')).toBeInTheDocument();
      expect(screen.getByText('Platform')).toBeInTheDocument();
      expect(screen.getByText('Format')).toBeInTheDocument();
      expect(screen.getByLabelText('Release Date')).toBeInTheDocument();
      expect(screen.getByLabelText('Age Rating')).toBeInTheDocument();
      expect(screen.getByText('Developers')).toBeInTheDocument();
      expect(screen.getByText('Publishers')).toBeInTheDocument();
      expect(screen.getByText('Genres')).toBeInTheDocument();
      expect(screen.getByLabelText('Barcode')).toBeInTheDocument();
      expect(screen.getByLabelText('Description')).toBeInTheDocument();
    });

    it('should render the hidden id field', () => {
      render(<GameForm {...defaultProps} game={mockGame} />);

      const idInput = screen.getByDisplayValue('game-1') as HTMLInputElement;
      expect(idInput).toHaveAttribute('type', 'hidden');
      expect(idInput).toHaveAttribute('name', 'id');
    });

    it('should render the barcode lookup button', () => {
      render(<GameForm {...defaultProps} />);

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      expect(lookupButton).toBeInTheDocument();
      expect(lookupButton).toHaveAttribute('type', 'button');
    });

    it('should render all inputs with aria-labels', () => {
      render(<GameForm {...defaultProps} />);

      expect(screen.getByLabelText('Title')).toHaveAttribute('aria-label', 'Title');
      expect(screen.getByLabelText('Release Date')).toHaveAttribute('aria-label', 'Release Date');
      expect(screen.getByLabelText('Age Rating')).toHaveAttribute('aria-label', 'Age Rating');
      expect(screen.getByLabelText('Barcode')).toHaveAttribute('aria-label', 'Barcode');
      expect(screen.getByLabelText('Description')).toHaveAttribute('aria-label', 'Description');
    });

    it('should have proper label associations', () => {
      render(<GameForm {...defaultProps} />);

      const titleLabel = screen.getByText('Title');
      const titleInput = screen.getByLabelText('Title');

      expect(titleLabel).toBeInTheDocument();
      expect(titleInput).toBeInTheDocument();
    });

    it('should render textarea for description', () => {
      render(<GameForm {...defaultProps} />);

      const descriptionTextarea = screen.getByLabelText('Description') as HTMLTextAreaElement;
      expect(descriptionTextarea.tagName).toBe('TEXTAREA');
    });
  });

  describe('Initial Value Loading', () => {
    it('should populate form fields with game data', () => {
      render(<GameForm {...defaultProps} game={mockGame} />);

      expect(screen.getByLabelText('Title')).toHaveValue('The Witcher 3: Wild Hunt');
      expect(screen.getByLabelText('Release Date')).toHaveValue('2015-05-19');
      expect(screen.getByLabelText('Age Rating')).toHaveValue('M');
      expect(screen.getByLabelText('Barcode')).toHaveValue('9120000000001');
      expect(screen.getByLabelText('Description')).toHaveValue(
        'An open-world action RPG based on the novels by Andrzej Sapkowski.'
      );
    });

    it('should pass initial values to multiselect and singleselect inputs', () => {
      render(<GameForm {...defaultProps} game={mockGame} />);

      expect(screen.getByTestId('developers-multiselect')).toHaveValue('cdprojekt');
      expect(screen.getByTestId('publishers-multiselect')).toHaveValue('sony');
      expect(screen.getByTestId('genres-multiselect')).toHaveValue('action,rpg,adventure');
      expect(screen.getByTestId('platform-singleselect')).toHaveValue('ps5');
      expect(screen.getByTestId('format-singleselect')).toHaveValue('physical');
    });

    it('should render with empty values when no game is provided', () => {
      render(<GameForm {...defaultProps} />);

      expect(screen.getByLabelText('Title')).toHaveValue('');
      expect(screen.getByLabelText('Release Date')).toHaveValue('');
      expect(screen.getByLabelText('Age Rating')).toHaveValue('');
      expect(screen.getByLabelText('Barcode')).toHaveValue('');
      expect(screen.getByLabelText('Description')).toHaveValue('');
    });

    it('should handle game with partial data', () => {
      const partialGame: GameEntity = {
        type: Entity.Games,
        id: 'game-2',
        title: 'Partial Game',
      };

      render(<GameForm {...defaultProps} game={partialGame} />);

      expect(screen.getByLabelText('Title')).toHaveValue('Partial Game');
      expect(screen.getByLabelText('Release Date')).toHaveValue('');
      expect(screen.getByLabelText('Barcode')).toHaveValue('');
    });

    it('should load empty multiselect values when game has no developers', () => {
      const gameNoMetadata: GameEntity = {
        ...mockGame,
        developers: undefined,
        publishers: undefined,
        genres: undefined,
      };

      render(<GameForm {...defaultProps} game={gameNoMetadata} />);

      expect(screen.getByTestId('developers-multiselect')).toHaveValue('');
      expect(screen.getByTestId('publishers-multiselect')).toHaveValue('');
      expect(screen.getByTestId('genres-multiselect')).toHaveValue('');
    });
  });

  describe('Input Changes and Validation', () => {
    it('should update title input when user types', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} />);

      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      await user.type(titleInput, 'New Game Title');

      expect(titleInput.value).toBe('New Game Title');
    });

    it('should update release date input', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} />);

      const dateInput = screen.getByLabelText('Release Date') as HTMLInputElement;
      await user.type(dateInput, '2024-01-15');

      expect(dateInput.value).toBe('2024-01-15');
    });

    it('should update age rating input', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} />);

      const ratingInput = screen.getByLabelText('Age Rating') as HTMLInputElement;
      await user.type(ratingInput, 'T');

      expect(ratingInput.value).toBe('T');
    });

    it('should update barcode input', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} />);

      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;
      await user.type(barcodeInput, '9876543210987');

      expect(barcodeInput.value).toBe('9876543210987');
    });

    it('should update description textarea', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} />);

      const descriptionInput = screen.getByLabelText('Description') as HTMLTextAreaElement;
      await user.type(descriptionInput, 'This is an amazing game.');

      expect(descriptionInput.value).toBe('This is an amazing game.');
    });

    it('should handle clearing text fields', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} game={mockGame} />);

      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      await user.clear(titleInput);

      expect(titleInput.value).toBe('');
    });

    it('should allow numeric values in age rating', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} />);

      const ratingInput = screen.getByLabelText('Age Rating') as HTMLInputElement;
      await user.type(ratingInput, '18');

      expect(ratingInput.value).toBe('18');
    });

    it('should handle special characters in barcode', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} />);

      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;
      await user.type(barcodeInput, '978-0-123-45678-9');

      expect(barcodeInput.value).toBe('978-0-123-45678-9');
    });

    it('should handle multiline text in description field', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} />);

      const descriptionInput = screen.getByLabelText('Description') as HTMLTextAreaElement;
      await user.type(descriptionInput, 'Line 1{Enter}Line 2');

      expect(descriptionInput.value).toContain('Line 1');
      expect(descriptionInput.value).toContain('Line 2');
    });

    it('should preserve input values during rapid changes', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} />);

      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      const ratingInput = screen.getByLabelText('Age Rating') as HTMLInputElement;

      await user.type(titleInput, 'Game A');
      await user.type(ratingInput, 'M');
      await user.type(titleInput, ' Updated');

      expect(titleInput.value).toBe('Game A Updated');
      expect(ratingInput.value).toBe('M');
    });
  });

  describe('Form Submission', () => {
    it('should call submit when lookup button is clicked with barcode', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} />);

      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;
      await user.type(barcodeInput, '9120000000001');

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      expect(mockSubmit).toHaveBeenCalled();
    });

    it('should pass correct form data to submit on lookup', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} />);

      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;
      await user.type(barcodeInput, '9120000000001');

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      expect(mockSubmit).toHaveBeenCalledWith(expect.any(FormData), expect.objectContaining({ method: 'post' }));

      const callArgs = mockSubmit.mock.calls[0][0] as FormData;
      expect(callArgs.get('intent')).toBe('lookup');
      expect(callArgs.get('fieldName')).toBe('barcode');
      expect(callArgs.get('identifierValue')).toBe('9120000000001');
    });

    it('should not call submit if barcode is empty', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} />);

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      expect(mockSubmit).not.toHaveBeenCalled();
    });

    it('should not call submit if lookup button is disabled during submission', async () => {
      render(<GameForm {...defaultProps} isSubmitting={true} />);

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      expect(lookupButton).toBeDisabled();
    });

    it('should disable fieldset when isSubmitting is true', () => {
      const { container } = render(<GameForm {...defaultProps} isSubmitting={true} />);

      const fieldset = container.querySelector('fieldset');
      expect(fieldset).toBeDisabled();
    });

    it('should enable fieldset when isSubmitting is false', () => {
      const { container } = render(<GameForm {...defaultProps} isSubmitting={false} />);

      const fieldset = container.querySelector('fieldset');
      expect(fieldset).not.toBeDisabled();
    });

    it('should submit with FormData method POST', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} />);

      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;
      await user.type(barcodeInput, '5555555555555');

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      const submitCall = mockSubmit.mock.calls[0];
      expect(submitCall[1]).toHaveProperty('method', 'post');
    });
  });

  describe('Mock API Calls', () => {
    it('should format lookup request with correct field name', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} />);

      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;
      await user.type(barcodeInput, '1111111111111');

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      const formData = mockSubmit.mock.calls[0][0] as FormData;
      expect(formData.get('fieldName')).toBe('barcode');
    });

    it('should handle multiple lookup calls', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} />);

      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;

      // First lookup
      await user.type(barcodeInput, '1111111111111');
      let lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      expect(mockSubmit).toHaveBeenCalledTimes(1);

      // Clear and second lookup
      await user.clear(barcodeInput);
      await user.type(barcodeInput, '2222222222222');
      lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      expect(mockSubmit).toHaveBeenCalledTimes(2);
    });

    it('should pass correct identifierValue to submit', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} />);

      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;
      const testBarcode = '9999999999999';
      await user.type(barcodeInput, testBarcode);

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      const formData = mockSubmit.mock.calls[0][0] as FormData;
      expect(formData.get('identifierValue')).toBe(testBarcode);
    });

    it('should set intent to lookup for API call', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} />);

      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;
      await user.type(barcodeInput, '3333333333333');

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      const formData = mockSubmit.mock.calls[0][0] as FormData;
      expect(formData.get('intent')).toBe('lookup');
    });

    it('should use document.getElementById to retrieve barcode input', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} />);

      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;
      await user.type(barcodeInput, '4444444444444');

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      // Verify the mock was called with the correct barcode value
      const formData = mockSubmit.mock.calls[0][0] as FormData;
      expect(formData.get('identifierValue')).toBe('4444444444444');
    });
  });

  describe('Integration Tests', () => {
    it('should fill form with initial game data and allow modifications', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} game={mockGame} />);

      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      expect(titleInput).toHaveValue('The Witcher 3: Wild Hunt');

      await user.clear(titleInput);
      await user.type(titleInput, 'New Game Title');

      expect(titleInput.value).toBe('New Game Title');
    });

    it('should handle form with all fields populated', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} game={mockGame} />);

      // Verify all fields are populated
      expect(screen.getByLabelText('Title')).toHaveValue('The Witcher 3: Wild Hunt');
      expect(screen.getByLabelText('Release Date')).toHaveValue('2015-05-19');
      expect(screen.getByLabelText('Age Rating')).toHaveValue('M');
      expect(screen.getByLabelText('Barcode')).toHaveValue('9120000000001');
      expect(screen.getByLabelText('Description')).toHaveValue(
        'An open-world action RPG based on the novels by Andrzej Sapkowski.'
      );

      // Modify one field
      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      await user.clear(titleInput);
      await user.type(titleInput, 'Updated Game Title');

      expect(titleInput.value).toBe('Updated Game Title');

      // Verify other fields unchanged
      expect(screen.getByLabelText('Barcode')).toHaveValue('9120000000001');
      expect(screen.getByLabelText('Description')).toHaveValue(
        'An open-world action RPG based on the novels by Andrzej Sapkowski.'
      );
    });

    it('should trigger lookup and maintain form state', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} game={mockGame} />);

      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      const initialTitle = titleInput.value;

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      // Title should remain unchanged after lookup attempt
      expect(screen.getByLabelText('Title')).toHaveValue(initialTitle);
    });

    it('should handle rapid input changes across multiple fields', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} />);

      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      const ratingInput = screen.getByLabelText('Age Rating') as HTMLInputElement;
      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;

      await user.type(titleInput, 'Game X');
      await user.type(ratingInput, 'M');
      await user.type(barcodeInput, '1234567890');
      await user.type(titleInput, ' Edition');

      expect(titleInput.value).toBe('Game X Edition');
      expect(ratingInput.value).toBe('M');
      expect(barcodeInput.value).toBe('1234567890');
    });

    it('should support empty string values for optional fields', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} game={mockGame} />);

      const descriptionInput = screen.getByLabelText('Description') as HTMLTextAreaElement;
      await user.clear(descriptionInput);

      expect(descriptionInput.value).toBe('');
    });

    it('should maintain metadata selection through form changes', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} game={mockGame} />);

      // Verify initial metadata values
      expect(screen.getByTestId('developers-multiselect')).toHaveValue('cdprojekt');
      expect(screen.getByTestId('platform-singleselect')).toHaveValue('ps5');

      // Modify text field
      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      await user.clear(titleInput);
      await user.type(titleInput, 'Updated Title');

      // Verify metadata values unchanged
      expect(screen.getByTestId('developers-multiselect')).toHaveValue('cdprojekt');
      expect(screen.getByTestId('platform-singleselect')).toHaveValue('ps5');
    });
  });

  describe('Accessibility', () => {
    it('should have descriptive labels for all inputs', () => {
      render(<GameForm {...defaultProps} />);

      const labels = [
        'Title',
        'Platform',
        'Format',
        'Release Date',
        'Age Rating',
        'Developers',
        'Publishers',
        'Genres',
        'Barcode',
        'Description',
      ];

      labels.forEach((label) => {
        expect(screen.getByText(label)).toBeInTheDocument();
      });
    });

    it('should disable all inputs during submission', () => {
      const { container } = render(<GameForm {...defaultProps} isSubmitting={true} />);

      const fieldset = container.querySelector('fieldset');
      expect(fieldset).toBeDisabled();
    });

    it('should maintain accessibility during rapid interactions', async () => {
      const user = userEvent.setup();
      const { container } = render(<GameForm {...defaultProps} />);

      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      await user.type(titleInput, 'Accessible Game');

      const fieldset = container.querySelector('fieldset');
      expect(fieldset).not.toBeDisabled();
      expect(titleInput.value).toBe('Accessible Game');
    });
  });

  describe('Edge Cases', () => {
    it('should handle game object with only required fields', () => {
      const minimalGame: GameEntity = {
        type: Entity.Games,
        id: 'game-minimal',
        title: 'Minimal Game',
      };

      render(<GameForm {...defaultProps} game={minimalGame} />);

      expect(screen.getByLabelText('Title')).toHaveValue('Minimal Game');
      expect(screen.getByLabelText('Release Date')).toHaveValue('');
    });

    it('should handle undefined game parameter', () => {
      render(<GameForm {...defaultProps} game={undefined} />);

      expect(screen.getByLabelText('Title')).toHaveValue('');
      expect(screen.getByLabelText('Release Date')).toHaveValue('');
    });

    it('should handle very long strings in text fields', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} />);

      const longDescription = 'This is a very long description '.repeat(20);
      const descriptionInput = screen.getByLabelText('Description') as HTMLTextAreaElement;

      // Use paste() instead of type() for realistic user action with better performance
      await user.click(descriptionInput);
      await user.paste(longDescription);

      expect(descriptionInput.value).toBe(longDescription);
    });

    it('should handle special characters in all text fields', async () => {
      const user = userEvent.setup();
      render(<GameForm {...defaultProps} />);

      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      const specialChars = '!@#$%^&*()_+-=';

      await user.type(titleInput, specialChars);

      expect(titleInput.value).toBe(specialChars);
    });
  });
});
