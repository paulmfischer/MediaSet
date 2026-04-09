import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';
import { render, screen } from '~/test/test-utils';
import userEvent from '@testing-library/user-event';
import GameForm, { GameLookupSection } from './game-form';
import { GameEntity, Entity } from '~/models';

// Mock the custom input components
vi.mock('~/components/inputs/multiselect-input', () => ({
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

vi.mock('~/components/inputs/singleselect-input', () => ({
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

vi.mock('@remix-run/react', async () => {
  const actual = await vi.importActual('@remix-run/react');
  return {
    ...actual,
    Form: ({ children, ...props }: { children: React.ReactNode; [key: string]: unknown }) => (
      <form {...props}>{children}</form>
    ),
    useSubmit: () => vi.fn(),
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

      const titleLabel = screen.getAllByText('Title')[0];
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

  describe('Form State', () => {
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
  });

  describe('GameLookupSection', () => {
    it('should render title and barcode inputs', () => {
      render(<GameLookupSection defaultOpen />);

      expect(screen.getByLabelText('Title')).toBeInTheDocument();
      expect(screen.getByLabelText('Barcode')).toBeInTheDocument();
    });

    it('should render two Search buttons', () => {
      render(<GameLookupSection defaultOpen />);

      const searchButtons = screen.getAllByRole('button', { name: /search/i });
      expect(searchButtons).toHaveLength(2);
    });

    it('should have correct hidden inputs for title form', () => {
      const { container } = render(<GameLookupSection defaultOpen />);

      const forms = container.querySelectorAll('form');
      const titleForm = forms[0];
      const intentInput = titleForm.querySelector('input[name="intent"]') as HTMLInputElement;
      const fieldNameInput = titleForm.querySelector('input[name="fieldName"]') as HTMLInputElement;
      expect(intentInput.value).toBe('lookup');
      expect(fieldNameInput.value).toBe('title');
    });

    it('should have correct hidden inputs for barcode form', () => {
      const { container } = render(<GameLookupSection defaultOpen />);

      const forms = container.querySelectorAll('form');
      const barcodeForm = forms[1];
      const intentInput = barcodeForm.querySelector('input[name="intent"]') as HTMLInputElement;
      const fieldNameInput = barcodeForm.querySelector('input[name="fieldName"]') as HTMLInputElement;
      expect(intentInput.value).toBe('lookup');
      expect(fieldNameInput.value).toBe('barcode');
    });

    it('should disable inputs when isSubmitting is true', () => {
      render(<GameLookupSection defaultOpen isSubmitting={true} />);

      expect(screen.getByLabelText('Title')).toBeDisabled();
      expect(screen.getByLabelText('Barcode')).toBeDisabled();
      screen.getAllByRole('button', { name: /search/i }).forEach((btn) => {
        expect(btn).toBeDisabled();
      });
    });

    it('should enable inputs when isSubmitting is false', () => {
      render(<GameLookupSection defaultOpen isSubmitting={false} />);

      expect(screen.getByLabelText('Title')).not.toBeDisabled();
      expect(screen.getByLabelText('Barcode')).not.toBeDisabled();
    });

    it('should have identifierValue name on title and barcode inputs', () => {
      render(<GameLookupSection defaultOpen />);

      const inputs = screen.getAllByRole('textbox').filter((el) => (el as HTMLInputElement).name === 'identifierValue');
      expect(inputs.length).toBe(2);
    });

    it('should hide content when collapsed by default', () => {
      render(<GameLookupSection />);

      expect(screen.queryByLabelText('Title')).not.toBeInTheDocument();
      expect(screen.queryByLabelText('Barcode')).not.toBeInTheDocument();
    });

    it('should show content after clicking the toggle button', async () => {
      const user = userEvent.setup();
      render(<GameLookupSection />);

      await user.click(screen.getByRole('button', { name: /game lookup/i }));

      expect(screen.getByLabelText('Title')).toBeInTheDocument();
      expect(screen.getByLabelText('Barcode')).toBeInTheDocument();
    });

    it('should toggle closed when clicked while open', async () => {
      const user = userEvent.setup();
      render(<GameLookupSection defaultOpen />);

      expect(screen.getByLabelText('Title')).toBeInTheDocument();

      await user.click(screen.getAllByRole('button')[0]);

      expect(screen.queryByLabelText('Title')).not.toBeInTheDocument();
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
        expect(screen.getAllByText(label)[0]).toBeInTheDocument();
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
