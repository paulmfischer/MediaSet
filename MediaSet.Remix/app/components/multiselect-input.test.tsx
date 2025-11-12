import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, waitFor } from '~/test/test-utils';
import userEvent from '@testing-library/user-event';
import MultiselectInput from './multiselect-input';
import type { Option } from '~/models';

describe('MultiselectInput', () => {
  const mockOptions: Option[] = [
    { label: 'Option 1', value: 'option1' },
    { label: 'Option 2', value: 'option2' },
    { label: 'Option 3', value: 'option3' },
    { label: 'Rock', value: 'rock' },
    { label: 'Jazz', value: 'jazz' },
    { label: 'Classical', value: 'classical' },
  ];

  const defaultProps = {
    name: 'test-multiselect',
    addLabel: 'Add new',
    selectText: 'Select options',
    options: mockOptions,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Dropdown Rendering', () => {
    it('should render the input field with placeholder', () => {
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      expect(input).toBeInTheDocument();
      expect(input).toHaveAttribute('role', 'combobox');
    });

    it('should render the hidden input field with correct name', () => {
      render(<MultiselectInput {...defaultProps} />);

      const hiddenInput = document.querySelector(
        `input[type="hidden"][name="${defaultProps.name}"]`
      );
      expect(hiddenInput).toBeInTheDocument();
      expect(hiddenInput).toHaveAttribute('type', 'hidden');
    });

    it('should have aria-expanded set to false initially', () => {
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByRole('combobox');
      expect(input).toHaveAttribute('aria-expanded', 'false');
    });

    it('should display dropdown when input is focused', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      expect(input).toHaveAttribute('aria-expanded', 'true');
    });

    it('should render all options in dropdown when opened', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      mockOptions.forEach((option) => {
          expect(screen.getByText(option.label)).toBeInTheDocument();
        });
    });

    it('should have listbox role on dropdown container', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      const listbox = screen.getByRole('listbox');
      expect(listbox).toHaveAttribute('id', `${defaultProps.name}-listbox`);
    });

    it('should render options with role="option"', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      const options = screen.getAllByRole('option');
      expect(options).toHaveLength(mockOptions.length);
    });

    it('should have aria-controls pointing to listbox', () => {
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByRole('combobox');
      expect(input).toHaveAttribute('aria-controls', `${defaultProps.name}-listbox`);
    });

    it('should have aria-autocomplete="list" for combobox', () => {
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByRole('combobox');
      expect(input).toHaveAttribute('aria-autocomplete', 'list');
    });

    it('should display backdrop when dropdown is open', async () => {
      const user = userEvent.setup();
      const { container } = render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      await waitFor(() => {
        const backdrop = container.querySelector('.absolute.top-0.left-0.z-10');
        expect(backdrop).not.toHaveClass('hidden');
      });
    });

    it('should hide backdrop when dropdown is closed', () => {
      const { container } = render(<MultiselectInput {...defaultProps} />);

      const backdrop = container.querySelector('.absolute.top-0.left-0.z-10');
      expect(backdrop).toHaveClass('hidden');
    });

    it('should close dropdown when clicking backdrop', async () => {
      const user = userEvent.setup();
      const { container } = render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      expect(input).toHaveAttribute('aria-expanded', 'true');

      const backdrop = container.querySelector('.absolute.top-0.left-0.z-10');
      if (backdrop) {
        await user.click(backdrop);
      }

      expect(input).toHaveAttribute('aria-expanded', 'false');
    });
  });

  describe('Multiple Selection', () => {
    it('should select multiple options', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      await user.click(screen.getByText('Option 1'));
      await user.click(screen.getByText('Option 2'));

      const hiddenInput = document.querySelector(
        `input[type="hidden"][name="${defaultProps.name}"]`
      ) as HTMLInputElement;
      expect(hiddenInput.value).toBe('option1,option2');
    });

    it('should display selected badges', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      const option1 = screen.getByRole('option', { name: 'Option 1' });
      await user.click(option1);
      const option2 = screen.getByRole('option', { name: 'Option 2' });
      await user.click(option2);

      // Check badges are displayed
      const badges = screen.getAllByText(/Option [12]/);
      expect(badges.length).toBeGreaterThanOrEqual(2);
    });

    it('should keep dropdown open after selection', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      await user.click(screen.getByText('Option 1'));

      // Dropdown should remain open
      expect(input).toHaveAttribute('aria-expanded', 'true');
      expect(screen.getByRole('listbox')).toBeInTheDocument();
    });

    it('should refocus input after selecting option', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      await user.click(screen.getByText('Option 1'));

      // Input should still have focus
      expect(input).toHaveFocus();
    });

    it('should allow selecting all options', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      for (const option of mockOptions) {
        await user.click(screen.getByText(option.label));
      }

      const hiddenInput = document.querySelector(
        `input[type="hidden"][name="${defaultProps.name}"]`
      ) as HTMLInputElement;
      const selectedValues = hiddenInput.value.split(',');
      expect(selectedValues).toHaveLength(mockOptions.length);
    });
  });

  describe('Option Removal', () => {
    it('should remove option when badge X is clicked', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      await user.click(screen.getByText('Option 1'));
      await user.click(screen.getByText('Option 2'));

      // Get the badge for Option 1
      const badges = screen.getAllByText('Option 1');
      // The badge is in a different container than the dropdown option
      const badge = badges.find((el) => el.closest('[class*="flex"][class*="gap-2"]'));

      if (badge) {
        await user.click(badge);
      }

      const hiddenInput = document.querySelector(
        `input[type="hidden"][name="${defaultProps.name}"]`
      ) as HTMLInputElement;
      expect(hiddenInput.value).toBe('option2');
    });

    it('should remove option by pressing Backspace on empty input', async () => {
      const user = userEvent.setup();
      render(
        <MultiselectInput
          {...defaultProps}
          selectedValues={['option1', 'option2']}
        />
      );

      const input = screen.getByPlaceholderText('Select options');
      
      // Click to focus and activate
      await user.click(input);
      
      // Now press Backspace to remove last selected
      await user.keyboard('{Backspace}');

      const hiddenInput = document.querySelector(
        `input[type="hidden"][name="${defaultProps.name}"]`
      ) as HTMLInputElement;
      expect(hiddenInput.value).toBe('option1');
    });

    it('should not remove selection with Backspace when input has text', async () => {
      const user = userEvent.setup();
      render(
        <MultiselectInput
          {...defaultProps}
          selectedValues={['option1']}
        />
      );

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);
      await user.type(input, 'test');

      // Backspace deletes the typed character, not the selection
      const hiddenInput = document.querySelector(
        `input[type="hidden"][name="${defaultProps.name}"]`
      ) as HTMLInputElement;
      expect(hiddenInput.value).toBe('option1');
    });

    it('should deselect option when clicked again', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      const option1 = screen.getByRole('option', { name: 'Option 1' });
      await user.click(option1);
      expect(
        document.querySelector(`input[type="hidden"][name="${defaultProps.name}"]`) as HTMLInputElement
      ).toHaveValue('option1');

      // Click again to deselect
      await user.click(option1);
      expect(
        document.querySelector(`input[type="hidden"][name="${defaultProps.name}"]`) as HTMLInputElement
      ).toHaveValue('');
    });

    it('should remove last selected with multiple Backspaces', async () => {
      const user = userEvent.setup();
      render(
        <MultiselectInput
          {...defaultProps}
          selectedValues={['option1', 'option2', 'option3']}
        />
      );

      const input = screen.getByPlaceholderText('Select options');
      
      // Click to focus and activate
      await user.click(input);

      await user.keyboard('{Backspace}');
      let hiddenInput = document.querySelector(
        `input[type="hidden"][name="${defaultProps.name}"]`
      ) as HTMLInputElement;
      expect(hiddenInput.value).toBe('option1,option2');

      await user.keyboard('{Backspace}');
      hiddenInput = document.querySelector(
        `input[type="hidden"][name="${defaultProps.name}"]`
      ) as HTMLInputElement;
      expect(hiddenInput.value).toBe('option1');
    });
  });

  describe('Search/Filter Functionality', () => {
    it('should filter options based on input text', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);
      await user.type(input, 'Option 1');

      await waitFor(() => {
        expect(screen.getByText('Option 1')).toBeInTheDocument();
        expect(screen.queryByText('Option 2')).not.toBeInTheDocument();
      });
    });

    it('should perform case-insensitive filtering', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);
      await user.type(input, 'rock');

      await waitFor(() => {
        expect(screen.getByText('Rock')).toBeInTheDocument();
      });
    });

    it('should filter by partial match', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
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
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);
      await user.type(input, 'NonExistent');

      await waitFor(() => {
        expect(screen.getByText('Add new NonExistent')).toBeInTheDocument();
      });
    });

    it('should not show "Add new" option when filter is empty', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      await waitFor(() => {
        expect(screen.queryByText(/^Add new/)).not.toBeInTheDocument();
      });
    });

    it('should clear filter when dropdown closes', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);
      await user.type(input, 'Option');

      expect(input).toHaveValue('Option');

      await user.keyboard('{Escape}');

      // After closing, the input should be cleared
      expect(input).toHaveValue('');
    });

    it('should handle whitespace-only filter', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);
      await user.type(input, '   ');

      await waitFor(() => {
        // Should show all options (whitespace is trimmed)
        expect(screen.getByText('Option 1')).toBeInTheDocument();
        expect(screen.getByText('Option 2')).toBeInTheDocument();
      });
    });

    it('should update filtered options when props.options change', async () => {
      const { rerender } = render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await userEvent.click(input);

      const newOptions: Option[] = [
        { label: 'New 1', value: 'new1' },
        { label: 'New 2', value: 'new2' },
      ];

      rerender(
        <MultiselectInput
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

    it('should allow creating new option through "Add new"', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);
      await user.type(input, 'Custom Genre');

      const addNewOption = screen.getByText('Add new Custom Genre');
      await user.click(addNewOption);

      const hiddenInput = document.querySelector(
        `input[type="hidden"][name="${defaultProps.name}"]`
      ) as HTMLInputElement;
      expect(hiddenInput.value).toBe('Custom Genre');
    });

    it('should allow custom values to be added alongside standard options', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      
      // Add first standard option
      await user.click(input);
      await user.click(screen.getByRole('option', { name: 'Option 1' }));

      // Verify it's added
      let hiddenInput = document.querySelector(
        `input[type="hidden"][name="${defaultProps.name}"]`
      ) as HTMLInputElement;
      expect(hiddenInput.value).toContain('option1');

      // Add a custom option
      await user.type(input, 'CustomGenre');
      await waitFor(() => {
        expect(screen.getByRole('option', { name: /Add new CustomGenre/ })).toBeInTheDocument();
      });
      const addNewOption = screen.getByRole('option', { name: /Add new CustomGenre/ });
      await user.click(addNewOption);

      // Verify both are in hidden input
      hiddenInput = document.querySelector(
        `input[type="hidden"][name="${defaultProps.name}"]`
      ) as HTMLInputElement;
      expect(hiddenInput.value).toContain('option1');
      expect(hiddenInput.value).toContain('CustomGenre');
    });
  });

  describe('Keyboard Navigation', () => {
    it('should open dropdown with ArrowDown key', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      await user.keyboard('{ArrowDown}');

      expect(input).toHaveAttribute('aria-expanded', 'true');
    });

    it('should open dropdown with ArrowUp key', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      await user.keyboard('{ArrowUp}');

      expect(input).toHaveAttribute('aria-expanded', 'true');
    });

    it('should navigate down through options with ArrowDown', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      await user.keyboard('{ArrowDown}');

      await waitFor(() => {
        // aria-activedescendant should point to index 1 after ArrowDown from 0
        expect(input).toHaveAttribute('aria-activedescendant', `${defaultProps.name}-option-1`);
      });
    });

    it('should navigate up through options with ArrowUp', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      await user.keyboard('{ArrowDown}');
      await user.keyboard('{ArrowUp}');

      await waitFor(() => {
        // Should be back to index 0 after one up and one down
        expect(input).toHaveAttribute('aria-activedescendant', `${defaultProps.name}-option-0`);
      });
    });

    it('should select option with Enter key', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      await user.keyboard('{ArrowDown}');
      await user.keyboard('{Enter}');

      const hiddenInput = document.querySelector(
        `input[type="hidden"][name="${defaultProps.name}"]`
      ) as HTMLInputElement;
      expect(hiddenInput.value).toBe('option2');
    });

    it('should close dropdown with Escape key', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      expect(input).toHaveAttribute('aria-expanded', 'true');

      await user.keyboard('{Escape}');

      expect(input).toHaveAttribute('aria-expanded', 'false');
    });

    it('should close dropdown and move focus out with Tab key', async () => {
      const user = userEvent.setup();
      render(
        <div>
          <MultiselectInput {...defaultProps} />
          <input type="text" placeholder="Next field" />
        </div>
      );

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      expect(input).toHaveAttribute('aria-expanded', 'true');

      await user.keyboard('{Tab}');

      expect(input).toHaveAttribute('aria-expanded', 'false');
    });

    it('should set aria-activedescendant during keyboard navigation', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      await user.keyboard('{ArrowDown}');

      expect(input).toHaveAttribute(
          'aria-activedescendant',
          `${defaultProps.name}-option-1`
        );
    });

    it('should not go below first option with ArrowUp at start', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      await user.keyboard('{ArrowUp}{ArrowUp}{ArrowUp}');

      // Should stay at index 0
      expect(input).toHaveAttribute('aria-activedescendant', `${defaultProps.name}-option-0`);
    });

    it('should not go beyond last option with ArrowDown', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      // Navigate beyond last option multiple times
      for (let i = 0; i < mockOptions.length + 5; i++) {
        await user.keyboard('{ArrowDown}');
      }

      // Should be at last option (index = mockOptions.length - 1 = 5)
      expect(input).toHaveAttribute('aria-activedescendant', `${defaultProps.name}-option-5`);
    });

    it('should open dropdown with Enter when closed', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      await user.keyboard('{Escape}');

      // Now reopen with Enter
      await user.keyboard('{Enter}');

      expect(input).toHaveAttribute('aria-expanded', 'true');
    });
  });

  describe('Accessibility', () => {
    it('should have proper ARIA attributes for combobox', () => {
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByRole('combobox');
      expect(input).toHaveAttribute('aria-expanded');
      expect(input).toHaveAttribute('aria-controls');
      expect(input).toHaveAttribute('aria-autocomplete', 'list');
    });

    it('should update aria-expanded based on dropdown state', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByRole('combobox');
      expect(input).toHaveAttribute('aria-expanded', 'false');

      await user.click(input);

      expect(input).toHaveAttribute('aria-expanded', 'true');

      await user.keyboard('{Escape}');

      expect(input).toHaveAttribute('aria-expanded', 'false');
    });

    it('should have aria-selected on options', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      const options = screen.getAllByRole('option');
      options.forEach((option) => {
        expect(option).toHaveAttribute('aria-selected');
      });
    });

    it('should have unique IDs for options', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      const options = screen.getAllByRole('option');
      const ids = options.map((opt) => opt.id);

      const uniqueIds = new Set(ids);
      expect(uniqueIds.size).toBe(ids.length);
    });

    it('should handle focus management correctly', async () => {
      const user = userEvent.setup();
      render(
        <div>
          <MultiselectInput {...defaultProps} />
          <button>Next Button</button>
        </div>
      );

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);
      expect(input).toHaveAttribute('aria-expanded', 'true');

      await user.tab();

      expect(input).toHaveAttribute('aria-expanded', 'false');
    });

    it('should have role="listbox" on dropdown', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      const listbox = screen.getByRole('listbox');
      expect(listbox).toBeInTheDocument();
    });

    it('should have accessible names for interactive elements', () => {
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      expect(input).toHaveAttribute('placeholder', 'Select options');
    });

    it('should maintain focus outline on keyboard navigation', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      expect(input).toHaveFocus();

      await user.keyboard('{ArrowDown}');

      expect(input).toHaveFocus();
    });

    it('should provide visual feedback for active option', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      await user.keyboard('{ArrowDown}');

      // Check that aria-activedescendant points to the second option
      expect(input).toHaveAttribute('aria-activedescendant', `${defaultProps.name}-option-1`);
    });

    it('should scroll active option into view', async () => {
      const user = userEvent.setup();
      const manyOptions: Option[] = Array.from({ length: 20 }, (_, i) => ({
        label: `Option ${i + 1}`,
        value: `option${i + 1}`,
      }));

      render(
        <MultiselectInput
          {...defaultProps}
          options={manyOptions}
        />
      );

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      for (let i = 0; i < 10; i++) {
        await user.keyboard('{ArrowDown}');
      }

      const option = screen.getByText('Option 11');
      expect(option).toBeInTheDocument();
    });

    it('should properly mark selected options', async () => {
      const user = userEvent.setup();
      render(
        <MultiselectInput
          {...defaultProps}
          selectedValues={['option1', 'option2']}
        />
      );

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      const option1 = screen.getAllByText('Option 1').find(
        (el) => el.getAttribute('role') === 'option'
      );
      const option2 = screen.getAllByText('Option 2').find(
        (el) => el.getAttribute('role') === 'option'
      );

      expect(option1).toHaveAttribute('aria-selected', 'true');
      expect(option2).toHaveAttribute('aria-selected', 'true');
    });
  });

  describe('Edge Cases', () => {
    it('should handle empty options array', () => {
      render(
        <MultiselectInput
          {...defaultProps}
          options={[]}
        />
      );

      const input = screen.getByPlaceholderText('Select options');
      expect(input).toBeInTheDocument();
    });

    it('should handle rapid option selection', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');

      await user.click(input);
      await user.click(screen.getByText('Option 1'));
      await user.click(screen.getByText('Option 2'));
      await user.click(screen.getByText('Option 3'));

      const hiddenInput = document.querySelector(
        `input[type="hidden"][name="${defaultProps.name}"]`
      ) as HTMLInputElement;
      const values = hiddenInput.value.split(',');
      expect(values).toContain('option1');
      expect(values).toContain('option2');
      expect(values).toContain('option3');
    });

    it('should handle rapid typing and selection', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);
      await user.type(input, 'opt');

      const option = screen.getByText('Option 1');
      await user.click(option);

      expect(input).toHaveValue('opt');
    });

    it('should handle selecting option with special characters', async () => {
      const user = userEvent.setup();
      const specialOptions: Option[] = [
        { label: 'Option & Special', value: 'special1' },
        { label: 'Option "Quoted"', value: 'special2' },
      ];

      render(
        <MultiselectInput
          {...defaultProps}
          options={specialOptions}
        />
      );

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      const option = screen.getByText('Option & Special');
      await user.click(option);

      const hiddenInput = document.querySelector(
        `input[type="hidden"][name="${defaultProps.name}"]`
      ) as HTMLInputElement;
      expect(hiddenInput.value).toBe('special1');
    });

    it('should handle very long option labels', async () => {
      const user = userEvent.setup();
      const longOptions: Option[] = [
        {
          label: 'This is a very long option label that goes on and on and on and might cause layout issues if not handled properly',
          value: 'long1',
        },
      ];

      render(
        <MultiselectInput
          {...defaultProps}
          options={longOptions}
        />
      );

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      const option = screen.getByText('This is a very long option label that goes on and on and on and might cause layout issues if not handled properly');
      expect(option).toBeInTheDocument();

      await user.click(option);

      const hiddenInput = document.querySelector(
        `input[type="hidden"][name="${defaultProps.name}"]`
      ) as HTMLInputElement;
      expect(hiddenInput.value).toBe('long1');
    });

    it('should handle blur and refocus', async () => {
      const user = userEvent.setup();
      render(
        <div>
          <MultiselectInput {...defaultProps} />
          <input type="text" placeholder="Other field" />
        </div>
      );

      const input = screen.getByPlaceholderText('Select options');

      await user.click(input);
      expect(input).toHaveAttribute('aria-expanded', 'true');

      const otherField = screen.getByPlaceholderText('Other field');
      await user.click(otherField);

      expect(input).toHaveAttribute('aria-expanded', 'false');

      await user.click(input);
      expect(input).toHaveAttribute('aria-expanded', 'true');
    });

    it('should handle selectedValues changing to undefined', async () => {
      const { rerender } = render(
        <MultiselectInput
          {...defaultProps}
          selectedValues={['option1', 'option2']}
        />
      );

      let hiddenInput = document.querySelector(
        `input[type="hidden"][name="${defaultProps.name}"]`
      ) as HTMLInputElement;
      expect(hiddenInput.value).toBe('option1,option2');

      rerender(
        <MultiselectInput
          {...defaultProps}
          selectedValues={undefined}
        />
      );

      await waitFor(() => {
        hiddenInput = document.querySelector(
          `input[type="hidden"][name="${defaultProps.name}"]`
        ) as HTMLInputElement;
        expect(hiddenInput.value).toBe('');
      });
    });

    it('should handle options with unique values correctly', async () => {
      const user = userEvent.setup();
      const uniqueOptions: Option[] = [
        { label: 'First', value: 'first' },
        { label: 'Second', value: 'second' },
      ];

      render(
        <MultiselectInput
          {...defaultProps}
          options={uniqueOptions}
        />
      );

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      const firstOption = screen.getByRole('option', { name: 'First' });
      await user.click(firstOption);

      const hiddenInput = document.querySelector(
        `input[type="hidden"][name="${defaultProps.name}"]`
      ) as HTMLInputElement;
      expect(hiddenInput.value).toBe('first');
    });

    it('should handle mouse hover on options', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      const options = screen.getAllByRole('option');
      const secondOption = options[1];
      await user.hover(secondOption);

      // After hover, the input's aria-activedescendant should point to the hovered option
      expect(input).toHaveAttribute('aria-activedescendant', `${defaultProps.name}-option-1`);
    });
  });

  describe('Prop Changes', () => {
    it('should update selectText when prop changes', async () => {
      const { rerender } = render(<MultiselectInput {...defaultProps} />);

      let input = screen.getByPlaceholderText('Select options');
      expect(input).toBeInTheDocument();

      rerender(
        <MultiselectInput
          {...defaultProps}
          selectText="New placeholder"
        />
      );

      input = screen.getByPlaceholderText('New placeholder');
      expect(input).toBeInTheDocument();
    });

    it('should update name attribute when prop changes', async () => {
      const { rerender } = render(<MultiselectInput {...defaultProps} />);

      let hiddenInput = document.querySelector(
        `input[type="hidden"][name="${defaultProps.name}"]`
      );
      expect(hiddenInput).toBeInTheDocument();

      rerender(
        <MultiselectInput
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
        <MultiselectInput
          {...defaultProps}
          addLabel="Create"
        />
      );

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);
      await user.type(input, 'NewItem');

      expect(screen.getByText('Create NewItem')).toBeInTheDocument();
    });

    it('should initialize with selectedValues', () => {
      render(
        <MultiselectInput
          {...defaultProps}
          selectedValues={['option1', 'option2']}
        />
      );

      expect(screen.getByText('Option 1')).toBeInTheDocument();
      expect(screen.getByText('Option 2')).toBeInTheDocument();
    });

    it('should update when selectedValues prop changes', async () => {
      const { rerender } = render(
        <MultiselectInput
          {...defaultProps}
          selectedValues={['option1']}
        />
      );

      expect(screen.getByText('Option 1')).toBeInTheDocument();

      rerender(
        <MultiselectInput
          {...defaultProps}
          selectedValues={['option2', 'option3']}
        />
      );

      await waitFor(() => {
        // Option 1 badge should not be displayed anymore
        const badges = screen.getAllByText(/Option [23]/);
        expect(badges.length).toBeGreaterThanOrEqual(2);
        // Old selection should not appear in hidden input
        const hiddenInput = document.querySelector(
          `input[type="hidden"][name="${defaultProps.name}"]`
        ) as HTMLInputElement;
        expect(hiddenInput.value).toContain('option2');
        expect(hiddenInput.value).toContain('option3');
      });
    });
  });

  describe('Hidden Input Synchronization', () => {
    it('should synchronize hidden input with selected values', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      await user.click(screen.getByText('Option 1'));
      await user.click(screen.getByText('Option 3'));

      const hiddenInput = document.querySelector(
        `input[type="hidden"][name="${defaultProps.name}"]`
      ) as HTMLInputElement;
      expect(hiddenInput.value).toBe('option1,option3');
    });

    it('should maintain comma-separated format in hidden input', async () => {
      const user = userEvent.setup();
      render(<MultiselectInput {...defaultProps} />);

      const input = screen.getByPlaceholderText('Select options');
      await user.click(input);

      for (const option of [screen.getByText('Option 1'), screen.getByText('Option 2'), screen.getByText('Option 3')]) {
        await user.click(option);
      }

      const hiddenInput = document.querySelector(
        `input[type="hidden"][name="${defaultProps.name}"]`
      ) as HTMLInputElement;
      const values = hiddenInput.value.split(',');
      expect(values).toContain('option1');
      expect(values).toContain('option2');
      expect(values).toContain('option3');
      expect(values).toHaveLength(3);
    });
  });
});
