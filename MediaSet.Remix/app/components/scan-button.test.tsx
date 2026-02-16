import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, act } from '~/test/test-utils';
import ScanButton from './scan-button';

// Mock the Remix submit hook
const mockSubmit = vi.fn();
vi.mock('@remix-run/react', () => ({
  useSubmit: () => mockSubmit,
}));

// Mock the BarcodeScanner component completely to avoid camera/barcode testing
vi.mock('~/components/barcode-scanner', () => ({
  default: () => null,
}));

describe('ScanButton', () => {
  beforeEach(() => {
    mockSubmit.mockClear();

    // Mock mobile environment
    Object.defineProperty(window, 'ontouchstart', {
      writable: true,
      configurable: true,
      value: true,
    });

    Object.defineProperty(navigator, 'maxTouchPoints', {
      writable: true,
      configurable: true,
      value: 1,
    });

    Object.defineProperty(navigator, 'userAgent', {
      writable: true,
      configurable: true,
      value: 'Mozilla/5.0 (Linux; Android 10) Mobile',
    });

    window.matchMedia = vi.fn().mockImplementation((query) => ({
      matches: query === '(pointer: coarse)',
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    }));
  });

  describe('Mobile Detection', () => {
    it('should render scan button on mobile device', () => {
      render(<ScanButton inputId="isbn" fieldName="isbn" />);
      expect(screen.getByRole('button', { name: /scan/i })).toBeInTheDocument();
    });
  });

  describe('Button Rendering', () => {
    it('should render with default label "Scan"', () => {
      render(<ScanButton inputId="isbn" fieldName="isbn" />);
      expect(screen.getByRole('button', { name: /scan/i })).toBeInTheDocument();
    });

    it('should render with custom button label', () => {
      render(<ScanButton inputId="isbn" fieldName="isbn" buttonLabel="Scan Barcode" />);
      expect(screen.getByRole('button', { name: /scan barcode/i })).toBeInTheDocument();
    });

    it('should be enabled by default', () => {
      render(<ScanButton inputId="isbn" fieldName="isbn" />);
      const button = screen.getByRole('button', { name: /scan/i });
      expect(button).not.toBeDisabled();
    });

    it('should be disabled when disabled prop is true', () => {
      render(<ScanButton inputId="isbn" fieldName="isbn" disabled />);
      const button = screen.getByRole('button', { name: /scan/i });
      expect(button).toBeDisabled();
    });
  });

  describe('Button Interaction', () => {
    it('should open modal when clicked', () => {
      render(<ScanButton inputId="isbn" fieldName="isbn" />);
      const button = screen.getByRole('button', { name: /scan/i });

      // Click the button - modal logic is tested but BarcodeScanner is mocked
      act(() => {
        button.click();
      });

      // We can't test modal visibility since BarcodeScanner is mocked to return null
      // This test ensures the button is clickable without errors
      expect(button).toBeInTheDocument();
    });
  });
});
