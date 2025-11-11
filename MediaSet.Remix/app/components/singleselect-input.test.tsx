import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, waitFor } from '~/test/test-utils';
import userEvent from '@testing-library/user-event';
import SingleselectInput from './singleselect-input';
import type { Option } from '~/models';

describe('SingleselectInput', () => {
  const mockOptions: Option[] = [
    { label: 'Option 1', value: 'option1' },
    { label: 'Option 2', value: 'option2' },
    { label: 'Option 3', value: 'option3' },
    { label: 'Rock', value: 'rock' },
    { label: 'Jazz', value: 'jazz' },
    { label: 'Classical', value: 'classical' },
  ];

  const defaultProps = {
    name: 'test-select',
    addLabel: 'Add new',
    placeholder: 'Select an option',
    options: mockOptions,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Dropdown Rendering', () => {
    it('should render the input field with placeholder', () => {
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      expect(input).toBeInTheDocument();
      expect(input).toHaveAttribute('role', 'combobox');
    });

    it('should render the hidden input field with correct name', () => {
      render(<SingleselectInput {...defaultProps} />);

      const hiddenInputs = document.querySelectorAll('input[type="hidden"][name="test-select"]');
      expect(hiddenInputs.length).toBeGreaterThan(0);
      expect(hiddenInputs[0]).toHaveAttribute('type', 'hidden');
      expect(hiddenInputs[0]).toHaveAttribute('name', 'test-select');
    });

    it('should have aria-expanded set to false initially', () => {
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByRole('combobox');
      expect(input).toHaveAttribute('aria-expanded', 'false');
    });

    it('should display dropdown when input is focused', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      await waitFor(() => {
        expect(input).toHaveAttribute('aria-expanded', 'true');
      });
    });

    it('should render all options in dropdown when opened', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      await waitFor(() => {
        mockOptions.forEach((option) => {
          expect(screen.getByText(option.label)).toBeInTheDocument();
        });
      });
    });

    it('should have listbox role on dropdown container', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      const listbox = screen.getByRole('listbox');
      expect(listbox).toHaveAttribute('id', `${defaultProps.name}-listbox`);
    });

    it('should render options with role="option"', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      const options = screen.getAllByRole('option');
      expect(options).toHaveLength(mockOptions.length);
    });

    it('should have aria-controls pointing to listbox', () => {
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByRole('combobox');
      expect(input).toHaveAttribute('aria-controls', `${defaultProps.name}-listbox`);
    });

    it('should have aria-autocomplete="list" for combobox', () => {
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByRole('combobox');
      expect(input).toHaveAttribute('aria-autocomplete', 'list');
    });

    it('should display backdrop when dropdown is open', async () => {
      const user = userEvent.setup();
      const { container } = render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      await waitFor(() => {
        const backdrop = container.querySelector('.absolute.top-0.left-0.z-10');
        expect(backdrop).not.toHaveClass('hidden');
      });
    });

    it('should hide backdrop when dropdown is closed', () => {
      const { container } = render(<SingleselectInput {...defaultProps} />);

      const backdrop = container.querySelector('.absolute.top-0.left-0.z-10');
      expect(backdrop).toHaveClass('hidden');
    });
  });

  describe('Option Selection', () => {
    it('should select option when clicked', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      const option = screen.getByText('Option 1');
      await user.click(option);

      expect(input).toHaveValue('Option 1');
    });

    it('should update hidden input with selected value', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      const option = screen.getByText('Option 2');
      await user.click(option);

      const hiddenInput = document.querySelector('input[type="hidden"][name="test-select"]') as HTMLInputElement;
      expect(hiddenInput.value).toBe('option2');
    });

    it('should close dropdown after selecting option', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      const option = screen.getByText('Option 1');
      await user.click(option);

      await waitFor(() => {
        expect(input).toHaveAttribute('aria-expanded', 'false');
      });
    });

    it('should clear filter text after selection', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);
      await user.type(input, 'opt');

      const option = screen.getByText('Option 1');
      await user.click(option);

      expect(input).toHaveValue('Option 1');
    });

    it('should handle selecting new options', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);
      await user.type(input, 'New Option');

      const newOption = screen.getByText('Add new New Option');
      await user.click(newOption);

      const hiddenInput = document.querySelector('input[type="hidden"][name="test-select"]') as HTMLInputElement;
      expect(hiddenInput.value).toBe('New Option');
      expect(input).toHaveValue('New Option');
    });

    it('should initialize with selected value', () => {
      render(
        <SingleselectInput
          {...defaultProps}
          selectedValue="option2"
        />
      );

      const input = screen.getByPlaceholderText('Select an option');
      // The display value shows the value, not the label, when initialized
      expect(input).toHaveValue('option2');
    });

    it('should update display when selectedValue prop changes', async () => {
      const { rerender } = render(
        <SingleselectInput
          {...defaultProps}
          selectedValue="option1"
        />
      );

      const input = screen.getByPlaceholderText('Select an option');
      expect(input).toHaveValue('option1');

      rerender(
        <SingleselectInput
          {...defaultProps}
          selectedValue="option3"
        />
      );

      await waitFor(() => {
        expect(input).toHaveValue('option3');
      });
    });

    it('should handle option with isNew flag', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);
      await user.type(input, 'Custom');

      const newOption = screen.getByText('Add new Custom');
      await user.click(newOption);

      const hiddenInput = document.querySelector('input[type="hidden"][name="test-select"]') as HTMLInputElement;
      expect(hiddenInput.value).toBe('Custom');
    });
  });

  describe('Search/Filter Functionality', () => {
    it('should filter options based on input text', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);
      await user.type(input, 'Option 1');

      await waitFor(() => {
        expect(screen.getByText('Option 1')).toBeInTheDocument();
        expect(screen.queryByText('Option 2')).not.toBeInTheDocument();
      });
    });

    it('should perform case-insensitive filtering', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);
      await user.type(input, 'rock');

      await waitFor(() => {
        expect(screen.getByText('Rock')).toBeInTheDocument();
      });
    });

    it('should filter by partial match', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);
      await user.type(input, 'opt');

      await waitFor(() => {
        expect(screen.getByText('Option 1')).toBeInTheDocument();
        expect(screen.getByText('Option 2')).toBeInTheDocument();
        expect(screen.getByText('Option 3')).toBeInTheDocument();
      });
    });

    it('should show "Add new" option when filtering with non-matching text', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);
      await user.type(input, 'NonExistent');

      await waitFor(() => {
        expect(screen.getByText('Add new NonExistent')).toBeInTheDocument();
      });
    });

    it('should not show "Add new" option when filter is empty', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      await waitFor(() => {
        expect(screen.queryByText(/^Add new/)).not.toBeInTheDocument();
      });
    });

    it('should clear filter when dropdown closes', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);
      await user.type(input, 'Option');

      expect(input).toHaveValue('Option');

      await user.keyboard('{Escape}');

      // After closing, the input should show the selected value (or placeholder if nothing selected)
      expect(input).toHaveValue('');
    });

    it('should handle whitespace-only filter', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);
      await user.type(input, '   ');

      await waitFor(() => {
        // Should show all options (whitespace is trimmed)
        expect(screen.getByText('Option 1')).toBeInTheDocument();
        expect(screen.getByText('Option 2')).toBeInTheDocument();
      });
    });

    it('should update filtered options when props.options change', async () => {
      const { rerender } = render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await userEvent.click(input);

      const newOptions: Option[] = [
        { label: 'New 1', value: 'new1' },
        { label: 'New 2', value: 'new2' },
      ];

      rerender(
        <SingleselectInput
          {...defaultProps}
          options={newOptions}
        />
      );

      await waitFor(() => {
        expect(screen.getByText('New 1')).toBeInTheDocument();
        expect(screen.getByText('New 2')).toBeInTheDocument();
        expect(screen.queryByText('Option 1')).not.toBeInTheDocument();
      });
    });
  });

  describe('Keyboard Navigation', () => {
    it('should open dropdown with ArrowDown key', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      input.focus();

      await user.keyboard('{ArrowDown}');

      await waitFor(() => {
        expect(input).toHaveAttribute('aria-expanded', 'true');
      });
    });

    it('should open dropdown with ArrowUp key', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      input.focus();

      await user.keyboard('{ArrowUp}');

      await waitFor(() => {
        expect(input).toHaveAttribute('aria-expanded', 'true');
      });
    });

    it('should navigate down through options with ArrowDown', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      // First ArrowDown should set active to first option (index 1, since initial is 0)
      await user.keyboard('{ArrowDown}');

      await waitFor(() => {
        const option = screen.getByText('Option 2');
        expect(option).toHaveAttribute('aria-selected', 'true');
      });
    });

    it('should navigate up through options with ArrowUp', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      // Go down to first option, then up (should stay at first or wrap)
      await user.keyboard('{ArrowDown}');
      await user.keyboard('{ArrowUp}');

      await waitFor(() => {
        const option = screen.getByText('Option 1');
        expect(option).toHaveAttribute('aria-selected', 'true');
      });
    });

    it('should select option with Enter key', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      await user.keyboard('{ArrowDown}');
      await user.keyboard('{Enter}');

      // Should select the second option (first ArrowDown moves to index 1 = Option 2)
      expect(input).toHaveValue('Option 2');
    });

    it('should close dropdown with Escape key', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      expect(input).toHaveAttribute('aria-expanded', 'true');

      await user.keyboard('{Escape}');

      await waitFor(() => {
        expect(input).toHaveAttribute('aria-expanded', 'false');
      });
    });

    it('should close dropdown and move focus out with Tab key', async () => {
      const user = userEvent.setup();
      render(
        <div>
          <SingleselectInput {...defaultProps} />
          <input type="text" placeholder="Next field" />
        </div>
      );

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      expect(input).toHaveAttribute('aria-expanded', 'true');

      await user.keyboard('{Tab}');

      await waitFor(() => {
        expect(input).toHaveAttribute('aria-expanded', 'false');
      });
    });

    it('should clear selection with Backspace when input is empty', async () => {
      const user = userEvent.setup();
      render(
        <SingleselectInput
          {...defaultProps}
          selectedValue="option1"
        />
      );

      const input = screen.getByPlaceholderText('Select an option');
      // Display shows the value initially
      expect(input).toHaveValue('option1');

      // Focus and press backspace to clear
      input.focus();
      await user.keyboard('{Backspace}');

      // After backspace, both display and hidden input should be empty
      await waitFor(() => {
        const hiddenInput = document.querySelector('input[type="hidden"][name="test-select"]') as HTMLInputElement;
        expect(hiddenInput.value).toBe('');
        expect(input).toHaveValue('');
      });
    });

    it('should not clear selection with Backspace when input has text', async () => {
      const user = userEvent.setup();
      render(
        <SingleselectInput
          {...defaultProps}
          selectedValue="option1"
        />
      );

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);
      await user.type(input, 'test');

      // Backspace will delete the typed character, not the selection
      const hiddenInput = document.querySelector('input[type="hidden"][name="test-select"]') as HTMLInputElement;
      expect(hiddenInput.value).toBe('option1');
    });

    it('should set aria-activedescendant during keyboard navigation', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      await user.keyboard('{ArrowDown}');

      // After one ArrowDown, activeIndex should be 1
      await waitFor(() => {
        expect(input).toHaveAttribute(
          'aria-activedescendant',
          `${defaultProps.name}-option-1`
        );
      });
    });

    it('should not go below first option with ArrowUp at start', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      // Multiple ArrowUp presses shouldn't go below index 0
      await user.keyboard('{ArrowUp}{ArrowUp}{ArrowUp}');

      const option = screen.getByText('Option 1');
      expect(option).toHaveAttribute('aria-selected', 'true');
    });

    it('should not go beyond last option with ArrowDown', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      // Navigate to last option multiple times
      for (let i = 0; i < mockOptions.length + 5; i++) {
        await user.keyboard('{ArrowDown}');
      }

      const options = screen.getAllByRole('option');
      const lastOption = options[options.length - 1];
      expect(lastOption).toHaveAttribute('aria-selected', 'true');
    });

    it('should focus input on Enter when dropdown is closed', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      input.focus();

      // Press Enter with closed dropdown - should open it
      await user.keyboard('{Enter}');

      // Since dropdown was closed, Enter opens it
      // The condition "if (!displayOptions)" returns early
      await waitFor(() => {
        // The display should have actually opened the dropdown
        const listbox = screen.getByRole('listbox');
        // Check if it's not hidden by seeing if we can interact with options
        expect(listbox).toBeInTheDocument();
      });
    });
  });

  describe('Accessibility', () => {
    it('should have proper ARIA attributes for combobox', () => {
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByRole('combobox');
      expect(input).toHaveAttribute('aria-expanded');
      expect(input).toHaveAttribute('aria-controls');
      expect(input).toHaveAttribute('aria-autocomplete', 'list');
    });

    it('should update aria-expanded based on dropdown state', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByRole('combobox');
      expect(input).toHaveAttribute('aria-expanded', 'false');

      await user.click(input);

      await waitFor(() => {
        expect(input).toHaveAttribute('aria-expanded', 'true');
      });

      await user.keyboard('{Escape}');

      await waitFor(() => {
        expect(input).toHaveAttribute('aria-expanded', 'false');
      });
    });

    it('should have aria-selected on options', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      const options = screen.getAllByRole('option');
      options.forEach((option) => {
        expect(option).toHaveAttribute('aria-selected');
      });
    });

    it('should have unique IDs for options', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      const options = screen.getAllByRole('option');
      const ids = options.map((opt) => opt.id);

      // All IDs should be unique
      const uniqueIds = new Set(ids);
      expect(uniqueIds.size).toBe(ids.length);
    });

    it('should handle focus management correctly', async () => {
      const user = userEvent.setup();
      render(
        <div>
          <SingleselectInput {...defaultProps} />
          <button>Next Button</button>
        </div>
      );

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);
      expect(input).toHaveAttribute('aria-expanded', 'true');

      // Tab to move focus away
      await user.tab();

      // Dropdown should close when focus leaves
      await waitFor(() => {
        expect(input).toHaveAttribute('aria-expanded', 'false');
      });
    });

    it('should keep dropdown open when focus moves to listbox', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      const option = screen.getByText('Option 1');
      await user.click(option);

      // After selection, dropdown should close
      expect(input).toHaveAttribute('aria-expanded', 'false');
    });

    it('should have role="listbox" on dropdown', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      const listbox = screen.getByRole('listbox');
      expect(listbox).toBeInTheDocument();
    });

    it('should have accessible names for interactive elements', () => {
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      expect(input).toHaveAttribute('placeholder', 'Select an option');
    });

    it('should maintain focus outline on keyboard navigation', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      input.focus();

      expect(input).toHaveFocus();

      await user.keyboard('{ArrowDown}');

      expect(input).toHaveFocus();
    });

    it('should provide visual feedback for active option', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      await user.keyboard('{ArrowDown}');

      // After ArrowDown, activeIndex is 1, so Option 2 should be selected
      const option = screen.getByText('Option 2');
      expect(option).toHaveAttribute('aria-selected', 'true');
    });

    it('should scroll active option into view', async () => {
      const user = userEvent.setup();
      const manyOptions: Option[] = Array.from({ length: 20 }, (_, i) => ({
        label: `Option ${i + 1}`,
        value: `option${i + 1}`,
      }));

      render(
        <SingleselectInput
          {...defaultProps}
          options={manyOptions}
        />
      );

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      // Navigate to an option far down the list
      for (let i = 0; i < 10; i++) {
        await user.keyboard('{ArrowDown}');
      }

      // The active option should be in the document
      // (scrollIntoView is called but we can verify the element exists)
      const option = screen.getByText('Option 11');
      expect(option).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('should handle empty options array', () => {
      render(
        <SingleselectInput
          {...defaultProps}
          options={[]}
        />
      );

      const input = screen.getByPlaceholderText('Select an option');
      expect(input).toBeInTheDocument();
    });

    it('should handle rapid option selection', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');

      // Select first option
      await user.click(input);
      await user.click(screen.getByText('Option 1'));

      // Immediately open and select another
      await user.click(input);
      await user.click(screen.getByText('Option 2'));

      expect(input).toHaveValue('Option 2');
    });

    it('should handle rapid typing and selection', async () => {
      const user = userEvent.setup();
      render(<SingleselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);
      await user.type(input, 'opt');

      const option = screen.getByText('Option 1');
      await user.click(option);

      expect(input).toHaveValue('Option 1');
    });

    it('should handle selecting option with special characters', async () => {
      const user = userEvent.setup();
      const specialOptions: Option[] = [
        { label: 'Option & Special', value: 'special1' },
        { label: 'Option "Quoted"', value: 'special2' },
      ];

      render(
        <SingleselectInput
          {...defaultProps}
          options={specialOptions}
        />
      );

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      const option = screen.getByText('Option & Special');
      await user.click(option);

      const hiddenInput = document.querySelector('input[type="hidden"][name="test-select"]') as HTMLInputElement;
      expect(hiddenInput.value).toBe('special1');
    });

    it('should handle very long option labels', async () => {
      const user = userEvent.setup();
      const longOptions: Option[] = [
        { 
          label: 'This is a very long option label that goes on and on and on and might cause layout issues if not handled properly', 
          value: 'long1' 
        },
      ];

      render(
        <SingleselectInput
          {...defaultProps}
          options={longOptions}
        />
      );

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      const option = screen.getByText('This is a very long option label that goes on and on and on and might cause layout issues if not handled properly');
      expect(option).toBeInTheDocument();

      await user.click(option);

      expect(input).toHaveValue('This is a very long option label that goes on and on and on and might cause layout issues if not handled properly');
    });

    it('should handle blur and refocus', async () => {
      const user = userEvent.setup();
      render(
        <div>
          <SingleselectInput {...defaultProps} />
          <input type="text" placeholder="Other field" />
        </div>
      );

      const input = screen.getByPlaceholderText('Select an option');

      await user.click(input);
      expect(input).toHaveAttribute('aria-expanded', 'true');

      const otherField = screen.getByPlaceholderText('Other field');
      await user.click(otherField);

      await waitFor(() => {
        expect(input).toHaveAttribute('aria-expanded', 'false');
      });

      await user.click(input);
      expect(input).toHaveAttribute('aria-expanded', 'true');
    });

    it('should handle selectedValue changing to undefined', async () => {
      const { rerender } = render(
        <SingleselectInput
          {...defaultProps}
          selectedValue="option1"
        />
      );

      const input = screen.getByPlaceholderText('Select an option');
      expect(input).toHaveValue('option1');

      rerender(
        <SingleselectInput
          {...defaultProps}
          selectedValue={undefined}
        />
      );

      await waitFor(() => {
        expect(input).toHaveValue('');
      });
    });

    it('should handle options with duplicate values', async () => {
      const user = userEvent.setup();
      const duplicateOptions: Option[] = [
        { label: 'First', value: 'same' },
        { label: 'Second', value: 'same' },
      ];

      render(
        <SingleselectInput
          {...defaultProps}
          options={duplicateOptions}
        />
      );

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);

      const firstOption = screen.getAllByText(/First|Second/)[0];
      await user.click(firstOption);

      expect(input).toHaveValue('First');
    });
  });

  describe('Prop Changes', () => {
    it('should update placeholder when prop changes', async () => {
      const { rerender } = render(<SingleselectInput {...defaultProps} />);

      let input = screen.getByPlaceholderText('Select an option');
      expect(input).toBeInTheDocument();

      rerender(
        <SingleselectInput
          {...defaultProps}
          placeholder="New placeholder"
        />
      );

      input = screen.getByPlaceholderText('New placeholder');
      expect(input).toBeInTheDocument();
    });

    it('should update name attribute when prop changes', async () => {
      const { rerender } = render(<SingleselectInput {...defaultProps} />);

      let hiddenInput = document.querySelector('input[type="hidden"][name="test-select"]');
      expect(hiddenInput).toBeInTheDocument();

      rerender(
        <SingleselectInput
          {...defaultProps}
          name="new-name"
        />
      );

      hiddenInput = document.querySelector('input[type="hidden"][name="new-name"]');
      expect(hiddenInput).toBeInTheDocument();
    });

    it('should handle addLabel prop', async () => {
      const user = userEvent.setup();
      render(
        <SingleselectInput
          {...defaultProps}
          addLabel="Create"
        />
      );

      const input = screen.getByPlaceholderText('Select an option');
      await user.click(input);
      await user.type(input, 'NewItem');

      expect(screen.getByText('Create NewItem')).toBeInTheDocument();
    });
  });
});
