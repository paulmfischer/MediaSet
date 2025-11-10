import React, { ReactElement } from 'react';
import { render, RenderOptions } from '@testing-library/react';
import '@testing-library/jest-dom';

/**
 * Custom render function that includes any necessary providers
 * Currently no providers are needed, but this is set up for future additions
 * (e.g., session providers, theme providers, etc.)
 */
function customRender(
  ui: ReactElement,
  options?: Omit<RenderOptions, 'wrapper'>,
) {
  return render(ui, { ...options });
}

// Re-export everything from @testing-library/react
export * from '@testing-library/react';
export { customRender as render };
