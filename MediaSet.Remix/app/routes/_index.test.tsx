import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen } from '~/test/test-utils';
import Index, { meta, loader } from './_index';
import * as statsData from '~/api/stats-data';
import * as remixReact from '@remix-run/react';

vi.mock('~/api/stats-data');

const mockGetStats = vi.mocked(statsData.getStats);

vi.mock('@remix-run/react', async () => {
  const actual = await vi.importActual('@remix-run/react');
  return {
    ...actual,
    useLoaderData: vi.fn(),
  };
});

const mockStats = {
  bookStats: {
    total: 42,
    totalFormats: 3,
    formats: ['Hardcover', 'Paperback', 'eBook'],
    uniqueAuthors: 28,
    totalPages: 12504,
    formatBreakdown: {},
  },
  movieStats: {
    total: 156,
    totalFormats: 2,
    formats: ['Blu-ray', 'DVD'],
    totalTvSeries: 12,
    formatBreakdown: {},
  },
  gameStats: {
    total: 87,
    totalFormats: 3,
    formats: ['Disc', 'Digital', 'Physical'],
    totalPlatforms: 5,
    platforms: ['PS5', 'Xbox Series X', 'Switch', 'PC', 'PS4'],
    formatBreakdown: {},
    platformBreakdown: {},
  },
  musicStats: {
    total: 234,
    totalFormats: 2,
    formats: ['CD', 'Vinyl'],
    uniqueArtists: 156,
    totalTracks: 2847,
    formatBreakdown: {},
  },
};

const emptyStats = {
  bookStats: { total: 0, totalFormats: 0, formats: [], uniqueAuthors: 0, totalPages: 0, formatBreakdown: {} },
  movieStats: { total: 0, totalFormats: 0, formats: [], totalTvSeries: 0, formatBreakdown: {} },
  gameStats: {
    total: 0,
    totalFormats: 0,
    formats: [],
    totalPlatforms: 0,
    platforms: [],
    formatBreakdown: {},
    platformBreakdown: {},
  },
  musicStats: { total: 0, totalFormats: 0, formats: [], uniqueArtists: 0, totalTracks: 0, formatBreakdown: {} },
};

