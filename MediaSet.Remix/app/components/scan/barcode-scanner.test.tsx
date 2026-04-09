import { describe, it, expect, vi, beforeEach, beforeAll, afterAll } from 'vitest';
import { render, screen } from '~/test/test-utils';
import BarcodeScanner from './barcode-scanner';

describe('BarcodeScanner', () => {
  const mockOnClose = vi.fn();
  const mockOnDetected = vi.fn();

  // Suppress all console output for the entire test suite
  beforeAll(() => {
    vi.spyOn(console, 'log').mockImplementation(() => {});
    vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.spyOn(console, 'debug').mockImplementation(() => {});
    vi.spyOn(console, 'warn').mockImplementation(() => {});
  });

  afterAll(() => {
    vi.restoreAllMocks();
  });

  beforeEach(() => {
    mockOnClose.mockClear();
    mockOnDetected.mockClear();

    // Mock navigator.mediaDevices to prevent camera access attempts in tests
    Object.defineProperty(navigator, 'mediaDevices', {
      writable: true,
      configurable: true,
      value: undefined,
    });
  });

  describe('Rendering', () => {
    it('should not render when open is false', () => {
      const { container } = render(<BarcodeScanner open={false} onClose={mockOnClose} onDetected={mockOnDetected} />);

      expect(container.firstChild).toBeNull();
    });

    it('should render modal title when open', () => {
      render(<BarcodeScanner open={true} onClose={mockOnClose} onDetected={mockOnDetected} />);

      expect(screen.getByText('Scan Barcode')).toBeInTheDocument();
    });

    it('should render video element', () => {
      const { container } = render(<BarcodeScanner open={true} onClose={mockOnClose} onDetected={mockOnDetected} />);

      const video = container.querySelector('video');
      expect(video).toBeInTheDocument();
      expect(video).toHaveAttribute('autoPlay');
      expect(video).toHaveAttribute('playsInline');
      expect(video).toHaveAttribute('muted');
    });

    it('should render close button', () => {
      render(<BarcodeScanner open={true} onClose={mockOnClose} onDetected={mockOnDetected} />);

      expect(screen.getByRole('button', { name: /close/i })).toBeInTheDocument();
    });

    it('should render file upload button', () => {
      render(<BarcodeScanner open={true} onClose={mockOnClose} onDetected={mockOnDetected} />);

      expect(screen.getByText('📷 Upload Photo')).toBeInTheDocument();
    });
  });

  describe('User Interactions', () => {
    it('should call onClose when close button clicked', () => {
      render(<BarcodeScanner open={true} onClose={mockOnClose} onDetected={mockOnDetected} />);

      const closeButton = screen.getByRole('button', { name: /close/i });
      closeButton.click();

      expect(mockOnClose).toHaveBeenCalledTimes(1);
    });

    it('should have file input with correct attributes', () => {
      const { container } = render(<BarcodeScanner open={true} onClose={mockOnClose} onDetected={mockOnDetected} />);

      const fileInput = container.querySelector('input[type="file"]');
      expect(fileInput).toBeInTheDocument();
      expect(fileInput).toHaveAttribute('accept', 'image/*');
      expect(fileInput).toHaveAttribute('capture', 'environment');
    });
  });

  describe('Accessibility', () => {
    it('should have proper button types', () => {
      render(<BarcodeScanner open={true} onClose={mockOnClose} onDetected={mockOnDetected} />);

      const closeButton = screen.getByRole('button', { name: /close/i });
      expect(closeButton).toHaveAttribute('type', 'button');
    });
  });
});
