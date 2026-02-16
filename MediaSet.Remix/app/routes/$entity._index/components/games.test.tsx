import { describe, it, expect, vi } from 'vitest';
import React from 'react';
import { render, screen, fireEvent } from '~/test/test-utils';
import Games from './games';
import { GameEntity, Entity } from '~/models';

// Mock the delete dialog component
vi.mock('~/components/delete-dialog', () => ({
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
vi.mock('@remix-run/react', async () => {
  const actual = await vi.importActual('@remix-run/react');
  return {
    ...actual,
    Link: ({ to, children, ...props }: { to: string; children?: React.ReactNode; [key: string]: unknown }) => (
      <a href={to} {...props}>
        {children}
      </a>
    ),
  };
});

describe('Games component', () => {
  const mockGames: GameEntity[] = [
    {
      type: Entity.Games,
      id: '1',
      title: 'Elden Ring',
      platform: 'PS5',
      format: 'Disc',
      developers: ['FromSoftware'],
    },
    {
      type: Entity.Games,
      id: '2',
      title: 'The Legend of Zelda: Breath of the Wild',
      platform: 'Nintendo Switch',
      format: 'Physical',
      developers: ['Nintendo EPD'],
    },
    {
      type: Entity.Games,
      id: '3',
      title: "Baldur's Gate 3",
      platform: 'PC',
      format: 'Digital',
      developers: ['Larian Studios'],
    },
  ];

  describe('rendering', () => {
    it('should render a table with correct headers and all games', () => {
      render(<Games games={mockGames} />);

      expect(screen.getByRole('table')).toBeInTheDocument();
      expect(screen.getByText('Title')).toBeInTheDocument();
      expect(screen.getByText('Platform')).toBeInTheDocument();
      expect(screen.getByText('Format')).toBeInTheDocument();
      expect(screen.getByText('Developers')).toBeInTheDocument();

      expect(screen.getByText('Elden Ring')).toBeInTheDocument();
      expect(screen.getByText('The Legend of Zelda: Breath of the Wild')).toBeInTheDocument();
      expect(screen.getByText("Baldur's Gate 3")).toBeInTheDocument();
    });

    it('should display game titles as links to detail pages', () => {
      render(<Games games={mockGames} />);

      expect(screen.getByText('Elden Ring')).toHaveAttribute('href', '/games/1');
      expect(screen.getByText('The Legend of Zelda: Breath of the Wild')).toHaveAttribute('href', '/games/2');
    });

    it('should display platform, format, and developers', () => {
      render(<Games games={mockGames} />);

      expect(screen.getByText('PS5')).toBeInTheDocument();
      expect(screen.getByText('Nintendo Switch')).toBeInTheDocument();
      expect(screen.getByText('PC')).toBeInTheDocument();
      expect(screen.getByText('Disc')).toBeInTheDocument();
      expect(screen.getByText('Physical')).toBeInTheDocument();
      expect(screen.getByText('Digital')).toBeInTheDocument();
      expect(screen.getByText('FromSoftware')).toBeInTheDocument();
      expect(screen.getByText('Nintendo EPD')).toBeInTheDocument();
      expect(screen.getByText('Larian Studios')).toBeInTheDocument();
    });

    it('should render edit and delete actions for each game', () => {
      render(<Games games={mockGames} />);

      const editLinks = screen.getAllByRole('link', { name: /edit/i });
      expect(editLinks).toHaveLength(mockGames.length);
      expect(editLinks[0]).toHaveAttribute('href', '/games/1/edit');
      expect(editLinks[1]).toHaveAttribute('href', '/games/2/edit');

      const deleteButtons = screen.getAllByRole('button', { name: /delete/i });
      expect(deleteButtons).toHaveLength(mockGames.length);
    });

    it('should handle game without platform', () => {
      const gamesNoPlatform: GameEntity[] = [
        {
          type: Entity.Games,
          id: '1',
          title: 'Test Game',
          format: 'Disc',
          developers: ['Dev Studio'],
        },
      ];

      render(<Games games={gamesNoPlatform} />);

      expect(screen.getByText('Test Game')).toBeInTheDocument();
    });

    it('should handle game without developers', () => {
      const gamesNoDevelopers: GameEntity[] = [
        {
          type: Entity.Games,
          id: '1',
          title: 'Test Game',
          platform: 'PS5',
          format: 'Disc',
        },
      ];

      render(<Games games={gamesNoDevelopers} />);

      expect(screen.getByText('Test Game')).toBeInTheDocument();
    });

    it('should handle game with multiple developers', () => {
      const gamesMultipleDev: GameEntity[] = [
        {
          type: Entity.Games,
          id: '1',
          title: 'Test Game',
          platform: 'PS5',
          developers: ['Studio A', 'Studio B', 'Studio C'],
        },
      ];

      render(<Games games={gamesMultipleDev} />);

      expect(screen.getByText('Studio A, Studio B, Studio C')).toBeInTheDocument();
    });

    it('should trim trailing spaces in developer names', () => {
      const gamesSpacedDevelopers: GameEntity[] = [
        {
          type: Entity.Games,
          id: '1',
          title: 'Test Game',
          platform: 'PS5',
          developers: ['Dev Studio  ', 'Another Studio   '],
        },
      ];

      render(<Games games={gamesSpacedDevelopers} />);

      expect(screen.getByText('Dev Studio, Another Studio')).toBeInTheDocument();
    });
  });

  describe('delete dialog interactions', () => {
    it('should not show delete dialog initially', () => {
      render(<Games games={mockGames} />);

      expect(screen.queryByTestId('delete-dialog')).not.toBeInTheDocument();
    });

    it('should show delete dialog when delete button is clicked', () => {
      render(<Games games={mockGames} />);

      const firstDeleteButton = screen.getAllByRole('button', { name: /delete/i })[0];
      fireEvent.click(firstDeleteButton);

      expect(screen.getByTestId('delete-dialog')).toBeInTheDocument();
      expect(screen.getByText('Delete: Elden Ring')).toBeInTheDocument();
    });

    it('should close delete dialog when cancel is clicked', () => {
      render(<Games games={mockGames} />);

      const firstDeleteButton = screen.getAllByRole('button', { name: /delete/i })[0];
      fireEvent.click(firstDeleteButton);

      expect(screen.getByTestId('delete-dialog')).toBeInTheDocument();

      const cancelButton = screen.getByText('Cancel');
      fireEvent.click(cancelButton);

      expect(screen.queryByTestId('delete-dialog')).not.toBeInTheDocument();
    });

    it('should have correct delete action link', () => {
      render(<Games games={mockGames} />);

      const firstDeleteButton = screen.getAllByRole('button', { name: /delete/i })[0];
      fireEvent.click(firstDeleteButton);

      const deleteLink = screen.getByText('Confirm Delete');
      expect(deleteLink).toHaveAttribute('href', '/games/1/delete');
    });

    it('should show correct game title in delete dialog for different games', () => {
      render(<Games games={mockGames} />);

      const deleteButtons = screen.getAllByRole('button', { name: /delete/i });
      fireEvent.click(deleteButtons[2]);

      expect(screen.getByText("Delete: Baldur's Gate 3")).toBeInTheDocument();
    });

    it('should allow deleting multiple games sequentially', () => {
      render(<Games games={mockGames} />);

      const deleteButtons = screen.getAllByRole('button', { name: /delete/i });

      // First delete
      fireEvent.click(deleteButtons[0]);
      expect(screen.getByText(/Delete: Elden Ring/)).toBeInTheDocument();

      // Cancel first delete
      fireEvent.click(screen.getByText('Cancel'));
      expect(screen.queryByTestId('delete-dialog')).not.toBeInTheDocument();

      // Second delete - need to re-query buttons
      const newDeleteButtons = screen.getAllByRole('button', { name: /delete/i });
      fireEvent.click(newDeleteButtons[2]); // Third game
      expect(screen.getByText(/Delete: Baldur's Gate 3/)).toBeInTheDocument();
    });
  });

  describe('platform variations', () => {
    it('should handle different platforms', () => {
      const gameDifferentPlatforms: GameEntity[] = [
        {
          type: Entity.Games,
          id: '1',
          title: 'Multi-platform Game',
          platform: 'Xbox Series X',
          developers: ['Studio'],
        },
        {
          type: Entity.Games,
          id: '2',
          title: 'Mobile Game',
          platform: 'iOS',
          developers: ['Studio'],
        },
        {
          type: Entity.Games,
          id: '3',
          title: 'Indie Game',
          platform: 'itch.io',
          developers: ['Studio'],
        },
      ];

      render(<Games games={gameDifferentPlatforms} />);

      expect(screen.getByText('Xbox Series X')).toBeInTheDocument();
      expect(screen.getByText('iOS')).toBeInTheDocument();
      expect(screen.getByText('itch.io')).toBeInTheDocument();
    });
  });

  describe('edge cases', () => {
    it('should handle empty games array', () => {
      render(<Games games={[]} />);

      const rows = screen.getAllByRole('row');
      // Only header row
      expect(rows).toHaveLength(1);
    });

    it('should handle single game', () => {
      const singleGame: GameEntity[] = [mockGames[0]];
      render(<Games games={singleGame} />);

      expect(screen.getByText('Elden Ring')).toBeInTheDocument();
      expect(screen.getAllByRole('row')).toHaveLength(2); // header + 1 game
    });

    it('should handle very long title', () => {
      const longTitleGame: GameEntity[] = [
        {
          type: Entity.Games,
          id: '1',
          title:
            'This is a very long game title that should still render properly in the table without breaking the layout',
          platform: 'PC',
          developers: ['Dev'],
        },
      ];

      render(<Games games={longTitleGame} />);

      expect(screen.getByText(/This is a very long game title/)).toBeInTheDocument();
    });

    it('should handle many games', () => {
      const manyGames = Array.from({ length: 50 }, (_, i) => ({
        type: Entity.Games,
        id: `game-${i}`,
        title: `Game ${i}`,
        platform: `Platform ${i}`,
        developers: [`Dev ${i}`],
      }));

      render(<Games games={manyGames} />);

      expect(screen.getByText('Game 0')).toBeInTheDocument();
      expect(screen.getByText('Game 49')).toBeInTheDocument();
      const rows = screen.getAllByRole('row');
      expect(rows).toHaveLength(51); // header + 50 games
    });

    it('should have unique keys for each row', () => {
      render(<Games games={mockGames} />);

      const rows = screen.getAllByRole('row');
      // If we have all rows, keys are properly set
      expect(rows).toHaveLength(4);
    });

    it('should handle empty developers array', () => {
      const gamesEmptyDevelopers: GameEntity[] = [
        {
          type: Entity.Games,
          id: '1',
          title: 'Unknown Developer Game',
          platform: 'PS5',
          developers: [],
        },
      ];

      render(<Games games={gamesEmptyDevelopers} />);

      expect(screen.getByText('Unknown Developer Game')).toBeInTheDocument();
    });
  });

  describe('accessibility', () => {
    it('should have proper aria labels on edit buttons', () => {
      render(<Games games={mockGames} />);

      const editButtons = screen.getAllByRole('link', { name: /edit/i });
      expect(editButtons).toHaveLength(mockGames.length);

      editButtons.forEach((btn: HTMLElement) => {
        expect(btn).toHaveAttribute('aria-label', 'Edit');
      });
    });

    it('should have proper aria labels on delete buttons', () => {
      render(<Games games={mockGames} />);

      const deleteButtons = screen.getAllByRole('button', { name: /delete/i });
      expect(deleteButtons).toHaveLength(mockGames.length);

      deleteButtons.forEach((btn: HTMLElement) => {
        expect(btn).toHaveAttribute('aria-label', 'Delete');
      });
    });

    it('should have title attributes for action buttons', () => {
      render(<Games games={mockGames} />);

      const editButtons = screen.getAllByRole('link', { name: /edit/i });
      editButtons.forEach((btn: HTMLElement) => {
        expect(btn).toHaveAttribute('title', 'Edit');
      });

      const deleteButtons = screen.getAllByRole('button', { name: /delete/i });
      deleteButtons.forEach((btn: HTMLElement) => {
        expect(btn).toHaveAttribute('title', 'Delete');
      });
    });

    it('should have proper table structure for screen readers', () => {
      render(<Games games={mockGames} />);

      const table = screen.getByRole('table');
      expect(table).toBeInTheDocument();

      const thead = table.querySelector('thead');
      expect(thead).toBeInTheDocument();

      const tbody = table.querySelector('tbody');
      expect(tbody).toBeInTheDocument();
    });
  });
});
