import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import ErrorScreen from './error-screen';

describe('Error screen', () => {
  it('should offer recovery without exposing details by default', async () => {
    const retry = vi.fn();
    render(
      <MemoryRouter>
        <ErrorScreen title="Page not found" statusCode={404} data="private-error" onRetry={retry} />
      </MemoryRouter>
    );
    expect(screen.getByText('Page not found')).toBeInTheDocument();
    expect(screen.queryByText('private-error')).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /try again/i }));
    expect(retry).toHaveBeenCalledOnce();
    expect(screen.getByRole('link', { name: /home/i })).toHaveAttribute('href', '/');
  });
});
