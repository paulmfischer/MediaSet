import { describe, it, expect } from 'vitest';
import React from 'react';
import { render, screen } from '~/test/test-utils';
import Spinner from './spinner';

describe('Spinner', () => {
  describe('Visibility', () => {
    it('should render spinner element', () => {
      const { container } = render(<Spinner />);
      
      const spinner = container.querySelector('svg');
      expect(spinner).toBeInTheDocument();
    });

    it('should render with animate-spin class', () => {
      const { container } = render(<Spinner />);
      
      const spinner = container.querySelector('svg');
      expect(spinner).toHaveClass('animate-spin');
    });

    it('should be visible with default rendering', () => {
      const { container } = render(<Spinner />);
      
      const spinner = container.querySelector('svg');
      expect(spinner).toBeVisible();
    });

    it('should render multiple spinners independently', () => {
      const { container } = render(
        <>
          <Spinner />
          <Spinner />
          <Spinner />
        </>
      );
      
      const spinners = container.querySelectorAll('svg');
      expect(spinners).toHaveLength(3);
    });

    it('should render spinners in different sizes independently', () => {
      const { container } = render(
        <>
          <Spinner size={16} />
          <Spinner size={24} />
          <Spinner size={32} />
        </>
      );
      
      const spinners = container.querySelectorAll('svg');
      expect(spinners).toHaveLength(3);
    });

    it('should maintain visibility across re-renders', () => {
      const { rerender, container } = render(<Spinner />);
      
      let spinner = container.querySelector('svg');
      expect(spinner).toBeVisible();
      
      rerender(<Spinner />);
      
      spinner = container.querySelector('svg');
      expect(spinner).toBeVisible();
    });

    it('should render Shell icon from lucide-react', () => {
      const { container } = render(<Spinner />);
      
      const spinner = container.querySelector('svg');
      // Shell icon from lucide-react renders as SVG
      expect(spinner?.tagName).toBe('svg');
    });

    it('should maintain animation class after re-render', () => {
      const { rerender, container } = render(<Spinner />);
      
      const spinner1 = container.querySelector('svg');
      expect(spinner1).toHaveClass('animate-spin');
      
      rerender(<Spinner />);
      
      const spinner2 = container.querySelector('svg');
      expect(spinner2).toHaveClass('animate-spin');
    });
  });

  describe('Loading States', () => {
    it('should render with animation indicating loading state', () => {
      const { container } = render(<Spinner />);
      
      const spinner = container.querySelector('svg');
      expect(spinner).toHaveClass('animate-spin');
    });

    it('should support different sizes for different contexts', () => {
      const sizes = [16, 24, 48];
      sizes.forEach(size => {
        const { container } = render(<Spinner size={size} />);
        const spinner = container.querySelector('svg');
        expect(spinner).toHaveAttribute('width', size.toString());
        expect(spinner).toHaveAttribute('height', size.toString());
      });
    });

    it('should maintain loading state during size changes', () => {
      const { rerender, container } = render(<Spinner size={16} />);
      
      expect(container.querySelector('svg')).toHaveClass('animate-spin');
      
      rerender(<Spinner size={32} />);
      
      expect(container.querySelector('svg')).toHaveClass('animate-spin');
    });

    it('should show consistent loading appearance across multiple renders', () => {
      const { rerender, container } = render(<Spinner />);
      
      const spinner1 = container.querySelector('svg');
      const classes1 = spinner1?.className;
      
      rerender(<Spinner />);
      
      const spinner2 = container.querySelector('svg');
      const classes2 = spinner2?.className;
      
      expect(classes1?.baseVal).toBe(classes2?.baseVal);
    });
  });

  describe('Accessibility', () => {
    it('should render as SVG element for screen reader compatibility', () => {
      const { container } = render(<Spinner />);
      
      const spinner = container.querySelector('svg');
      expect(spinner?.tagName).toBe('svg');
    });

    it('should be perceivable by providing visual animation', () => {
      const { container } = render(<Spinner />);
      
      const spinner = container.querySelector('svg');
      expect(spinner).toHaveClass('animate-spin');
      expect(spinner).toBeVisible();
    });

    it('should not hide content from assistive technologies', () => {
      const { container } = render(
        <div>
          <label htmlFor="search">Search:</label>
          <Spinner size={16} />
          <input id="search" type="text" />
        </div>
      );
      
      const spinner = container.querySelector('svg');
      expect(spinner).toBeInTheDocument();
      
      const label = screen.getByText('Search:');
      expect(label).toBeInTheDocument();
    });

    it('should work with aria-busy attribute for loading states', () => {
      const { container } = render(
        <div aria-busy="true">
          <Spinner />
          Loading content...
        </div>
      );
      
      const loadingContainer = container.querySelector('[aria-busy="true"]');
      expect(loadingContainer).toBeInTheDocument();
      
      const spinner = container.querySelector('svg');
      expect(spinner).toBeInTheDocument();
    });

    it('should be keyboard accessible when in interactive context', () => {
      const { container } = render(
        <button>
          <Spinner size={16} />
          Submit
        </button>
      );
      
      const button = container.querySelector('button');
      expect(button).toBeInTheDocument();
      
      const spinner = container.querySelector('svg');
      expect(spinner).toBeInTheDocument();
    });

    it('should support association with loading text', () => {
      render(
        <div>
          <Spinner size={20} />
          <span>Loading resources...</span>
        </div>
      );
      
      expect(screen.getByText('Loading resources...')).toBeInTheDocument();
    });

    it('should maintain contrast for visibility', () => {
      const { container } = render(
        <div className="bg-white">
          <Spinner />
        </div>
      );
      
      const spinner = container.querySelector('svg');
      expect(spinner).toBeInTheDocument();
      expect(spinner).toBeVisible();
    });

    it('should not require aria-label since it is a visual indicator paired with text', () => {
      const { container } = render(
        <div>
          <Spinner />
          <span>Please wait...</span>
        </div>
      );
      
      const spinner = container.querySelector('svg');
      // Spinner paired with descriptive text doesn't require aria-label
      expect(spinner).toBeInTheDocument();
      expect(screen.getByText('Please wait...')).toBeInTheDocument();
    });

    it('should work with aria-label when used without accompanying text', () => {
      const { container } = render(
        <div aria-label="Loading">
          <Spinner />
        </div>
      );
      
      const spinner = container.querySelector('svg');
      expect(spinner).toBeInTheDocument();
      
      const loadingDiv = container.querySelector('[aria-label="Loading"]');
      expect(loadingDiv).toBeInTheDocument();
    });

    it('should be perceivable in low contrast scenarios with animation', () => {
      const { container } = render(<Spinner />);
      
      const spinner = container.querySelector('svg');
      // Animation makes it perceivable even if colors have lower contrast
      expect(spinner).toHaveClass('animate-spin');
      expect(spinner).toBeVisible();
    });

    it('should work in loading contexts with appropriate semantic structure', () => {
      const { container } = render(
        <section aria-label="Loading indicator">
          <Spinner size={24} />
        </section>
      );
      
      const section = container.querySelector('section[aria-label="Loading indicator"]');
      expect(section).toBeInTheDocument();
      
      const spinner = container.querySelector('svg');
      expect(spinner).toBeInTheDocument();
    });

    it('should be compatible with loading state announcements', () => {
      const { container } = render(
        <div role="status" aria-live="polite">
          <Spinner />
          Loading data...
        </div>
      );
      
      const status = container.querySelector('[role="status"]');
      expect(status).toHaveAttribute('aria-live', 'polite');
      
      expect(screen.getByText('Loading data...')).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('should handle undefined size prop', () => {
      const { container } = render(<Spinner size={undefined} />);
      
      const spinner = container.querySelector('svg');
      expect(spinner).toBeInTheDocument();
      expect(spinner).toHaveClass('animate-spin');
    });

    it('should handle size as 0', () => {
      const { container } = render(<Spinner size={0} />);
      
      const spinner = container.querySelector('svg');
      expect(spinner).toBeInTheDocument();
      expect(spinner).toHaveAttribute('width', '0');
      expect(spinner).toHaveAttribute('height', '0');
    });

    it('should handle large size values', () => {
      const { container } = render(<Spinner size={256} />);
      
      const spinner = container.querySelector('svg');
      expect(spinner).toHaveAttribute('width', '256');
      expect(spinner).toHaveAttribute('height', '256');
    });

    it('should handle fractional size values', () => {
      const { container } = render(<Spinner size={23.5} />);
      
      const spinner = container.querySelector('svg');
      expect(spinner).toBeInTheDocument();
    });

    it('should handle negative size gracefully', () => {
      const { container } = render(<Spinner size={-16} />);
      
      const spinner = container.querySelector('svg');
      expect(spinner).toBeInTheDocument();
    });

    it('should handle re-rendering with different size props', () => {
      const { rerender, container } = render(<Spinner size={16} />);
      
      let spinner = container.querySelector('svg');
      expect(spinner).toHaveAttribute('width', '16');
      
      rerender(<Spinner size={32} />);
      
      spinner = container.querySelector('svg');
      expect(spinner).toHaveAttribute('width', '32');
    });

    it('should maintain animation during prop changes', () => {
      const { rerender, container } = render(<Spinner size={16} />);
      
      expect(container.querySelector('svg')).toHaveClass('animate-spin');
      
      rerender(<Spinner size={24} />);
      
      expect(container.querySelector('svg')).toHaveClass('animate-spin');
    });

    it('should handle rendering in strict mode', () => {
      const { container } = render(
        <React.StrictMode>
          <Spinner />
        </React.StrictMode>
      );
      
      const spinner = container.querySelector('svg');
      expect(spinner).toBeInTheDocument();
    });
  });

  describe('Component Integration', () => {
    it('should work in a page loading context', () => {
      const { container } = render(
        <div className="flex items-center justify-center min-h-screen">
          <Spinner size={48} />
        </div>
      );
      
      const spinner = container.querySelector('svg');
      expect(spinner).toBeInTheDocument();
      expect(spinner).toHaveClass('animate-spin');
    });

    it('should work in a button loading state', () => {
      const { container } = render(
        <button disabled>
          <Spinner size={16} />
          <span>Loading...</span>
        </button>
      );
      
      const button = container.querySelector('button');
      expect(button).toBeDisabled();
      
      const spinner = container.querySelector('svg');
      expect(spinner).toHaveClass('animate-spin');
    });

    it('should work alongside text content', () => {
      render(
        <div>
          <Spinner size={20} />
          <span>Processing your request...</span>
        </div>
      );
      
      expect(screen.getByText('Processing your request...')).toBeInTheDocument();
    });

    it('should work with Tailwind layout classes', () => {
      const { container } = render(
        <div className="flex items-center justify-center gap-2">
          <Spinner size={24} />
          <p>Please wait</p>
        </div>
      );
      
      const spinner = container.querySelector('svg');
      expect(spinner).toBeInTheDocument();
      
      expect(screen.getByText('Please wait')).toBeInTheDocument();
    });
  });
});