describe('_index route', () => {
  describe('meta function', () => {
    it('should return correct title and description', () => {
      const result = meta({ params: {} } as unknown as Parameters<typeof loader>[0]);
      expect(result).toEqual([
        { title: 'Dashboard | MediaSet' },
        { name: 'description', content: 'Your personal media collection dashboard' },
      ]);
    });

    it('should have all required meta tags', () => {
      const result = meta({ params: {} } as unknown as Parameters<typeof loader>[0]);
      expect(result).toHaveLength(2);
      const titles = result.filter((m: Record<string, unknown>) => m.title);
      const descriptions = result.filter((m: Record<string, unknown>) => m.name === 'description');
      expect(titles).toHaveLength(1);
      expect(descriptions).toHaveLength(1);
    });
  });

  describe('loader function', () => {
    beforeEach(() => {
      vi.clearAllMocks();
    });

    it('should fetch stats and return JSON response', async () => {
      mockGetStats.mockResolvedValueOnce(mockStats);
      const result = await loader();
      expect(mockGetStats).toHaveBeenCalled();
      expect(result).toBeDefined();
    });

    it('should handle empty statistics', async () => {
      mockGetStats.mockResolvedValueOnce(emptyStats);
      const result = await loader();
      expect(mockGetStats).toHaveBeenCalled();
      expect(result).toBeDefined();
    });

    it('should propagate getStats errors', async () => {
      const error = new Error('API Error');
      mockGetStats.mockRejectedValueOnce(error);
      await expect(loader()).rejects.toThrow('API Error');
    });
  });

  describe('Index component', () => {
    beforeEach(() => {
      vi.clearAllMocks();
      vi.mocked(remixReact.useLoaderData).mockReturnValue({ stats: mockStats });
    });

    describe('hero section', () => {
      it('should render welcome message', () => {
        render(<Index />);
        expect(screen.getByText('Welcome to MediaSet')).toBeInTheDocument();
        expect(screen.getByText('Your personal media collection dashboard')).toBeInTheDocument();
      });

      it('should display total items count', () => {
        render(<Index />);
        const expectedTotal =
          mockStats.bookStats.total +
          mockStats.movieStats.total +
          mockStats.gameStats.total +
          mockStats.musicStats.total;
        expect(screen.getByText(expectedTotal.toString())).toBeInTheDocument();
        expect(screen.getByText('Total Items')).toBeInTheDocument();
      });
    });

    describe('collection overview', () => {
      it('should render the Collection Overview section', () => {
        render(<Index />);
        expect(screen.getByText('Collection Overview')).toBeInTheDocument();
      });

      it('should show books detail panel by default', () => {
        render(<Index />);
        expect(screen.getByText('Total Pages')).toBeInTheDocument();
        expect(screen.getByText(mockStats.bookStats.totalPages.toLocaleString())).toBeInTheDocument();
        expect(screen.getByText('Unique Authors')).toBeInTheDocument();
        expect(screen.getByText(mockStats.bookStats.uniqueAuthors.toString())).toBeInTheDocument();
      });

      it('should not show movie detail panel by default', () => {
        render(<Index />);
        expect(screen.queryByText('Movies & TV Shows')).not.toBeInTheDocument();
      });

      it('should not show games detail panel by default', () => {
        render(<Index />);
        // Games heading only renders in the detail panel
        expect(screen.queryByText('Platform Breakdown')).not.toBeInTheDocument();
      });

      it('should not show music detail panel by default', () => {
        render(<Index />);
        expect(screen.queryByText('Total Tracks')).not.toBeInTheDocument();
      });
    });

    describe('books detail panel (default selection)', () => {
      it('should display book stat cards', () => {
        render(<Index />);
        expect(screen.getByText('Total Pages')).toBeInTheDocument();
        expect(screen.getByText('Unique Authors')).toBeInTheDocument();
        expect(screen.getAllByText('Formats').length).toBeGreaterThan(0);
      });

      it('should display book formats as tags when no breakdown data', () => {
        render(<Index />);
        for (const format of mockStats.bookStats.formats) {
          expect(screen.getByText(format)).toBeInTheDocument();
        }
      });

      it('should show format pie chart when multiple breakdown entries exist', () => {
        const statsWithBreakdown = {
          ...mockStats,
          bookStats: {
            ...mockStats.bookStats,
            formatBreakdown: { Hardcover: 20, Paperback: 15, eBook: 7 },
          },
        };
        vi.mocked(remixReact.useLoaderData).mockReturnValue({ stats: statsWithBreakdown });
        render(<Index />);
        expect(screen.getByText('Format Breakdown')).toBeInTheDocument();
      });
    });

    describe('empty state', () => {
      beforeEach(() => {
        vi.mocked(remixReact.useLoaderData).mockReturnValue({ stats: emptyStats });
      });

      it('should render empty state message', () => {
        render(<Index />);
        expect(screen.getByText('No media items yet')).toBeInTheDocument();
        expect(
          screen.getByText('Start building your collection by adding books, movies, games, or music.')
        ).toBeInTheDocument();
      });

      it('should still display the hero section', () => {
        render(<Index />);
        expect(screen.getByText('Welcome to MediaSet')).toBeInTheDocument();
        expect(screen.getByText('0')).toBeInTheDocument();
        expect(screen.getByText('Total Items')).toBeInTheDocument();
      });

      it('should not show Collection Overview in empty state', () => {
        render(<Index />);
        expect(screen.queryByText('Collection Overview')).not.toBeInTheDocument();
      });
    });

    describe('partial data states', () => {
      it('should calculate total items correctly from mixed categories', () => {
        const mixedStats = {
          bookStats: {
            total: 5,
            totalFormats: 1,
            formats: ['Book'],
            uniqueAuthors: 3,
            totalPages: 500,
            formatBreakdown: {},
          },
          movieStats: { total: 10, totalFormats: 1, formats: ['Movie'], totalTvSeries: 0, formatBreakdown: {} },
          gameStats: {
            total: 0,
            totalFormats: 0,
            formats: [],
            totalPlatforms: 0,
            platforms: [],
            formatBreakdown: {},
            platformBreakdown: {},
          },
          musicStats: {
            total: 3,
            totalFormats: 1,
            formats: ['CD'],
            uniqueArtists: 2,
            totalTracks: 30,
            formatBreakdown: {},
          },
        };
        vi.mocked(remixReact.useLoaderData).mockReturnValue({ stats: mixedStats });
        render(<Index />);
        expect(screen.getByText('18')).toBeInTheDocument();
      });

      it('should show books detail when books total is zero but other entities have data', () => {
        const partialStats = {
          ...mockStats,
          bookStats: { ...mockStats.bookStats, total: 0 },
        };
        vi.mocked(remixReact.useLoaderData).mockReturnValue({ stats: partialStats });
        render(<Index />);
        const expectedTotal = mockStats.movieStats.total + mockStats.gameStats.total + mockStats.musicStats.total;
        expect(screen.getByText(expectedTotal.toString())).toBeInTheDocument();
      });
    });

    describe('formatting', () => {
      it('should format large page counts with locale formatting', () => {
        const statsWithLargeNumbers = {
          ...mockStats,
          bookStats: { ...mockStats.bookStats, totalPages: 1000000 },
        };
        vi.mocked(remixReact.useLoaderData).mockReturnValue({ stats: statsWithLargeNumbers });
        render(<Index />);
        expect(screen.getByText((1000000).toLocaleString())).toBeInTheDocument();
      });
    });
  });
});
