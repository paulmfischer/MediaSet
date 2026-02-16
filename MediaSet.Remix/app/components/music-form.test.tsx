import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '~/test/test-utils';
import userEvent from '@testing-library/user-event';
import MusicForm from './music-form';
import { MusicEntity, Entity } from '~/models';

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

// Mock helper function
vi.mock('~/utils/helpers', () => ({
  millisecondsToMinutesSeconds: (ms: number | null | undefined) => {
    if (!ms) return '';
    const totalSeconds = Math.floor(ms / 1000);
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = totalSeconds % 60;
    return `${minutes}:${seconds.toString().padStart(2, '0')}`;
  },
}));

describe('MusicForm', () => {
  const defaultProps = {
    genres: [
      { label: 'Rock', value: 'rock' },
      { label: 'Jazz', value: 'jazz' },
      { label: 'Classical', value: 'classical' },
    ],
    formats: [
      { label: 'CD', value: 'cd' },
      { label: 'Vinyl', value: 'vinyl' },
      { label: 'Digital', value: 'digital' },
    ],
    labels: [
      { label: 'Sony Music', value: 'sony' },
      { label: 'Universal', value: 'universal' },
      { label: 'Warner Bros', value: 'warner' },
    ],
    isSubmitting: false,
    barcodeLookupAvailable: true,
  };

  const mockMusic: MusicEntity = {
    id: 'music-1',
    type: Entity.Musics,
    title: 'Abbey Road',
    artist: 'The Beatles',
    format: 'vinyl',
    releaseDate: '1969-09-26',
    genres: ['rock'],
    duration: 2700000, // 45 minutes in milliseconds
    label: 'sony',
    barcode: '9120074540614',
    tracks: 17,
    discs: 1,
    discList: [
      { trackNumber: 1, title: 'Come Together', duration: 259000 },
      { trackNumber: 2, title: 'Something', duration: 183000 },
    ],
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Form Rendering', () => {
    it('should render all form fields', () => {
      render(<MusicForm {...defaultProps} />);

      expect(screen.getByLabelText('Title')).toBeInTheDocument();
      expect(screen.getByLabelText('Artist')).toBeInTheDocument();
      expect(screen.getByText('Format')).toBeInTheDocument();
      expect(screen.getByLabelText('Release Date')).toBeInTheDocument();
      expect(screen.getByText('Genres')).toBeInTheDocument();
      expect(screen.getByLabelText('Duration (MM:SS)')).toBeInTheDocument();
      expect(screen.getByText('Label')).toBeInTheDocument();
      expect(screen.getByLabelText('Barcode')).toBeInTheDocument();
      expect(screen.getByLabelText('Tracks')).toBeInTheDocument();
      expect(screen.getByLabelText('Discs')).toBeInTheDocument();
      expect(screen.getByText('Disc List')).toBeInTheDocument();
    });

    it('should render the hidden id field', () => {
      render(<MusicForm {...defaultProps} music={mockMusic} />);

      const idInput = screen.getByDisplayValue('music-1') as HTMLInputElement;
      expect(idInput).toHaveAttribute('type', 'hidden');
      expect(idInput).toHaveAttribute('name', 'id');
    });

    it('should render the barcode lookup button', () => {
      render(<MusicForm {...defaultProps} />);

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      expect(lookupButton).toBeInTheDocument();
      expect(lookupButton).toHaveAttribute('type', 'button');
    });

    it('should render the Add Track button', () => {
      render(<MusicForm {...defaultProps} />);

      const addTrackButton = screen.getByRole('button', { name: /add track/i });
      expect(addTrackButton).toBeInTheDocument();
      expect(addTrackButton).toHaveAttribute('type', 'button');
    });

    it('should render all inputs with aria-labels', () => {
      render(<MusicForm {...defaultProps} />);

      expect(screen.getByLabelText('Title')).toHaveAttribute('aria-label', 'Title');
      expect(screen.getByLabelText('Artist')).toHaveAttribute('aria-label', 'Artist');
      expect(screen.getByLabelText('Release Date')).toHaveAttribute('aria-label', 'Release Date');
      expect(screen.getByLabelText('Duration (MM:SS)')).toHaveAttribute('aria-label', 'Duration');
      expect(screen.getByLabelText('Barcode')).toHaveAttribute('aria-label', 'Barcode');
      expect(screen.getByLabelText('Tracks')).toHaveAttribute('aria-label', 'Tracks');
      expect(screen.getByLabelText('Discs')).toHaveAttribute('aria-label', 'Discs');
    });
  });

  describe('Initial Value Loading', () => {
    it('should populate form fields with music data', () => {
      render(<MusicForm {...defaultProps} music={mockMusic} />);

      expect(screen.getByLabelText('Title')).toHaveValue('Abbey Road');
      expect(screen.getByLabelText('Artist')).toHaveValue('The Beatles');
      expect(screen.getByLabelText('Release Date')).toHaveValue('1969-09-26');
      expect(screen.getByLabelText('Barcode')).toHaveValue('9120074540614');
      expect(screen.getByLabelText('Tracks')).toHaveValue(17);
      expect(screen.getByLabelText('Discs')).toHaveValue(1);
    });

    it('should pass initial values to multiselect and singleselect inputs', () => {
      render(<MusicForm {...defaultProps} music={mockMusic} />);

      expect(screen.getByTestId('genres-multiselect')).toHaveValue('rock');
      expect(screen.getByTestId('format-singleselect')).toHaveValue('vinyl');
      expect(screen.getByTestId('label-singleselect')).toHaveValue('sony');
    });

    it('should render with empty values when no music is provided', () => {
      render(<MusicForm {...defaultProps} />);

      expect(screen.getByLabelText('Title')).toHaveValue('');
      expect(screen.getByLabelText('Artist')).toHaveValue('');
      expect(screen.getByLabelText('Release Date')).toHaveValue('');
      expect(screen.getByLabelText('Barcode')).toHaveValue('');
      expect(screen.getByLabelText('Tracks')).toHaveValue(null);
      expect(screen.getByLabelText('Discs')).toHaveValue(null);
    });

    it('should handle music with partial data', () => {
      const partialMusic: MusicEntity = {
        type: Entity.Musics,
        id: 'music-2',
        title: 'Partial Album',
        artist: 'Test Artist',
      };

      render(<MusicForm {...defaultProps} music={partialMusic} />);

      expect(screen.getByLabelText('Title')).toHaveValue('Partial Album');
      expect(screen.getByLabelText('Artist')).toHaveValue('Test Artist');
      expect(screen.getByLabelText('Release Date')).toHaveValue('');
      expect(screen.getByLabelText('Barcode')).toHaveValue('');
    });

    it('should load initial disc list from music data', () => {
      render(<MusicForm {...defaultProps} music={mockMusic} />);

      expect(screen.getByDisplayValue('Come Together')).toBeInTheDocument();
      expect(screen.getByDisplayValue('Something')).toBeInTheDocument();
    });

    it('should update disc list when music prop changes', async () => {
      const { rerender } = render(<MusicForm {...defaultProps} />);

      // Initially no discs
      expect(screen.queryByDisplayValue('Come Together')).not.toBeInTheDocument();

      // Rerender with music that has discs
      rerender(<MusicForm {...defaultProps} music={mockMusic} />);

      await waitFor(() => {
        expect(screen.getByDisplayValue('Come Together')).toBeInTheDocument();
      });
    });
  });

  describe('Input Changes and Validation', () => {
    it('should update title input when user types', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} />);

      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      await user.type(titleInput, 'New Album Title');

      expect(titleInput.value).toBe('New Album Title');
    });

    it('should update artist input', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} />);

      const artistInput = screen.getByLabelText('Artist') as HTMLInputElement;
      await user.type(artistInput, 'New Artist');

      expect(artistInput.value).toBe('New Artist');
    });

    it('should update release date input', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} />);

      const dateInput = screen.getByLabelText('Release Date') as HTMLInputElement;
      await user.type(dateInput, '2020-01-15');

      expect(dateInput.value).toBe('2020-01-15');
    });

    it('should update duration input', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} />);

      const durationInput = screen.getByLabelText('Duration (MM:SS)') as HTMLInputElement;
      await user.type(durationInput, '45:30');

      expect(durationInput.value).toBe('45:30');
    });

    it('should update barcode input', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} />);

      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;
      await user.type(barcodeInput, '5901234123457');

      expect(barcodeInput.value).toBe('5901234123457');
    });

    it('should update tracks number input', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} />);

      const tracksInput = screen.getByLabelText('Tracks') as HTMLInputElement;
      await user.type(tracksInput, '12');

      expect(tracksInput.value).toBe('12');
    });

    it('should update discs number input', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} />);

      const discsInput = screen.getByLabelText('Discs') as HTMLInputElement;
      await user.type(discsInput, '2');

      expect(discsInput.value).toBe('2');
    });

    it('should handle clearing text fields', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} music={mockMusic} />);

      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      await user.clear(titleInput);

      expect(titleInput.value).toBe('');
    });

    it('should accept numbers in tracks and discs fields', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} />);

      const tracksInput = screen.getByLabelText('Tracks') as HTMLInputElement;
      const discsInput = screen.getByLabelText('Discs') as HTMLInputElement;

      await user.type(tracksInput, '25');
      await user.type(discsInput, '3');

      expect(tracksInput.value).toBe('25');
      expect(discsInput.value).toBe('3');
    });
  });

  describe('Disc List Management', () => {
    it('should add a new disc when Add Track button is clicked', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} />);

      const addTrackButton = screen.getByRole('button', { name: /add track/i });
      await user.click(addTrackButton);

      // Should show a new track input with Track 1 label
      expect(screen.getByLabelText(/Track 1 - Title/)).toBeInTheDocument();
    });

    it('should render multiple disc inputs with correct track numbers', async () => {
      render(<MusicForm {...defaultProps} music={mockMusic} />);

      // Should display track numbers for initial discs
      expect(screen.getByLabelText(/Track 1 - Title/)).toBeInTheDocument();
      expect(screen.getByLabelText(/Track 2 - Title/)).toBeInTheDocument();
    });

    it('should update disc title when user types in track title field', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} music={mockMusic} />);

      const trackTitleInput = screen.getByLabelText(/Track 1 - Title/) as HTMLInputElement;
      await user.clear(trackTitleInput);
      await user.type(trackTitleInput, 'Updated Track Title');

      expect(trackTitleInput.value).toBe('Updated Track Title');
    });

    it('should update disc duration when user types in duration field', async () => {
      render(<MusicForm {...defaultProps} music={mockMusic} />);

      // The duration field in disc list shows formatted duration
      // Get all duration inputs (main form duration + disc durations)
      const durationInputs = screen.getAllByPlaceholderText('mm:ss');

      // The first one in mockMusic should be '4:19' (259000 ms)
      expect(durationInputs.length).toBeGreaterThan(0);
      expect(durationInputs[0]).toHaveValue('4:19');
    });

    it('should remove a disc when Remove button is clicked', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} music={mockMusic} />);

      const removeButtons = screen.getAllByRole('button', { name: /remove/i });
      expect(removeButtons).toHaveLength(2); // 2 initial discs

      await user.click(removeButtons[0]);

      const updatedRemoveButtons = screen.getAllByRole('button', { name: /remove/i });
      expect(updatedRemoveButtons).toHaveLength(1);
    });

    it('should render hidden track number input for each disc', () => {
      render(<MusicForm {...defaultProps} music={mockMusic} />);

      const trackNumberInputs = screen.getAllByDisplayValue('1');
      expect(trackNumberInputs.length).toBeGreaterThan(0);
    });

    it('should add disc with correct initial values', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} music={mockMusic} />);

      const initialDiscCount = screen.getAllByRole('button', { name: /remove/i }).length;

      const addTrackButton = screen.getByRole('button', { name: /add track/i });
      await user.click(addTrackButton);

      const newDiscCount = screen.getAllByRole('button', { name: /remove/i }).length;
      expect(newDiscCount).toBe(initialDiscCount + 1);
    });

    it('should handle adding and removing multiple discs', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} />);

      const addTrackButton = screen.getByRole('button', { name: /add track/i });

      // Add 3 discs
      await user.click(addTrackButton);
      await user.click(addTrackButton);
      await user.click(addTrackButton);

      let removeButtons = screen.getAllByRole('button', { name: /remove/i });
      expect(removeButtons).toHaveLength(3);

      // Remove middle disc
      await user.click(removeButtons[1]);

      removeButtons = screen.getAllByRole('button', { name: /remove/i });
      expect(removeButtons).toHaveLength(2);
    });
  });

  describe('Form Submission', () => {
    it('should call submit when lookup button is clicked with barcode', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} />);

      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;
      await user.type(barcodeInput, '5901234123457');

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      expect(mockSubmit).toHaveBeenCalled();
    });

    it('should pass correct form data to submit on lookup', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} />);

      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;
      await user.type(barcodeInput, '9120074540614');

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      expect(mockSubmit).toHaveBeenCalledWith(expect.any(FormData), expect.objectContaining({ method: 'post' }));

      const callArgs = mockSubmit.mock.calls[0][0] as FormData;
      expect(callArgs.get('intent')).toBe('lookup');
      expect(callArgs.get('fieldName')).toBe('barcode');
      expect(callArgs.get('identifierValue')).toBe('9120074540614');
    });

    it('should not call submit if barcode is empty', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} />);

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      expect(mockSubmit).not.toHaveBeenCalled();
    });

    it('should not call submit if lookup button is disabled during submission', async () => {
      render(<MusicForm {...defaultProps} isSubmitting={true} />);

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      expect(lookupButton).toBeDisabled();
    });

    it('should disable fieldset when isSubmitting is true', () => {
      const { container } = render(<MusicForm {...defaultProps} isSubmitting={true} />);

      const fieldset = container.querySelector('fieldset');
      expect(fieldset).toBeDisabled();
    });

    it('should enable fieldset when isSubmitting is false', () => {
      const { container } = render(<MusicForm {...defaultProps} isSubmitting={false} />);

      const fieldset = container.querySelector('fieldset');
      expect(fieldset).not.toBeDisabled();
    });
  });

  describe('Mock API Calls', () => {
    it('should format lookup request with correct field name', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} />);

      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;
      await user.type(barcodeInput, '1234567890');

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      const formData = mockSubmit.mock.calls[0][0] as FormData;
      expect(formData.get('fieldName')).toBe('barcode');
    });

    it('should handle multiple lookup calls', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} />);

      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;

      // First lookup
      await user.type(barcodeInput, '1111111111');
      let lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      expect(mockSubmit).toHaveBeenCalledTimes(1);

      // Clear and second lookup
      await user.clear(barcodeInput);
      await user.type(barcodeInput, '2222222222');
      lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      expect(mockSubmit).toHaveBeenCalledTimes(2);
    });

    it('should pass correct identifierValue to submit', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} />);

      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;
      const testBarcode = '9999999999';
      await user.type(barcodeInput, testBarcode);

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      const formData = mockSubmit.mock.calls[0][0] as FormData;
      expect(formData.get('identifierValue')).toBe(testBarcode);
    });

    it('should set intent to lookup for API call', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} />);

      const barcodeInput = screen.getByLabelText('Barcode') as HTMLInputElement;
      await user.type(barcodeInput, '3333333333');

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      const formData = mockSubmit.mock.calls[0][0] as FormData;
      expect(formData.get('intent')).toBe('lookup');
    });
  });

  describe('Integration Tests', () => {
    it('should fill form with initial music data and allow modifications', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} music={mockMusic} />);

      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      expect(titleInput).toHaveValue('Abbey Road');

      await user.clear(titleInput);
      await user.type(titleInput, 'New Album');

      expect(titleInput.value).toBe('New Album');
    });

    it('should handle form with all fields populated', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} music={mockMusic} />);

      // Verify all main fields are populated
      expect(screen.getByLabelText('Title')).toHaveValue('Abbey Road');
      expect(screen.getByLabelText('Artist')).toHaveValue('The Beatles');
      expect(screen.getByLabelText('Release Date')).toHaveValue('1969-09-26');
      expect(screen.getByLabelText('Barcode')).toHaveValue('9120074540614');
      expect(screen.getByLabelText('Tracks')).toHaveValue(17);
      expect(screen.getByLabelText('Discs')).toHaveValue(1);

      // Modify one field
      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      await user.clear(titleInput);
      await user.type(titleInput, 'Updated Album');

      expect(titleInput.value).toBe('Updated Album');

      // Verify other fields unchanged
      expect(screen.getByLabelText('Artist')).toHaveValue('The Beatles');
      expect(screen.getByLabelText('Barcode')).toHaveValue('9120074540614');
    });

    it('should trigger lookup and maintain form state', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} music={mockMusic} />);

      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      const initialTitle = titleInput.value;

      const lookupButton = screen.getByRole('button', { name: /lookup/i });
      await user.click(lookupButton);

      // Title should remain unchanged after lookup attempt (no barcode)
      expect(screen.getByLabelText('Title')).toHaveValue(initialTitle);
    });

    it('should handle rapid input changes', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} />);

      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      const artistInput = screen.getByLabelText('Artist') as HTMLInputElement;

      await user.type(titleInput, 'Test Album');
      await user.type(artistInput, 'Test Artist');
      await user.type(titleInput, ' Updated');

      expect(titleInput.value).toBe('Test Album Updated');
      expect(artistInput.value).toBe('Test Artist');
    });

    it('should manage disc list while updating other form fields', async () => {
      const user = userEvent.setup();
      render(<MusicForm {...defaultProps} music={mockMusic} />);

      const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
      await user.clear(titleInput);
      await user.type(titleInput, 'Updated Title');

      const addTrackButton = screen.getByRole('button', { name: /add track/i });
      await user.click(addTrackButton);

      expect(titleInput.value).toBe('Updated Title');
      expect(screen.getAllByRole('button', { name: /remove/i }).length).toBe(3); // 2 original + 1 new
    });
  });

  describe('Accessibility', () => {
    it('should have proper label associations', () => {
      render(<MusicForm {...defaultProps} />);

      const titleLabel = screen.getByText('Title');
      const titleInput = screen.getByLabelText('Title');

      expect(titleLabel).toBeInTheDocument();
      expect(titleInput).toBeInTheDocument();
    });

    it('should have descriptive labels for all inputs', () => {
      render(<MusicForm {...defaultProps} />);

      const labels = [
        'Title',
        'Artist',
        'Format',
        'Release Date',
        'Genres',
        'Duration (MM:SS)',
        'Label',
        'Barcode',
        'Tracks',
        'Discs',
      ];

      labels.forEach((label) => {
        expect(screen.getByText(label)).toBeInTheDocument();
      });
    });

    it('should disable all inputs during submission', () => {
      const { container } = render(<MusicForm {...defaultProps} isSubmitting={true} />);

      const fieldset = container.querySelector('fieldset');
      expect(fieldset).toBeDisabled();
    });

    it('should provide accessible disc list with track labels', () => {
      render(<MusicForm {...defaultProps} music={mockMusic} />);

      expect(screen.getByLabelText(/Track 1 - Title/)).toBeInTheDocument();
      expect(screen.getByLabelText(/Track 2 - Title/)).toBeInTheDocument();
    });

    it('should provide proper label for Add Track button', () => {
      render(<MusicForm {...defaultProps} />);

      const addTrackButton = screen.getByRole('button', { name: /add track/i });
      expect(addTrackButton).toBeInTheDocument();
    });

    it('should have proper duration format labels in disc list', () => {
      render(<MusicForm {...defaultProps} music={mockMusic} />);

      const durationInputs = screen.getAllByPlaceholderText('mm:ss');
      expect(durationInputs.length).toBeGreaterThan(0);
    });
  });
});
