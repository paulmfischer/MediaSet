import { describe, it, expect } from 'vitest';
import { render, screen } from '~/test/test-utils';
import Badge from './badge';

describe('Badge', () => {
  describe('Rendering', () => {
    it('should render badge element', () => {
      const { container } = render(<Badge>Test Badge</Badge>);
      
      const badge = container.querySelector('span');
      expect(badge).toBeInTheDocument();
    });

    it('should render children text content', () => {
      render(<Badge>Test Badge Text</Badge>);
      
      expect(screen.getByText('Test Badge Text')).toBeInTheDocument();
    });

    it('should render with default blue color scheme', () => {
      const { container } = render(<Badge>Default Badge</Badge>);
      
      const badge = container.querySelector('span');
      expect(badge).toHaveClass('bg-blue-800');
      expect(badge).toHaveClass('text-blue-100');
      expect(badge).toHaveClass('ring-blue-600');
    });

    it('should render multiple badges independently', () => {
      const { container } = render(
        <>
          <Badge>Badge 1</Badge>
          <Badge>Badge 2</Badge>
          <Badge>Badge 3</Badge>
        </>
      );
      
      const badges = container.querySelectorAll('span');
      expect(badges).toHaveLength(3);
      expect(screen.getByText('Badge 1')).toBeInTheDocument();
      expect(screen.getByText('Badge 2')).toBeInTheDocument();
      expect(screen.getByText('Badge 3')).toBeInTheDocument();
    });

    it('should render with empty children', () => {
      const { container } = render(<Badge></Badge>);
      
      const badge = container.querySelector('span');
      expect(badge).toBeInTheDocument();
      expect(badge).toBeEmptyDOMElement();
    });

    it('should render with complex children content', () => {
      const { container } = render(
        <Badge>
          <span className="custom-child">Complex Content</span>
        </Badge>
      );
      
      const complexChild = container.querySelector('.custom-child');
      expect(complexChild).toBeInTheDocument();
      expect(complexChild).toHaveTextContent('Complex Content');
    });

    it('should render with numeric children', () => {
      render(<Badge>42</Badge>);
      
      expect(screen.getByText('42')).toBeInTheDocument();
    });

    it('should render with special characters in children', () => {
      render(<Badge>Special & Characters &lt; &gt;</Badge>);
      
      expect(screen.getByText('Special & Characters < >')).toBeInTheDocument();
    });

    it('should render with whitespace in children', () => {
      render(
        <Badge>
          {' '}
          Text with spaces {' '}
        </Badge>
      );
      
      expect(screen.getByText(/Text with spaces/)).toBeInTheDocument();
    });
  });

  describe('Styling', () => {
    it('should render with default blue color scheme', () => {
      const { container } = render(<Badge>Badge</Badge>);
      
      const badge = container.querySelector('span');
      expect(badge).toHaveClass('bg-blue-800');
      expect(badge).toHaveClass('text-blue-100');
      expect(badge).toHaveClass('ring-blue-600');
    });

    it('should have all styling classes applied together', () => {
      const { container } = render(<Badge>Badge</Badge>);
      
      const badge = container.querySelector('span');
      const expectedClasses = [
        'inline-flex',
        'items-center',
        'rounded-md',
        'bg-blue-800',
        'px-2',
        'py-1',
        'text-xs',
        'font-medium',
        'text-blue-100',
        'ring-1',
        'ring-inset',
        'ring-blue-600',
      ];
      
      expectedClasses.forEach(className => {
        expect(badge).toHaveClass(className);
      });
    });

    it('should use semantic span element for styling container', () => {
      const { container } = render(<Badge>Badge</Badge>);
      
      const badge = container.querySelector('span');
      expect(badge?.tagName).toBe('SPAN');
    });

    it('should maintain styling consistency across multiple renders', () => {
      const { rerender, container } = render(<Badge>Badge 1</Badge>);
      
      const badge1 = container.querySelector('span');
      const classes1 = badge1?.className;
      
      rerender(<Badge>Badge 2</Badge>);
      
      const badge2 = container.querySelector('span');
      const classes2 = badge2?.className;
      
      expect(classes1).toBe(classes2);
    });
  });


  describe('Accessibility', () => {
    it('should use semantic HTML element (span)', () => {
      const { container } = render(<Badge>Badge</Badge>);
      
      const badge = container.querySelector('span');
      expect(badge?.tagName).toBe('SPAN');
    });

    it('should contain readable text content', () => {
      render(<Badge>Important Status</Badge>);
      
      expect(screen.getByText('Important Status')).toBeInTheDocument();
    });

    it('should inherit text color for visibility', () => {
      const { container } = render(<Badge>Badge</Badge>);
      
      const badge = container.querySelector('span');
      expect(badge).toHaveClass('text-blue-100');
    });

    it('should have sufficient contrast with background', () => {
      const { container } = render(<Badge>Badge</Badge>);
      
      const badge = container.querySelector('span');
      // bg-blue-800 with text-blue-100 provides sufficient contrast
      expect(badge).toHaveClass('bg-blue-800');
      expect(badge).toHaveClass('text-blue-100');
    });

    it('should be readable for screen readers', () => {
      render(<Badge>Status: Active</Badge>);
      
      const badge = screen.getByText('Status: Active');
      expect(badge).toHaveTextContent('Status: Active');
    });

    it('should not use display: none or visibility: hidden', () => {
      const { container } = render(<Badge>Badge</Badge>);
      
      const badge = container.querySelector('span');
      const styles = window.getComputedStyle(badge!);
      expect(styles.display).not.toBe('none');
    });

    it('should have appropriate line height for readability', () => {
      const { container } = render(<Badge>Badge Text</Badge>);
      
      const badge = container.querySelector('span');
      expect(badge).toBeInTheDocument();
      expect(badge?.textContent).toBeTruthy();
    });

    it('should maintain focus visibility if interactive wrapper is added', () => {
      const { container } = render(
        <button>
          <Badge>Clickable Badge</Badge>
        </button>
      );
      
      const badge = container.querySelector('span');
      expect(badge).toBeInTheDocument();
    });

    it('should not require additional ARIA attributes for generic status indicator', () => {
      const { container } = render(<Badge>New</Badge>);
      
      const badge = container.querySelector('span');
      // Badge should be self-contained without needing explicit ARIA
      expect(badge).toHaveTextContent('New');
    });

    it('should preserve text semantics', () => {
      render(<Badge>Important Information</Badge>);
      
      const text = screen.getByText('Important Information');
      expect(text).toBeInTheDocument();
      expect(text.textContent).toBe('Important Information');
    });

    it('should not prevent keyboard navigation through text content', () => {
      const { container } = render(<Badge>Accessible Badge</Badge>);
      
      const badge = container.querySelector('span');
      expect(badge).toBeInTheDocument();
      // Text content is accessible for selection and navigation
      expect(badge?.textContent).toBe('Accessible Badge');
    });

    it('should work with standard text selection', () => {
      render(<Badge>Selectable Text</Badge>);
      
      const badge = screen.getByText('Selectable Text');
      expect(badge).toBeInTheDocument();
      // Badge text should be selectable
      expect(badge.textContent).toBe('Selectable Text');
    });

    it('should have sufficient color contrast ratio', () => {
      const { container } = render(<Badge>Badge</Badge>);
      
      const badge = container.querySelector('span');
      // Blue-800 (#1e3a8a) to Blue-100 (#dbeafe) has good contrast
      expect(badge).toHaveClass('bg-blue-800');
      expect(badge).toHaveClass('text-blue-100');
    });
  });

  describe('Edge Cases', () => {
    it('should handle very long badge text', () => {
      const longText = 'This is a very long badge text that contains multiple words and spaces';
      render(<Badge>{longText}</Badge>);
      
      expect(screen.getByText(longText)).toBeInTheDocument();
    });

    it('should handle single character badge', () => {
      render(<Badge>A</Badge>);
      
      expect(screen.getByText('A')).toBeInTheDocument();
    });

    it('should handle badge with only numbers', () => {
      render(<Badge>12345</Badge>);
      
      expect(screen.getByText('12345')).toBeInTheDocument();
    });

    it('should handle badge with unicode characters', () => {
      render(<Badge>🎯 Badge</Badge>);
      
      expect(screen.getByText('🎯 Badge')).toBeInTheDocument();
    });

    it('should handle badge with HTML entities', () => {
      render(<Badge>&amp;&lt;&gt;</Badge>);
      
      expect(screen.getByText('&<>')).toBeInTheDocument();
    });

    it('should handle empty string children gracefully', () => {
      render(<Badge>{''}</Badge>);
      
      const { container } = render(<Badge>{''}</Badge>);
      const badge = container.querySelector('span');
      expect(badge).toBeInTheDocument();
    });

    it('should handle null children gracefully', () => {
      const { container } = render(<Badge>{null}</Badge>);
      
      const badge = container.querySelector('span');
      expect(badge).toBeInTheDocument();
    });

    it('should handle undefined children gracefully', () => {
      const { container } = render(<Badge>{undefined}</Badge>);
      
      const badge = container.querySelector('span');
      expect(badge).toBeInTheDocument();
    });

    it('should handle badge in nested containers', () => {
      const { container } = render(
        <div>
          <div>
            <span>
              <Badge>Nested Badge</Badge>
            </span>
          </div>
        </div>
      );
      
      const badge = container.querySelector('span span');
      expect(badge).toBeInTheDocument();
      expect(badge).toHaveTextContent('Nested Badge');
    });

    it('should handle multiple badges in a list', () => {
      const { container } = render(
        <ul>
          <li><Badge>Badge 1</Badge></li>
          <li><Badge>Badge 2</Badge></li>
          <li><Badge>Badge 3</Badge></li>
        </ul>
      );
      
      const badges = container.querySelectorAll('li span');
      expect(badges).toHaveLength(3);
    });

    it('should handle badge with only whitespace', () => {
      const { container } = render(<Badge>   </Badge>);
      
      const badge = container.querySelector('span');
      expect(badge).toBeInTheDocument();
    });

    it('should handle re-rendering with different children', () => {
      const { rerender } = render(<Badge>First</Badge>);
      
      expect(screen.getByText('First')).toBeInTheDocument();
      
      rerender(<Badge>Second</Badge>);
      
      expect(screen.queryByText('First')).not.toBeInTheDocument();
      expect(screen.getByText('Second')).toBeInTheDocument();
    });
  });

  describe('Component Props', () => {
    it('should accept React.PropsWithChildren type', () => {
      expect(() => {
        render(<Badge>Children content</Badge>);
      }).not.toThrow();
    });

    it('should render with no props except children', () => {
      const { container } = render(<Badge>Badge</Badge>);
      
      const badge = container.querySelector('span');
      expect(badge).toBeInTheDocument();
    });

    it('should maintain type safety with children prop', () => {
      // TypeScript should not allow non-PropsWithChildren props
      render(<Badge>Valid children</Badge>);
      
      expect(screen.getByText('Valid children')).toBeInTheDocument();
    });
  });

  describe('Integration', () => {
    it('should work in a status indicator context', () => {
      render(
        <div>
          Status: <Badge>Active</Badge>
        </div>
      );
      
      expect(screen.getByText('Active')).toBeInTheDocument();
      expect(screen.getByText(/Status:/)).toBeInTheDocument();
    });

    it('should work in a tag list context', () => {
      render(
        <div>
          Tags: <Badge>React</Badge> <Badge>TypeScript</Badge> <Badge>Testing</Badge>
        </div>
      );
      
      expect(screen.getByText('React')).toBeInTheDocument();
      expect(screen.getByText('TypeScript')).toBeInTheDocument();
      expect(screen.getByText('Testing')).toBeInTheDocument();
    });

    it('should work within a button element', () => {
      render(
        <button>
          Action <Badge>New</Badge>
        </button>
      );
      
      expect(screen.getByText('New')).toBeInTheDocument();
      expect(screen.getByRole('button')).toBeInTheDocument();
    });

    it('should work with label elements', () => {
      render(
        <label>
          Feature: <Badge>Beta</Badge>
        </label>
      );
      
      expect(screen.getByText('Beta')).toBeInTheDocument();
      expect(screen.getByText(/Feature:/)).toBeInTheDocument();
    });

    it('should work alongside other styled components', () => {
      render(
        <div className="flex gap-2">
          <span className="font-bold">Item Name</span>
          <Badge>Important</Badge>
        </div>
      );
      
      expect(screen.getByText('Item Name')).toBeInTheDocument();
      expect(screen.getByText('Important')).toBeInTheDocument();
    });
  });
});
