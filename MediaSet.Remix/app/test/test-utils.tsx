import { ReactElement } from 'react';
import { render, RenderOptions, waitFor, act, within, fireEvent, screen } from '@testing-library/react';
import '@testing-library/jest-dom';

/**
 * Custom render function that includes any necessary providers
 * Currently no providers are needed, but this is set up for future additions
 * (e.g., session providers, theme providers, etc.)
 */
function customRender(ui: ReactElement, options?: Omit<RenderOptions, 'wrapper'>) {
  return render(ui, { ...options });
}

export { customRender as render, waitFor, act, within, fireEvent, screen };
export type { RenderOptions };
