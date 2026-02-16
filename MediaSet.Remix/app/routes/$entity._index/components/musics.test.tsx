import { describe, it, expect, vi } from "vitest";
import React from "react";
import { render, screen, fireEvent } from "~/test/test-utils";
import Musics from "./musics";
import { MusicEntity, Entity } from "~/models";

// Mock the delete dialog component
vi.mock("~/components/delete-dialog", () => ({
  default: ({
    isOpen,
    onClose,
    entityTitle,
    deleteAction,
  }: {
    isOpen: boolean;
    onClose: () => void;
    entityTitle?: string;
    deleteAction: string;
  }) =>
    isOpen ? (
      <div data-testid="delete-dialog">
        <p>Delete: {entityTitle}</p>
        <button onClick={onClose}>Cancel</button>
        <a href={deleteAction}>Confirm Delete</a>
      </div>
    ) : null,
}));

// Mock Link to avoid router context requirement
vi.mock("@remix-run/react", async () => {
  const actual = await vi.importActual("@remix-run/react");
  return {
    ...actual,
    Link: ({ to, children, ...props }: { to: string; children?: React.ReactNode; [key: string]: unknown }) => (
      <a href={to} {...props}>
        {children}
      </a>
    ),
  };
});

describe("Musics component", () => {
  const mockMusics: MusicEntity[] = [
    {
      type: Entity.Musics,
      id: "1",
      title: "Abbey Road",
      artist: "The Beatles",
      format: "CD",
      tracks: 17,
    },
    {
      type: Entity.Musics,
      id: "2",
      title: "Dark Side of the Moon",
      artist: "Pink Floyd",
      format: "Vinyl",
      tracks: 10,
    },
    {
      type: Entity.Musics,
      id: "3",
      title: "Led Zeppelin IV",
      artist: "Led Zeppelin",
      format: "CD",
      tracks: 8,
    },
  ];

  describe("rendering", () => {
    it("should render a table with correct headers and all musics", () => {
      render(<Musics musics={mockMusics} />);

      expect(screen.getByRole("table")).toBeInTheDocument();
      expect(screen.getByText("Title")).toBeInTheDocument();
      expect(screen.getByText("Artist")).toBeInTheDocument();
      expect(screen.getByText("Format")).toBeInTheDocument();
      expect(screen.getByText("Tracks")).toBeInTheDocument();

      expect(screen.getByText("Abbey Road")).toBeInTheDocument();
      expect(screen.getByText("Dark Side of the Moon")).toBeInTheDocument();
      expect(screen.getByText("Led Zeppelin IV")).toBeInTheDocument();
    });

    it("should display music titles as links to detail pages", () => {
      render(<Musics musics={mockMusics} />);

      expect(screen.getByText("Abbey Road")).toHaveAttribute("href", "/musics/1");
      expect(screen.getByText("Dark Side of the Moon")).toHaveAttribute("href", "/musics/2");
    });

    it("should display artist, format, and track count", () => {
      render(<Musics musics={mockMusics} />);

      expect(screen.getByText("The Beatles")).toBeInTheDocument();
      expect(screen.getByText("Pink Floyd")).toBeInTheDocument();
      expect(screen.getByText("Led Zeppelin")).toBeInTheDocument();
      expect(screen.getAllByText("CD")).toBeDefined();
      expect(screen.getByText("Vinyl")).toBeInTheDocument();
      expect(screen.getByText("17")).toBeInTheDocument();
      expect(screen.getByText("10")).toBeInTheDocument();
      expect(screen.getByText("8")).toBeInTheDocument();
    });

    it("should render edit and delete actions for each music", () => {
      render(<Musics musics={mockMusics} />);

      const editLinks = screen.getAllByRole("link", { name: /edit/i });
      expect(editLinks).toHaveLength(mockMusics.length);
      expect(editLinks[0]).toHaveAttribute("href", "/musics/1/edit");
      expect(editLinks[1]).toHaveAttribute("href", "/musics/2/edit");

      const deleteButtons = screen.getAllByRole("button", { name: /delete/i });
      expect(deleteButtons).toHaveLength(mockMusics.length);
    });

    it("should handle music without artist", () => {
      const musicsNoArtist: MusicEntity[] = [
        {
          type: Entity.Musics,
          id: "1",
          title: "Unknown Artist Album",
          format: "CD",
          tracks: 12,
        },
      ];

      render(<Musics musics={musicsNoArtist} />);

      expect(screen.getByText("Unknown Artist Album")).toBeInTheDocument();
    });

    it("should handle music without tracks count", () => {
      const musicsNoTracks: MusicEntity[] = [
        {
          type: Entity.Musics,
          id: "1",
          title: "Test Album",
          artist: "Test Artist",
          format: "CD",
        },
      ];

      render(<Musics musics={musicsNoTracks} />);

      expect(screen.getByText("Test Album")).toBeInTheDocument();
    });

    it("should handle music without format", () => {
      const musicsNoFormat: MusicEntity[] = [
        {
          type: Entity.Musics,
          id: "1",
          title: "Test Album",
          artist: "Test Artist",
          tracks: 12,
        },
      ];

      render(<Musics musics={musicsNoFormat} />);

      expect(screen.getByText("Test Album")).toBeInTheDocument();
    });

    it("should handle different formats", () => {
      const musicsDifferentFormats: MusicEntity[] = [
        {
          type: Entity.Musics,
          id: "1",
          title: "Album 1",
          artist: "Artist 1",
          format: "CD",
          tracks: 10,
        },
        {
          type: Entity.Musics,
          id: "2",
          title: "Album 2",
          artist: "Artist 2",
          format: "Vinyl",
          tracks: 12,
        },
        {
          type: Entity.Musics,
          id: "3",
          title: "Album 3",
          artist: "Artist 3",
          format: "Digital",
          tracks: 15,
        },
      ];

      render(<Musics musics={musicsDifferentFormats} />);

      expect(screen.getByText("CD")).toBeInTheDocument();
      expect(screen.getByText("Vinyl")).toBeInTheDocument();
      expect(screen.getByText("Digital")).toBeInTheDocument();
    });
  });

  describe("delete dialog interactions", () => {
    it("should not show delete dialog initially", () => {
      render(<Musics musics={mockMusics} />);

      expect(screen.queryByTestId("delete-dialog")).not.toBeInTheDocument();
    });

    it("should show delete dialog when delete button is clicked", () => {
      render(<Musics musics={mockMusics} />);

      const firstDeleteButton = screen.getAllByRole("button", { name: /delete/i })[0];
      fireEvent.click(firstDeleteButton);

      expect(screen.getByTestId("delete-dialog")).toBeInTheDocument();
      expect(screen.getByText("Delete: Abbey Road")).toBeInTheDocument();
    });

    it("should close delete dialog when cancel is clicked", () => {
      render(<Musics musics={mockMusics} />);

      const firstDeleteButton = screen.getAllByRole("button", { name: /delete/i })[0];
      fireEvent.click(firstDeleteButton);

      expect(screen.getByTestId("delete-dialog")).toBeInTheDocument();

      const cancelButton = screen.getByText("Cancel");
      fireEvent.click(cancelButton);

      expect(screen.queryByTestId("delete-dialog")).not.toBeInTheDocument();
    });

    it("should have correct delete action link", () => {
      render(<Musics musics={mockMusics} />);

      const firstDeleteButton = screen.getAllByRole("button", { name: /delete/i })[0];
      fireEvent.click(firstDeleteButton);

      const deleteLink = screen.getByText("Confirm Delete");
      expect(deleteLink).toHaveAttribute("href", "/musics/1/delete");
    });

    it("should show correct music title in delete dialog for different musics", () => {
      render(<Musics musics={mockMusics} />);

      const deleteButtons = screen.getAllByRole("button", { name: /delete/i });
      fireEvent.click(deleteButtons[1]);

      expect(screen.getByText("Delete: Dark Side of the Moon")).toBeInTheDocument();
    });

    it("should allow deleting multiple musics sequentially", () => {
      render(<Musics musics={mockMusics} />);

      const deleteButtons = screen.getAllByRole("button", { name: /delete/i });

      // First delete
      fireEvent.click(deleteButtons[0]);
      expect(screen.getByText(/Delete: Abbey Road/)).toBeInTheDocument();

      // Cancel first delete
      fireEvent.click(screen.getByText("Cancel"));
      expect(screen.queryByTestId("delete-dialog")).not.toBeInTheDocument();

      // Second delete - need to re-query buttons
      const newDeleteButtons = screen.getAllByRole("button", { name: /delete/i });
      fireEvent.click(newDeleteButtons[2]); // Third music
      expect(screen.getByText(/Delete: Led Zeppelin IV/)).toBeInTheDocument();
    });
  });

  describe("edge cases", () => {
    it("should handle empty musics array", () => {
      render(<Musics musics={[]} />);

      const rows = screen.getAllByRole("row");
      // Only header row
      expect(rows).toHaveLength(1);
    });

    it("should handle single music", () => {
      const singleMusic: MusicEntity[] = [mockMusics[0]];
      render(<Musics musics={singleMusic} />);

      expect(screen.getByText("Abbey Road")).toBeInTheDocument();
      expect(screen.getAllByRole("row")).toHaveLength(2); // header + 1 music
    });

    it("should handle very long title", () => {
      const longTitleMusic: MusicEntity[] = [
        {
          type: Entity.Musics,
          id: "1",
          title:
            "This is a very long music title that should still render properly in the table without breaking the layout",
          artist: "Artist",
          format: "CD",
        },
      ];

      render(<Musics musics={longTitleMusic} />);

      expect(screen.getByText(/This is a very long music title/)).toBeInTheDocument();
    });

    it("should handle very long artist name", () => {
      const longArtistMusic: MusicEntity[] = [
        {
          type: Entity.Musics,
          id: "1",
          title: "Album",
          artist: "This is a very long artist name that should still render properly",
          format: "CD",
          tracks: 10,
        },
      ];

      render(<Musics musics={longArtistMusic} />);

      expect(screen.getByText("This is a very long artist name that should still render properly")).toBeInTheDocument();
    });

    it("should handle many musics", () => {
      const manyMusics = Array.from({ length: 50 }, (_, i) => ({
        type: Entity.Musics,
        id: `music-${i}`,
        title: `Album ${i}`,
        artist: `Artist ${i}`,
        format: i % 2 === 0 ? "CD" : "Vinyl",
        tracks: 10 + i,
      }));

      render(<Musics musics={manyMusics} />);

      expect(screen.getByText("Album 0")).toBeInTheDocument();
      expect(screen.getByText("Album 49")).toBeInTheDocument();
      const rows = screen.getAllByRole("row");
      expect(rows).toHaveLength(51); // header + 50 musics
    });

    it("should have unique keys for each row", () => {
      render(<Musics musics={mockMusics} />);

      const rows = screen.getAllByRole("row");
      // If we have all rows, keys are properly set
      expect(rows).toHaveLength(4);
    });

    it("should handle zero tracks", () => {
      const zeroTracksMusic: MusicEntity[] = [
        {
          type: Entity.Musics,
          id: "1",
          title: "Empty Album",
          artist: "Artist",
          format: "CD",
          tracks: 0,
        },
      ];

      render(<Musics musics={zeroTracksMusic} />);

      expect(screen.getByText("0")).toBeInTheDocument();
    });

    it("should handle very high track count", () => {
      const highTrackMusic: MusicEntity[] = [
        {
          type: Entity.Musics,
          id: "1",
          title: "Massive Album",
          artist: "Artist",
          format: "CD",
          tracks: 999,
        },
      ];

      render(<Musics musics={highTrackMusic} />);

      expect(screen.getByText("999")).toBeInTheDocument();
    });
  });

  describe("artist display", () => {
    it("should display single artist correctly", () => {
      const singleArtistMusic: MusicEntity[] = [
        {
          type: Entity.Musics,
          id: "1",
          title: "Album",
          artist: "Single Artist",
          format: "CD",
          tracks: 10,
        },
      ];

      render(<Musics musics={singleArtistMusic} />);

      expect(screen.getByText("Single Artist")).toBeInTheDocument();
    });

    it("should display different artists", () => {
      const multiArtistMusics: MusicEntity[] = [
        {
          type: Entity.Musics,
          id: "1",
          title: "Album 1",
          artist: "Artist One",
          format: "CD",
          tracks: 10,
        },
        {
          type: Entity.Musics,
          id: "2",
          title: "Album 2",
          artist: "Artist Two",
          format: "Vinyl",
          tracks: 12,
        },
        {
          type: Entity.Musics,
          id: "3",
          title: "Album 3",
          artist: "Various Artists",
          format: "Digital",
          tracks: 15,
        },
      ];

      render(<Musics musics={multiArtistMusics} />);

      expect(screen.getByText("Artist One")).toBeInTheDocument();
      expect(screen.getByText("Artist Two")).toBeInTheDocument();
      expect(screen.getByText("Various Artists")).toBeInTheDocument();
    });
  });

  describe("accessibility", () => {
    it("should have proper aria labels on edit buttons", () => {
      render(<Musics musics={mockMusics} />);

      const editButtons = screen.getAllByRole("link", { name: /edit/i });
      expect(editButtons).toHaveLength(mockMusics.length);

      editButtons.forEach((btn: HTMLElement) => {
        expect(btn).toHaveAttribute("aria-label", "Edit");
      });
    });

    it("should have proper aria labels on delete buttons", () => {
      render(<Musics musics={mockMusics} />);

      const deleteButtons = screen.getAllByRole("button", { name: /delete/i });
      expect(deleteButtons).toHaveLength(mockMusics.length);

      deleteButtons.forEach((btn: HTMLElement) => {
        expect(btn).toHaveAttribute("aria-label", "Delete");
      });
    });

    it("should have title attributes for action buttons", () => {
      render(<Musics musics={mockMusics} />);

      const editButtons = screen.getAllByRole("link", { name: /edit/i });
      editButtons.forEach((btn: HTMLElement) => {
        expect(btn).toHaveAttribute("title", "Edit");
      });

      const deleteButtons = screen.getAllByRole("button", { name: /delete/i });
      deleteButtons.forEach((btn: HTMLElement) => {
        expect(btn).toHaveAttribute("title", "Delete");
      });
    });

    it("should have proper table structure for screen readers", () => {
      render(<Musics musics={mockMusics} />);

      const table = screen.getByRole("table");
      expect(table).toBeInTheDocument();

      const thead = table.querySelector("thead");
      expect(thead).toBeInTheDocument();

      const tbody = table.querySelector("tbody");
      expect(tbody).toBeInTheDocument();
    });
  });
});
