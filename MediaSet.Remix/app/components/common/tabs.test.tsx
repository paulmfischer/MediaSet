import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '~/test/test-utils';
import Tabs, { type TabConfig } from './tabs';

const basicTabs: TabConfig[] = [
  { id: 'one', label: 'Tab One', panel: <div>Panel One</div> },
  { id: 'two', label: 'Tab Two', panel: <div>Panel Two</div> },
  { id: 'three', label: 'Tab Three', panel: <div>Panel Three</div> },
];

describe('Tabs', () => {
  it('should render all tab labels', () => {
    render(<Tabs tabs={basicTabs} />);

    expect(screen.getByText('Tab One')).toBeInTheDocument();
    expect(screen.getByText('Tab Two')).toBeInTheDocument();
    expect(screen.getByText('Tab Three')).toBeInTheDocument();
  });

  it('should show the first tab panel by default', () => {
    render(<Tabs tabs={basicTabs} />);

    expect(screen.getByText('Panel One')).toBeInTheDocument();
    expect(screen.queryByText('Panel Two')).not.toBeInTheDocument();
    expect(screen.queryByText('Panel Three')).not.toBeInTheDocument();
  });

  it('should show the panel for the selected defaultTabId', () => {
    render(<Tabs tabs={basicTabs} defaultTabId="two" />);

    expect(screen.queryByText('Panel One')).not.toBeInTheDocument();
    expect(screen.getByText('Panel Two')).toBeInTheDocument();
  });

  it('should switch panels when a tab is clicked', () => {
    render(<Tabs tabs={basicTabs} />);

    fireEvent.click(screen.getByText('Tab Two'));

    expect(screen.queryByText('Panel One')).not.toBeInTheDocument();
    expect(screen.getByText('Panel Two')).toBeInTheDocument();
  });

  it('should render nothing when tabs array is empty', () => {
    const { container } = render(<Tabs tabs={[]} />);

    expect(container.firstChild).toBeNull();
  });
});
