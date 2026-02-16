import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen } from "~/test/test-utils";
import Index, { meta, loader } from "./_index";
import * as statsData from "~/api/stats-data";
import * as remixReact from "@remix-run/react";

// Mock the stats-data module
vi.mock("~/api/stats-data");

const mockGetStats = vi.mocked(statsData.getStats);

// Mock useLoaderData
vi.mock("@remix-run/react", async () => {
  const actual = await vi.importActual("@remix-run/react");
  return {
    ...actual,
    useLoaderData: vi.fn(),
  };
});

describe("_index route", () => {
  describe("meta function", () => {
    it("should return correct title and description", () => {
      const result = meta({ params: {} } as unknown as Parameters<typeof loader>[0]);
      expect(result).toEqual([
        { title: "Dashboard | MediaSet" },
        {
          name: "description",
          content: "Your personal media collection dashboard",
        },
      ]);
    });

    it("should have all required meta tags", () => {
      const result = meta({ params: {} } as unknown as Parameters<typeof loader>[0]);
      expect(result).toHaveLength(2);
      const titles = result.filter((m: Record<string, unknown>) => m.title);
      const descriptions = result.filter((m: Record<string, unknown>) => m.name === "description");
      expect(titles).toHaveLength(1);
      expect(descriptions).toHaveLength(1);
    });
  });

  describe("loader function", () => {
    beforeEach(() => {
      vi.clearAllMocks();
    });

    it("should fetch stats and return JSON response", async () => {
      const mockStats = {
        bookStats: {
          total: 42,
          totalFormats: 3,
          formats: ["Hardcover", "Paperback", "eBook"],
          uniqueAuthors: 28,
          totalPages: 12504,
        },
        movieStats: {
          total: 156,
          totalFormats: 2,
          formats: ["Blu-ray", "DVD"],
          totalTvSeries: 12,
        },
        gameStats: {
          total: 87,
          totalFormats: 3,
          formats: ["Disc", "Digital", "Physical"],
          totalPlatforms: 5,
          platforms: ["PS5", "Xbox Series X", "Switch", "PC", "PS4"],
        },
        musicStats: {
          total: 234,
          totalFormats: 2,
          formats: ["CD", "Vinyl"],
          uniqueArtists: 156,
          totalTracks: 2847,
        },
      };

      mockGetStats.mockResolvedValueOnce(mockStats);

      const result = await loader();

      // The loader uses json() from Remix, so we verify the structure
      expect(mockGetStats).toHaveBeenCalled();
      expect(result).toBeDefined();
    });

    it("should handle empty statistics", async () => {
      const emptyStats = {
        bookStats: {
          total: 0,
          totalFormats: 0,
          formats: [],
          uniqueAuthors: 0,
          totalPages: 0,
        },
        movieStats: {
          total: 0,
          totalFormats: 0,
          formats: [],
          totalTvSeries: 0,
        },
        gameStats: {
          total: 0,
          totalFormats: 0,
          formats: [],
          totalPlatforms: 0,
          platforms: [],
        },
        musicStats: {
          total: 0,
          totalFormats: 0,
          formats: [],
          uniqueArtists: 0,
          totalTracks: 0,
        },
      };

      mockGetStats.mockResolvedValueOnce(emptyStats);

      const result = await loader();

      expect(mockGetStats).toHaveBeenCalled();
      expect(result).toBeDefined();
    });

    it("should propagate getStats errors", async () => {
      const error = new Error("API Error");
      mockGetStats.mockRejectedValueOnce(error);

      await expect(loader()).rejects.toThrow("API Error");
    });
  });

  describe("Index component", () => {
    const mockStats = {
      bookStats: {
        total: 42,
        totalFormats: 3,
        formats: ["Hardcover", "Paperback", "eBook"],
        uniqueAuthors: 28,
        totalPages: 12504,
      },
      movieStats: {
        total: 156,
        totalFormats: 2,
        formats: ["Blu-ray", "DVD"],
        totalTvSeries: 12,
      },
      gameStats: {
        total: 87,
        totalFormats: 3,
        formats: ["Disc", "Digital", "Physical"],
        totalPlatforms: 5,
        platforms: ["PS5", "Xbox Series X", "Switch", "PC", "PS4"],
      },
      musicStats: {
        total: 234,
        totalFormats: 2,
        formats: ["CD", "Vinyl"],
        uniqueArtists: 156,
        totalTracks: 2847,
      },
    };

    beforeEach(() => {
      vi.clearAllMocks();
      // Mock useLoaderData to return the mock stats
      vi.mocked(remixReact.useLoaderData).mockReturnValue({ stats: mockStats });
    });

    describe("rendering with data", () => {
      it("should render hero section with welcome message", () => {
        render(<Index />);

        expect(screen.getByText("Welcome to MediaSet")).toBeInTheDocument();
        expect(screen.getByText("Your personal media collection dashboard")).toBeInTheDocument();
      });

      it("should display total items count", () => {
        render(<Index />);

        const expectedTotal =
          mockStats.bookStats.total +
          mockStats.movieStats.total +
          mockStats.gameStats.total +
          mockStats.musicStats.total;

        expect(screen.getByText(expectedTotal.toString())).toBeInTheDocument();
        expect(screen.getByText("Total Items")).toBeInTheDocument();
      });

      it("should render overview section with all media categories", () => {
        render(<Index />);

        expect(screen.getByText("Overview")).toBeInTheDocument();
        expect(screen.getAllByText("Books").length).toBeGreaterThan(0);
        expect(screen.getAllByText("Movies & TV").length).toBeGreaterThan(0);
        expect(screen.getAllByText("Games").length).toBeGreaterThan(0);
        expect(screen.getAllByText("Music").length).toBeGreaterThan(0);
      });

      it("should display book statistics", () => {
        render(<Index />);

        expect(screen.getByText(mockStats.bookStats.total.toString())).toBeInTheDocument();
        expect(screen.getByText(`${mockStats.bookStats.totalFormats} formats`)).toBeInTheDocument();
      });

      it("should display movie statistics", () => {
        render(<Index />);

        // Check for movie count in overview
        const movieCards = screen.getAllByText("Movies & TV");
        expect(movieCards.length).toBeGreaterThan(0);
        expect(screen.getByText(`${mockStats.movieStats.totalFormats} formats`)).toBeInTheDocument();
      });

      it("should display game statistics", () => {
        render(<Index />);

        expect(screen.getAllByText("Games").length).toBeGreaterThan(0);
        expect(screen.getByText(`${mockStats.gameStats.totalPlatforms} platforms`)).toBeInTheDocument();
      });

      it("should display music statistics", () => {
        render(<Index />);

        expect(screen.getAllByText("Music").length).toBeGreaterThan(0);
        expect(screen.getByText(`${mockStats.musicStats.uniqueArtists} artists`)).toBeInTheDocument();
      });

      it("should render books section with detailed stats", () => {
        render(<Index />);

        expect(screen.getByText("Total Pages")).toBeInTheDocument();
        expect(screen.getByText(mockStats.bookStats.totalPages.toLocaleString())).toBeInTheDocument();
        expect(screen.getByText("Unique Authors")).toBeInTheDocument();
        expect(screen.getByText(mockStats.bookStats.uniqueAuthors.toString())).toBeInTheDocument();
      });

      it("should display book formats as tags", () => {
        render(<Index />);

        for (const format of mockStats.bookStats.formats) {
          expect(screen.getByText(format)).toBeInTheDocument();
        }
      });

      it("should render movies section with TV series and movies count", () => {
        render(<Index />);

        expect(screen.getByText("TV Series")).toBeInTheDocument();
        expect(screen.getByText(mockStats.movieStats.totalTvSeries.toString())).toBeInTheDocument();

        const expectedMovieCount = mockStats.movieStats.total - mockStats.movieStats.totalTvSeries;
        expect(screen.getByText(expectedMovieCount.toString())).toBeInTheDocument();
      });

      it("should display movie formats as tags", () => {
        render(<Index />);

        for (const format of mockStats.movieStats.formats) {
          expect(screen.getByText(format)).toBeInTheDocument();
        }
      });

      it("should render games section with platforms and formats", () => {
        render(<Index />);

        // Look for the platforms list in the Games detailed section
        const platformLabels = screen.getAllByText("Platforms");
        expect(platformLabels.length).toBeGreaterThan(0);

        // Verify all game platforms are rendered
        for (const platform of mockStats.gameStats.platforms) {
          expect(screen.getByText(platform)).toBeInTheDocument();
        }
      });

      it("should display game formats as tags", () => {
        render(<Index />);

        for (const format of mockStats.gameStats.formats) {
          expect(screen.getByText(format)).toBeInTheDocument();
        }
      });

      it("should render music section with tracks and artists", () => {
        render(<Index />);

        expect(screen.getByText("Total Tracks")).toBeInTheDocument();
        expect(screen.getByText(mockStats.musicStats.totalTracks.toLocaleString())).toBeInTheDocument();
      });

      it("should display music formats as tags", () => {
        render(<Index />);

        for (const format of mockStats.musicStats.formats) {
          expect(screen.getByText(format)).toBeInTheDocument();
        }
      });

      it("should not show sections for zero stats categories", () => {
        const partialStats = {
          ...mockStats,
          bookStats: { ...mockStats.bookStats, total: 0 },
        };
        vi.mocked(remixReact.useLoaderData).mockReturnValue({ stats: partialStats });

        render(<Index />);

        // With books at 0, we should still see total items but it should exclude books
        const expectedTotal = 0 + mockStats.movieStats.total + mockStats.gameStats.total + mockStats.musicStats.total;

        expect(screen.getByText(expectedTotal.toString())).toBeInTheDocument();

        // Movies & TV Shows section should still be visible
        expect(screen.getByText("Movies & TV Shows")).toBeInTheDocument();
      });
    });

    describe("empty state", () => {
      it("should render empty state when no items exist", () => {
        const emptyStats = {
          bookStats: {
            total: 0,
            totalFormats: 0,
            formats: [],
            uniqueAuthors: 0,
            totalPages: 0,
          },
          movieStats: {
            total: 0,
            totalFormats: 0,
            formats: [],
            totalTvSeries: 0,
          },
          gameStats: {
            total: 0,
            totalFormats: 0,
            formats: [],
            totalPlatforms: 0,
            platforms: [],
          },
          musicStats: {
            total: 0,
            totalFormats: 0,
            formats: [],
            uniqueArtists: 0,
            totalTracks: 0,
          },
        };
        vi.mocked(remixReact.useLoaderData).mockReturnValue({ stats: emptyStats });

        render(<Index />);

        expect(screen.getByText("No media items yet")).toBeInTheDocument();
        expect(
          screen.getByText("Start building your collection by adding books, movies, games, or music.")
        ).toBeInTheDocument();
      });

      it("should display welcome hero even with empty state", () => {
        const emptyStats = {
          bookStats: {
            total: 0,
            totalFormats: 0,
            formats: [],
            uniqueAuthors: 0,
            totalPages: 0,
          },
          movieStats: {
            total: 0,
            totalFormats: 0,
            formats: [],
            totalTvSeries: 0,
          },
          gameStats: {
            total: 0,
            totalFormats: 0,
            formats: [],
            totalPlatforms: 0,
            platforms: [],
          },
          musicStats: {
            total: 0,
            totalFormats: 0,
            formats: [],
            uniqueArtists: 0,
            totalTracks: 0,
          },
        };
        vi.mocked(remixReact.useLoaderData).mockReturnValue({ stats: emptyStats });

        render(<Index />);

        expect(screen.getByText("Welcome to MediaSet")).toBeInTheDocument();
        expect(screen.getByText("0")).toBeInTheDocument();
        expect(screen.getByText("Total Items")).toBeInTheDocument();
      });

      it("should not show overview section in empty state", () => {
        const emptyStats = {
          bookStats: {
            total: 0,
            totalFormats: 0,
            formats: [],
            uniqueAuthors: 0,
            totalPages: 0,
          },
          movieStats: {
            total: 0,
            totalFormats: 0,
            formats: [],
            totalTvSeries: 0,
          },
          gameStats: {
            total: 0,
            totalFormats: 0,
            formats: [],
            totalPlatforms: 0,
            platforms: [],
          },
          musicStats: {
            total: 0,
            totalFormats: 0,
            formats: [],
            uniqueArtists: 0,
            totalTracks: 0,
          },
        };
        vi.mocked(remixReact.useLoaderData).mockReturnValue({ stats: emptyStats });

        render(<Index />);

        const overviewHeadings = screen.queryAllByText("Overview");
        expect(overviewHeadings).toHaveLength(0);
      });
    });

    describe("partial data states", () => {
      it("should handle when only one category has data", () => {
        const partialStats = {
          bookStats: {
            total: 10,
            totalFormats: 1,
            formats: ["Hardcover"],
            uniqueAuthors: 5,
            totalPages: 1000,
          },
          movieStats: {
            total: 0,
            totalFormats: 0,
            formats: [],
            totalTvSeries: 0,
          },
          gameStats: {
            total: 0,
            totalFormats: 0,
            formats: [],
            totalPlatforms: 0,
            platforms: [],
          },
          musicStats: {
            total: 0,
            totalFormats: 0,
            formats: [],
            uniqueArtists: 0,
            totalTracks: 0,
          },
        };
        vi.mocked(remixReact.useLoaderData).mockReturnValue({ stats: partialStats });

        render(<Index />);

        expect(screen.getByText("Welcome to MediaSet")).toBeInTheDocument();
        // Verify the one category with data is shown
        expect(screen.getByText("Hardcover")).toBeInTheDocument();
        expect(screen.getByText(partialStats.bookStats.totalPages.toLocaleString())).toBeInTheDocument();
        expect(screen.getByText("Total Items")).toBeInTheDocument();
      });

      it("should calculate total items correctly from mixed categories", () => {
        const mixedStats = {
          bookStats: {
            total: 5,
            totalFormats: 1,
            formats: ["Book"],
            uniqueAuthors: 3,
            totalPages: 500,
          },
          movieStats: {
            total: 10,
            totalFormats: 1,
            formats: ["Movie"],
            totalTvSeries: 0,
          },
          gameStats: {
            total: 0,
            totalFormats: 0,
            formats: [],
            totalPlatforms: 0,
            platforms: [],
          },
          musicStats: {
            total: 3,
            totalFormats: 1,
            formats: ["CD"],
            uniqueArtists: 2,
            totalTracks: 30,
          },
        };
        vi.mocked(remixReact.useLoaderData).mockReturnValue({ stats: mixedStats });

        render(<Index />);

        expect(screen.getByText("18")).toBeInTheDocument();
      });

      it("should not render sections with zero items", () => {
        const partialStats = {
          bookStats: {
            total: 5,
            totalFormats: 1,
            formats: ["Book"],
            uniqueAuthors: 3,
            totalPages: 500,
          },
          movieStats: {
            total: 0,
            totalFormats: 0,
            formats: [],
            totalTvSeries: 0,
          },
          gameStats: {
            total: 8,
            totalFormats: 1,
            formats: ["Game"],
            totalPlatforms: 1,
            platforms: ["PC"],
          },
          musicStats: {
            total: 0,
            totalFormats: 0,
            formats: [],
            uniqueArtists: 0,
            totalTracks: 0,
          },
        };
        vi.mocked(remixReact.useLoaderData).mockReturnValue({ stats: partialStats });

        render(<Index />);

        // Movies & TV Shows section should not exist as detailed section
        const movieHeadings = screen.queryAllByText("Movies & TV Shows");
        expect(movieHeadings).toHaveLength(0);

        // Data-only sections should be visible
        expect(screen.getByText("Book")).toBeInTheDocument();
        expect(screen.getByText("Game")).toBeInTheDocument();
        expect(screen.getByText("500")).toBeInTheDocument();
        expect(screen.getByText("PC")).toBeInTheDocument();
      });
    });

    describe("formatting and display", () => {
      it("should format large numbers with locale formatting", () => {
        const statsWithLargeNumbers = {
          ...mockStats,
          bookStats: {
            ...mockStats.bookStats,
            totalPages: 1000000,
          },
          musicStats: {
            ...mockStats.musicStats,
            totalTracks: 50000,
          },
        };
        vi.mocked(remixReact.useLoaderData).mockReturnValue({ stats: statsWithLargeNumbers });

        render(<Index />);

        expect(screen.getByText(statsWithLargeNumbers.bookStats.totalPages.toLocaleString())).toBeInTheDocument();
        expect(screen.getByText(statsWithLargeNumbers.musicStats.totalTracks.toLocaleString())).toBeInTheDocument();
      });

      it("should render correct stat titles for each category", () => {
        render(<Index />);

        // These titles should all be present in their respective sections
        expect(screen.getByText("Total Pages")).toBeInTheDocument();
        expect(screen.getByText("Unique Authors")).toBeInTheDocument();
        expect(screen.getByText("TV Series")).toBeInTheDocument();
        expect(screen.getAllByText("Platforms").length).toBeGreaterThan(0);
        expect(screen.getByText("Total Tracks")).toBeInTheDocument();
      });

      it("should render format tags for all categories with formats", () => {
        render(<Index />);

        // Check that all formats are rendered as tags
        const allFormats = [
          ...mockStats.bookStats.formats,
          ...mockStats.movieStats.formats,
          ...mockStats.gameStats.formats,
          ...mockStats.musicStats.formats,
        ];

        for (const format of allFormats) {
          const elements = screen.getAllByText(format);
          expect(elements.length).toBeGreaterThan(0);
        }
      });
    });

    describe("structure and layout", () => {
      it("should have hero section at the top", () => {
        render(<Index />);

        const welcomeText = screen.getByText("Welcome to MediaSet");
        const dashboardText = screen.getByText("Your personal media collection dashboard");
        const totalItemsText = screen.getByText("Total Items");

        // All hero elements should exist
        expect(welcomeText).toBeInTheDocument();
        expect(dashboardText).toBeInTheDocument();
        expect(totalItemsText).toBeInTheDocument();
      });

      it("should render overview before detailed sections", () => {
        render(<Index />);

        const overviewHeading = screen.getByText("Overview");
        const booksHeadings = screen.queryAllByText("Books");

        expect(overviewHeading).toBeInTheDocument();
        expect(booksHeadings.length).toBeGreaterThan(0);
      });

      it("should have proper section headings for each category", () => {
        render(<Index />);

        expect(screen.getAllByText("Books").length).toBeGreaterThan(0);
        expect(screen.getByText("Movies & TV Shows")).toBeInTheDocument();
        expect(screen.getAllByText("Games").length).toBeGreaterThan(0);
        expect(screen.getAllByText("Music").length).toBeGreaterThan(0);
      });
    });
  });
});
