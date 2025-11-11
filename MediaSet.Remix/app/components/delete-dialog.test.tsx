import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { render, screen, waitFor } from '~/test/test-utils';
import userEvent from '@testing-library/user-event';
import DeleteDialog from './delete-dialog';

// Mock the Remix Form component to avoid router context requirement
vi.mock('@remix-run/react', async () => {
  const actual = await vi.importActual('@remix-run/react');
  return {
    ...actual,
    Form: ({ action, method, children, ...props }: any) => (
      <form action={action} method={method} {...props}>
        {children}
      </form>
    ),
  };
});

describe('DeleteDialog', () => {
  const defaultProps = {
    isOpen: true,
    onClose: vi.fn(),
    entityTitle: 'Test Entity',
    deleteAction: '/api/delete',
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  describe('Dialog Visibility', () => {
    it('should render dialog element', () => {
      const { container } = render(<DeleteDialog {...defaultProps} />);
      
      const dialog = container.querySelector('dialog');
      expect(dialog).toBeInTheDocument();
    });

    it('should show modal when isOpen is true', async () => {
      const { container } = render(<DeleteDialog {...defaultProps} isOpen={true} />);
      
      await waitFor(() => {
        const dialog = container.querySelector('dialog');
        expect(dialog).toHaveAttribute('open');
      });
    });

    it('should not show modal when isOpen is false', async () => {
      const { container } = render(<DeleteDialog {...defaultProps} isOpen={false} />);
      
      // Dialog should exist but not be open
      const dialog = container.querySelector('dialog');
      expect(dialog).toBeInTheDocument();
      expect(dialog).not.toHaveAttribute('open');
    });

    it('should toggle modal visibility when isOpen changes', async () => {
      const { container, rerender } = render(<DeleteDialog {...defaultProps} isOpen={true} />);
      
      await waitFor(() => {
        const dialog = container.querySelector('dialog');
        expect(dialog).toHaveAttribute('open');
      });

      rerender(<DeleteDialog {...defaultProps} isOpen={false} />);

      await waitFor(() => {
        const dialog = container.querySelector('dialog');
        expect(dialog).not.toHaveAttribute('open');
      });
    });
  });

  describe('Dialog Header', () => {
    it('should display "Confirm Delete" title', () => {
      render(<DeleteDialog {...defaultProps} />);
      
      expect(screen.getByText('Confirm Delete')).toBeInTheDocument();
    });

    it('should have a close button in header', () => {
      render(<DeleteDialog {...defaultProps} />);
      
      const closeButton = screen.getByLabelText('Close dialog');
      expect(closeButton).toBeInTheDocument();
    });

    it('should display border below header', () => {
      const { container } = render(<DeleteDialog {...defaultProps} />);
      
      const headerDiv = container.querySelector('.border-b');
      expect(headerDiv).toBeInTheDocument();
    });
  });

  describe('Dialog Content', () => {
    it('should display confirmation message', () => {
      render(<DeleteDialog {...defaultProps} entityTitle="My Item" />);
      
      expect(screen.getByText(/Are you sure you want to delete/)).toBeInTheDocument();
    });

    it('should display entity title in bold when provided', () => {
      render(<DeleteDialog {...defaultProps} entityTitle="My Item" />);
      
      const bold = screen.getByText('My Item');
      expect(bold.tagName).toBe('STRONG');
    });

    it('should display generic message when entityTitle is not provided', () => {
      render(<DeleteDialog {...defaultProps} entityTitle={undefined} />);
      
      expect(screen.getByText(/this item/)).toBeInTheDocument();
    });

    it('should display generic message when entityTitle is empty', () => {
      render(<DeleteDialog {...defaultProps} entityTitle="" />);
      
      expect(screen.getByText(/this item/)).toBeInTheDocument();
    });

    it('should display warning message', () => {
      render(<DeleteDialog {...defaultProps} />);
      
      expect(screen.getByText('This action cannot be undone.')).toBeInTheDocument();
    });
  });

  describe('Dialog Actions', () => {
    it('should have Cancel button', () => {
      render(<DeleteDialog {...defaultProps} />);
      
      const cancelButtons = screen.getAllByText('Cancel');
      expect(cancelButtons.length).toBeGreaterThan(0);
    });

    it('should have Delete button', () => {
      render(<DeleteDialog {...defaultProps} />);
      
      expect(screen.getByText('Delete')).toBeInTheDocument();
    });

    it('should position buttons to the right', () => {
      const { container } = render(<DeleteDialog {...defaultProps} />);
      
      const buttonContainer = container.querySelector('.justify-end');
      expect(buttonContainer).toBeInTheDocument();
    });
  });

  describe('Cancel Action', () => {
    it('should call onClose when cancel button is clicked', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      render(<DeleteDialog {...defaultProps} onClose={onClose} />);
      
      const cancelButtons = screen.getAllByText('Cancel');
      const cancelButton = cancelButtons[0];
      await user.click(cancelButton);
      
      expect(onClose).toHaveBeenCalledTimes(1);
    });

    it('should call onClose when X button is clicked', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      render(<DeleteDialog {...defaultProps} onClose={onClose} />);
      
      const closeButton = screen.getByLabelText('Close dialog');
      await user.click(closeButton);
      
      expect(onClose).toHaveBeenCalledTimes(1);
    });

    it('should call onClose only once per click', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      render(<DeleteDialog {...defaultProps} onClose={onClose} />);
      
      const cancelButtons = screen.getAllByText('Cancel');
      const cancelButton = cancelButtons[0];
      await user.click(cancelButton);
      
      expect(onClose).toHaveBeenCalledOnce();
    });
  });

  describe('Backdrop Click Handling', () => {
    it('should close dialog when clicking the dialog directly', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      const { container } = render(<DeleteDialog {...defaultProps} onClose={onClose} />);
      
      // Note: In a real browser, clicking on the ::backdrop would trigger a close event
      // In tests with jsdom, we need to test the click handler behavior
      const dialog = container.querySelector('dialog') as HTMLDialogElement;
      
      // Create a mouse event simulating a click outside the dialog content
      const mockEvent = new MouseEvent('click', {
        bubbles: true,
        cancelable: true,
        clientX: 0,
        clientY: 0,
      });
      
      // Mock getBoundingClientRect to simulate clicking outside
      vi.spyOn(dialog, 'getBoundingClientRect').mockReturnValue({
        top: 100,
        left: 100,
        right: 300,
        bottom: 300,
        width: 200,
        height: 200,
        x: 100,
        y: 100,
        toJSON: () => ({}),
      });
      
      dialog.dispatchEvent(mockEvent);
      
      expect(onClose).toHaveBeenCalled();
    });

    it('should not close dialog when clicking inside dialog content', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      render(<DeleteDialog {...defaultProps} onClose={onClose} />);
      
      const message = screen.getByText(/Are you sure you want to delete/);
      await user.click(message);
      
      // onClose should not be called since we clicked inside the dialog
      expect(onClose).not.toHaveBeenCalled();
    });

    it('should properly detect clicks within dialog bounds', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      render(<DeleteDialog {...defaultProps} onClose={onClose} />);
      
      const deleteButton = screen.getByText('Delete');
      await user.click(deleteButton);
      
      expect(onClose).not.toHaveBeenCalled();
    });
  });

  describe('Delete Form', () => {
    it('should have form with correct action', () => {
      const { container } = render(<DeleteDialog {...defaultProps} deleteAction="/api/delete-item" />);
      
      const form = container.querySelector('form');
      expect(form).toHaveAttribute('action', '/api/delete-item');
    });

    it('should have form with POST method', () => {
      const { container } = render(<DeleteDialog {...defaultProps} />);
      
      const form = container.querySelector('form');
      expect(form).toHaveAttribute('method', 'post');
    });

    it('should have delete button inside form', () => {
      const { container } = render(<DeleteDialog {...defaultProps} />);
      
      const form = container.querySelector('form');
      const deleteButton = form?.querySelector('button[type="submit"]');
      expect(deleteButton).toHaveTextContent('Delete');
    });

    it('should have delete button that can be clicked', async () => {
      const user = userEvent.setup();
      render(<DeleteDialog {...defaultProps} />);
      
      const deleteButton = screen.getByRole('button', { name: 'Delete' });
      expect(deleteButton).toHaveAttribute('type', 'submit');
      
      await user.click(deleteButton);
      expect(deleteButton).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have proper heading hierarchy', () => {
      render(<DeleteDialog {...defaultProps} />);
      
      const title = screen.getByText('Confirm Delete');
      expect(title.tagName).toBe('H2');
    });

    it('should have accessible close button label', () => {
      render(<DeleteDialog {...defaultProps} />);
      
      const closeButton = screen.getByLabelText('Close dialog');
      expect(closeButton).toBeInTheDocument();
    });

    it('should have semantic form structure', () => {
      const { container } = render(<DeleteDialog {...defaultProps} />);
      
      const form = container.querySelector('form');
      expect(form).toBeInTheDocument();
    });

    it('should have accessible button labels', () => {
      render(<DeleteDialog {...defaultProps} />);
      
      const cancelButtons = screen.getAllByText('Cancel');
      const deleteButton = screen.getByText('Delete');
      
      expect(cancelButtons.length).toBeGreaterThan(0);
      expect(deleteButton).toBeInTheDocument();
    });

    it('should maintain focus within dialog content', async () => {
      const user = userEvent.setup();
      const { container } = render(<DeleteDialog {...defaultProps} />);
      
      const deleteButton = screen.getByText('Delete');
      await user.click(deleteButton);
      
      const focusableElements = container.querySelectorAll('button');
      expect(focusableElements.length).toBeGreaterThan(0);
    });

    it('should support keyboard navigation', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      render(<DeleteDialog {...defaultProps} onClose={onClose} />);
      
      // Tab to close button and press Enter
      const closeButton = screen.getByLabelText('Close dialog');
      closeButton.focus();
      expect(closeButton).toHaveFocus();
      
      await user.keyboard('{Enter}');
      expect(onClose).toHaveBeenCalled();
    });

    it('should use semantic dialog element for accessibility', () => {
      const { container } = render(<DeleteDialog {...defaultProps} />);
      
      // HTML dialog element is semantically correct without explicit role
      const dialog = container.querySelector('dialog');
      expect(dialog?.tagName).toBe('DIALOG');
    });
  });

  describe('Edge Cases', () => {
    it('should handle special characters in entity title', () => {
      render(<DeleteDialog {...defaultProps} entityTitle="Item & Special <Characters>" />);
      
      expect(screen.getByText('Item & Special <Characters>')).toBeInTheDocument();
    });

    it('should handle very long entity title', () => {
      const longTitle = 'A'.repeat(100);
      render(<DeleteDialog {...defaultProps} entityTitle={longTitle} />);
      
      expect(screen.getByText(longTitle)).toBeInTheDocument();
    });

    it('should handle special characters in delete action URL', () => {
      const { container } = render(
        <DeleteDialog {...defaultProps} deleteAction="/api/delete?id=123&type=movie" />
      );
      
      const form = container.querySelector('form');
      expect(form).toHaveAttribute('action', '/api/delete?id=123&type=movie');
    });

    it('should handle rapid onClose calls', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      render(<DeleteDialog {...defaultProps} onClose={onClose} />);
      
      const cancelButton = screen.getAllByText('Cancel')[0];
      await user.click(cancelButton);
      await user.click(cancelButton);
      
      expect(onClose).toHaveBeenCalled();
    });

    it('should handle isOpen toggling rapidly', () => {
      const { rerender, container } = render(<DeleteDialog {...defaultProps} isOpen={true} />);
      
      rerender(<DeleteDialog {...defaultProps} isOpen={false} />);
      rerender(<DeleteDialog {...defaultProps} isOpen={true} />);
      rerender(<DeleteDialog {...defaultProps} isOpen={false} />);
      
      const dialog = container.querySelector('dialog');
      expect(dialog).not.toHaveAttribute('open');
    });

    it('should handle onClose being undefined', async () => {
      const user = userEvent.setup();
      
      expect(() => {
        render(
          <DeleteDialog
            {...defaultProps}
            onClose={undefined as any}
          />
        );
      }).not.toThrow();
    });

    it('should handle multiple entity title updates', () => {
      const { rerender } = render(
        <DeleteDialog {...defaultProps} entityTitle="First Entity" />
      );
      
      expect(screen.getByText('First Entity')).toBeInTheDocument();
      
      rerender(
        <DeleteDialog {...defaultProps} entityTitle="Second Entity" />
      );
      
      expect(screen.queryByText('First Entity')).not.toBeInTheDocument();
      expect(screen.getByText('Second Entity')).toBeInTheDocument();
    });

    it('should handle empty deleteAction', () => {
      const { container } = render(
        <DeleteDialog {...defaultProps} deleteAction="" />
      );
      
      const form = container.querySelector('form');
      expect(form).toHaveAttribute('action', '');
    });
  });

  describe('State Management', () => {
    it('should maintain state across re-renders', () => {
      const { rerender } = render(<DeleteDialog {...defaultProps} />);
      
      const titleBefore = screen.getByText('Confirm Delete');
      expect(titleBefore).toBeInTheDocument();
      
      rerender(<DeleteDialog {...defaultProps} />);
      
      const titleAfter = screen.getByText('Confirm Delete');
      expect(titleAfter).toBeInTheDocument();
    });

    it('should update deleteAction prop correctly', () => {
      const { container, rerender } = render(
        <DeleteDialog {...defaultProps} deleteAction="/api/delete-1" />
      );
      
      let form = container.querySelector('form');
      expect(form).toHaveAttribute('action', '/api/delete-1');
      
      rerender(
        <DeleteDialog {...defaultProps} deleteAction="/api/delete-2" />
      );
      
      form = container.querySelector('form');
      expect(form).toHaveAttribute('action', '/api/delete-2');
    });

    it('should update onClose callback correctly', async () => {
      const user = userEvent.setup();
      const onClose1 = vi.fn();
      const onClose2 = vi.fn();
      
      const { rerender } = render(
        <DeleteDialog {...defaultProps} onClose={onClose1} />
      );
      
      const cancelButton = screen.getAllByText('Cancel')[0];
      await user.click(cancelButton);
      
      expect(onClose1).toHaveBeenCalled();
      
      vi.clearAllMocks();
      
      rerender(
        <DeleteDialog {...defaultProps} onClose={onClose2} />
      );
      
      const newCancelButton = screen.getAllByText('Cancel')[0];
      await user.click(newCancelButton);
      
      expect(onClose2).toHaveBeenCalled();
    });
  });

  describe('Integration Tests', () => {
    it('should handle complete delete flow', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      const { container } = render(
        <DeleteDialog
          {...defaultProps}
          onClose={onClose}
          entityTitle="Test Item"
        />
      );
      
      // Verify dialog is visible
      const dialog = container.querySelector('dialog');
      expect(dialog).toHaveAttribute('open');
      
      // Verify content
      expect(screen.getByText('Confirm Delete')).toBeInTheDocument();
      expect(screen.getByText('Test Item')).toBeInTheDocument();
      
      // Click delete
      const deleteButton = screen.getByRole('button', { name: 'Delete' });
      expect(deleteButton).toBeInTheDocument();
      
      await user.click(deleteButton);
      
      // Form should submit
      const form = container.querySelector('form');
      expect(form).toHaveAttribute('action', '/api/delete');
    });

    it('should handle complete cancel flow', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      render(
        <DeleteDialog
          {...defaultProps}
          onClose={onClose}
          entityTitle="Test Item"
        />
      );
      
      const cancelButtons = screen.getAllByText('Cancel');
      const cancelButton = cancelButtons[0];
      
      await user.click(cancelButton);
      
      expect(onClose).toHaveBeenCalledOnce();
    });

    it('should handle close button flow', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      render(
        <DeleteDialog
          {...defaultProps}
          onClose={onClose}
        />
      );
      
      const closeButton = screen.getByLabelText('Close dialog');
      await user.click(closeButton);
      
      expect(onClose).toHaveBeenCalledOnce();
    });

    it('should transition from open to closed state', async () => {
      const onClose = vi.fn();
      const { container, rerender } = render(
        <DeleteDialog {...defaultProps} isOpen={true} onClose={onClose} />
      );
      
      let dialog = container.querySelector('dialog');
      expect(dialog).toHaveAttribute('open');
      
      rerender(
        <DeleteDialog {...defaultProps} isOpen={false} onClose={onClose} />
      );
      
      await waitFor(() => {
        dialog = container.querySelector('dialog');
        expect(dialog).not.toHaveAttribute('open');
      });
    });
  });
});
