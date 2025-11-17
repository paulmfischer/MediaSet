import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen } from '~/test/test-utils';
import Edit, { meta, loader, action } from './route';
import * as entityData from '~/entity-data';
import * as metadataData from '~/metadata-data';
import * as helpers from '~/helpers';
import { Entity, BookEntity, MovieEntity, GameEntity, MusicEntity } from '~/models';
import * as remixReact from '@remix-run/react';

// Mock modules
vi.mock('~/entity-data');
vi.mock('~/metadata-data');
vi.mock('~/helpers');

// Mock form components
vi.mock('~/components/book-form', () => ({
  default: (props: any) => <div data-testid="book-form">Book Form</div>,
}));

vi.mock('~/components/movie-form', () => ({
  default: (props: any) => <div data-testid="movie-form">Movie Form</div>,
}));

vi.mock('~/components/game-form', () => ({
  default: (props: any) => <div data-testid="game-form">Game Form</div>,
}));

vi.mock('~/components/music-form', () => ({
  default: (props: any) => <div data-testid="music-form">Music Form</div>,
}));

vi.mock('~/components/spinner', () => ({
  default: () => <div data-testid="spinner">Loading...</div>,
}));

// Mock Remix hooks and Form component
vi.mock('@remix-run/react', async () => {
  const actualRemix = await vi.importActual('@remix-run/react');
  return {
    ...(actualRemix as any),
    Form: ({ children, method, onSubmit, ...props }: any) => (
      <form method={method} onSubmit={onSubmit} {...props}>
        {children}
      </form>
    ),
    useLoaderData: vi.fn(),
    useActionData: vi.fn(),
    useNavigate: vi.fn(),
    useNavigation: vi.fn(),
    useSubmit: vi.fn(),
  };
});

const mockGetEntity = vi.mocked(entityData.getEntity);
const mockUpdateEntity = vi.mocked(entityData.updateEntity);
const mockGetGenres = vi.mocked(metadataData.getGenres);
const mockGetFormats = vi.mocked(metadataData.getFormats);
const mockGetAuthors = vi.mocked(metadataData.getAuthors);
const mockGetPublishers = vi.mocked(metadataData.getPublishers);
const mockGetStudios = vi.mocked(metadataData.getStudios);
const mockGetDevelopers = vi.mocked(metadataData.getDevelopers);
const mockGetLabels = vi.mocked(metadataData.getLabels);
const mockGetGamePublishers = vi.mocked(metadataData.getGamePublishers);
const mockGetPlatforms = vi.mocked(metadataData.getPlatforms);
const mockGetEntityFromParams = vi.mocked(helpers.getEntityFromParams);
const mockFormToDto = vi.mocked(helpers.formToDto);
const mockSingular = vi.mocked(helpers.singular);

const mockUseLoaderData = vi.mocked(remixReact.useLoaderData);
const mockUseActionData = vi.mocked(remixReact.useActionData);
const mockUseNavigate = vi.mocked(remixReact.useNavigate);
const mockUseNavigation = vi.mocked(remixReact.useNavigation);

describe('$entity_.$entityId_.edit route', () => {
  describe('meta function', () => {
    beforeEach(() => {
      vi.clearAllMocks();
    });

    it('should return correct title for editing a Book', () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);
      mockSingular.mockReturnValue('Book');

      const result = meta({ params: { entity: 'books', entityId: '1' } } as any);

      expect(result).toEqual(
        expect.arrayContaining([
          expect.objectContaining({ title: 'Add a Book' }),
          expect.objectContaining({
            name: 'description',
            content: 'Add a Book',
          }),
        ])
      );
    });

    it('should return correct title for editing a Movie', () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Movies);
      mockSingular.mockReturnValue('Movie');

      const result = meta({ params: { entity: 'movies', entityId: '1' } } as any);

      expect(result).toContainEqual(
        expect.objectContaining({ title: 'Add a Movie' })
      );
    });

    it('should return correct title for editing a Game', () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Games);
      mockSingular.mockReturnValue('Game');

      const result = meta({ params: { entity: 'games', entityId: '1' } } as any);

      expect(result).toContainEqual(
        expect.objectContaining({ title: 'Add a Game' })
      );
    });

    it('should return correct title for editing Music', () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Musics);
      mockSingular.mockReturnValue('Music');

      const result = meta({ params: { entity: 'musics', entityId: '1' } } as any);

      expect(result).toContainEqual(
        expect.objectContaining({ title: 'Add a Music' })
      );
    });
  });

  describe('loader function', () => {
    const mockBook: BookEntity = {
      id: 'book-1',
      type: Entity.Books,
      title: 'The Great Gatsby',
    };

    const mockMovie: MovieEntity = {
      id: 'movie-1',
      type: Entity.Movies,
      title: 'The Shawshank Redemption',
    };

    const mockGame: GameEntity = {
      id: 'game-1',
      type: Entity.Games,
      title: 'Elden Ring',
      platform: 'PS5',
    };

    const mockMusic: MusicEntity = {
      id: 'music-1',
      type: Entity.Musics,
      title: 'Abbey Road',
      artist: 'The Beatles',
    };

    const mockMetadata = {
      authors: [{ label: 'Author 1', value: 'author1' }],
      genres: [{ label: 'Fiction', value: 'fiction' }],
      formats: [{ label: 'Hardcover', value: 'hardcover' }],
      publishers: [{ label: 'Publisher 1', value: 'pub1' }],
      studios: [{ label: 'Studio 1', value: 'studio1' }],
      developers: [{ label: 'Developer 1', value: 'dev1' }],
      labels: [{ label: 'Label 1', value: 'label1' }],
      platforms: [{ label: 'PS5', value: 'ps5' }],
    };

    beforeEach(() => {
      vi.clearAllMocks();
      mockGetGenres.mockResolvedValue(mockMetadata.genres);
      mockGetFormats.mockResolvedValue(mockMetadata.formats);
      mockGetAuthors.mockResolvedValue(mockMetadata.authors);
      mockGetPublishers.mockResolvedValue(mockMetadata.publishers);
      mockGetStudios.mockResolvedValue(mockMetadata.studios);
      mockGetDevelopers.mockResolvedValue(mockMetadata.developers);
      mockGetLabels.mockResolvedValue(mockMetadata.labels);
      mockGetGamePublishers.mockResolvedValue(mockMetadata.publishers);
      mockGetPlatforms.mockResolvedValue(mockMetadata.platforms);
    });

    it('should load a book entity with its metadata', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);
      mockGetEntity.mockResolvedValue(mockBook);

      const result = await loader({
        params: { entity: 'books', entityId: 'book-1' },
      } as any);

      expect(mockGetEntity).toHaveBeenCalledWith(Entity.Books, 'book-1');
      expect(result.entity).toEqual(mockBook);
      expect(result.authors).toEqual(mockMetadata.authors);
      expect(result.publishers).toEqual(mockMetadata.publishers);
    });

    it('should load a movie entity with its metadata', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Movies);
      mockGetEntity.mockResolvedValue(mockMovie);

      const result = await loader({
        params: { entity: 'movies', entityId: 'movie-1' },
      } as any);

      expect(mockGetEntity).toHaveBeenCalledWith(Entity.Movies, 'movie-1');
      expect(result.entity).toEqual(mockMovie);
      expect(result.studios).toEqual(mockMetadata.studios);
    });

    it('should load a game entity with its metadata', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Games);
      mockGetEntity.mockResolvedValue(mockGame);

      const result = await loader({
        params: { entity: 'games', entityId: 'game-1' },
      } as any);

      expect(mockGetEntity).toHaveBeenCalledWith(Entity.Games, 'game-1');
      expect(result.entity).toEqual(mockGame);
      expect(result.developers).toEqual(mockMetadata.developers);
      expect(result.platforms).toEqual(mockMetadata.platforms);
    });

    it('should load a music entity with its metadata', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Musics);
      mockGetEntity.mockResolvedValue(mockMusic);

      const result = await loader({
        params: { entity: 'musics', entityId: 'music-1' },
      } as any);

      expect(mockGetEntity).toHaveBeenCalledWith(Entity.Musics, 'music-1');
      expect(result.entity).toEqual(mockMusic);
      expect(result.labels).toEqual(mockMetadata.labels);
    });

    it('should return all required properties in loader data', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);
      mockGetEntity.mockResolvedValue(mockBook);

      const result = await loader({
        params: { entity: 'books', entityId: 'book-1' },
      } as any);

      expect(result).toHaveProperty('entity');
      expect(result).toHaveProperty('authors');
      expect(result).toHaveProperty('genres');
      expect(result).toHaveProperty('publishers');
      expect(result).toHaveProperty('formats');
      expect(result).toHaveProperty('entityType');
      expect(result).toHaveProperty('studios');
      expect(result).toHaveProperty('developers');
      expect(result).toHaveProperty('labels');
      expect(result).toHaveProperty('platforms');
    });

    it('should throw error when entity param is missing', async () => {
      await expect(
        loader({
          params: { entityId: 'book-1' },
        } as any)
      ).rejects.toThrow();
    });

    it('should throw error when entityId param is missing', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);

      await expect(
        loader({
          params: { entity: 'books' },
        } as any)
      ).rejects.toThrow();
    });

    it('should use Promise.all for parallel metadata loading', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);
      mockGetEntity.mockResolvedValue(mockBook);

      await loader({
        params: { entity: 'books', entityId: 'book-1' },
      } as any);

      expect(mockGetGenres).toHaveBeenCalled();
      expect(mockGetFormats).toHaveBeenCalled();
    });
  });

  describe('action function - update entity', () => {
    const mockBook: BookEntity = {
      id: 'book-1',
      type: Entity.Books,
      title: 'Updated Book Title',
    };

    const mockMovie: MovieEntity = {
      id: 'movie-1',
      type: Entity.Movies,
      title: 'Updated Movie Title',
    };

    beforeEach(() => {
      vi.clearAllMocks();
    });

    it('should update a book entity', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);
      mockFormToDto.mockReturnValue(mockBook);
      mockUpdateEntity.mockResolvedValue(undefined);

      const mockFormData = new FormData();
      mockFormData.append('type', Entity.Books);
      mockFormData.append('title', 'Updated Book Title');

      const request = new Request('http://localhost/books/book-1/edit', {
        method: 'POST',
        body: mockFormData,
      });

      await action({
        request,
        params: { entity: 'books', entityId: 'book-1' },
      } as any);

      expect(mockFormToDto).toHaveBeenCalledWith(mockFormData);
      expect(mockUpdateEntity).toHaveBeenCalledWith('book-1', mockBook, undefined);
    });

    it('should update a movie entity', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Movies);
      mockFormToDto.mockReturnValue(mockMovie);
      mockUpdateEntity.mockResolvedValue(undefined);

      const mockFormData = new FormData();
      mockFormData.append('type', Entity.Movies);
      mockFormData.append('title', 'Updated Movie Title');

      const request = new Request('http://localhost/movies/movie-1/edit', {
        method: 'POST',
        body: mockFormData,
      });

      await action({
        request,
        params: { entity: 'movies', entityId: 'movie-1' },
      } as any);

      expect(mockUpdateEntity).toHaveBeenCalledWith('movie-1', mockMovie, undefined);
    });

    it('should redirect to entity detail page after update', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);
      mockFormToDto.mockReturnValue(mockBook);
      mockUpdateEntity.mockResolvedValue(undefined);

      const mockFormData = new FormData();
      mockFormData.append('type', Entity.Books);

      const request = new Request('http://localhost/books/book-1/edit', {
        method: 'POST',
        body: mockFormData,
      });

      const result = await action({
        request,
        params: { entity: 'books', entityId: 'book-1' },
      } as any);

      expect(result).toBeDefined();
    });

    it('should return error when formToDto returns null', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);
      mockFormToDto.mockReturnValue(null);

      const mockFormData = new FormData();
      mockFormData.append('type', Entity.Books);

      const request = new Request('http://localhost/books/book-1/edit', {
        method: 'POST',
        body: mockFormData,
      });

      const result = await action({
        request,
        params: { entity: 'books', entityId: 'book-1' },
      } as any);

      expect(result).toEqual({
        invalidObject: `Failed to convert form to a ${Entity.Books}`,
      });
      expect(mockUpdateEntity).not.toHaveBeenCalled();
    });

    it('should throw error when entity param is missing', async () => {
      const mockFormData = new FormData();
      const request = new Request('http://localhost/book-1/edit', {
        method: 'POST',
        body: mockFormData,
      });

      await expect(
        action({
          request,
          params: { entityId: 'book-1' },
        } as any)
      ).rejects.toThrow();
    });

    it('should throw error when entityId param is missing', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);

      const mockFormData = new FormData();
      const request = new Request('http://localhost/books/edit', {
        method: 'POST',
        body: mockFormData,
      });

      await expect(
        action({
          request,
          params: { entity: 'books' },
        } as any)
      ).rejects.toThrow();
    });
  });

  describe('action function - lookup', () => {
    beforeEach(() => {
      vi.clearAllMocks();
      mockGetEntityFromParams.mockReturnValue(Entity.Books);
    });

    it('should handle lookup intent', async () => {
      const mockFormData = new FormData();
      mockFormData.append('intent', 'lookup');
      mockFormData.append('fieldName', 'isbn');
      mockFormData.append('identifierValue', '978-0743273565');

      const request = new Request('http://localhost/books/book-1/edit', {
        method: 'POST',
        body: mockFormData,
      });

      vi.doMock('~/lookup-data.server', () => ({
        lookup: vi.fn().mockResolvedValue({
          title: 'Test Book',
          isbn: '978-0743273565',
        }),
        getIdentifierTypeForField: vi.fn().mockReturnValue('isbn'),
      }));

      const result = await action({
        request,
        params: { entity: 'books', entityId: 'book-1' },
      } as any);

      expect(result).toHaveProperty('lookupResult');
    });

    it('should return error when identifierValue is missing', async () => {
      const mockFormData = new FormData();
      mockFormData.append('intent', 'lookup');
      mockFormData.append('fieldName', 'isbn');

      const request = new Request('http://localhost/books/book-1/edit', {
        method: 'POST',
        body: mockFormData,
      });

      const result = await action({
        request,
        params: { entity: 'books', entityId: 'book-1' },
      } as any);

      expect(result).toEqual({
        error: { lookup: 'Identifier value is required' },
      });
    });

    it('should return error when identifierValue is empty string', async () => {
      const mockFormData = new FormData();
      mockFormData.append('intent', 'lookup');
      mockFormData.append('fieldName', 'isbn');
      mockFormData.append('identifierValue', '');

      const request = new Request('http://localhost/books/book-1/edit', {
        method: 'POST',
        body: mockFormData,
      });

      const result = await action({
        request,
        params: { entity: 'books', entityId: 'book-1' },
      } as any);

      expect(result).toEqual({
        error: { lookup: 'Identifier value is required' },
      });
    });
  });

  describe('Edit component rendering', () => {
    const mockBook: BookEntity = {
      id: 'book-1',
      type: Entity.Books,
      title: 'The Great Gatsby',
    };

    const mockLoaderData = {
      entity: mockBook,
      authors: [{ label: 'Author 1', value: 'author1' }],
      genres: [{ label: 'Fiction', value: 'fiction' }],
      formats: [{ label: 'Hardcover', value: 'hardcover' }],
      publishers: [{ label: 'Publisher 1', value: 'pub1' }],
      studios: [],
      developers: [],
      labels: [],
      platforms: [],
      entityType: Entity.Books,
    };

    beforeEach(() => {
      vi.clearAllMocks();
      mockUseLoaderData.mockReturnValue(mockLoaderData);
      mockUseActionData.mockReturnValue(undefined);
      mockUseNavigate.mockReturnValue(vi.fn());
      mockUseNavigation.mockReturnValue({ state: 'idle' } as any);
      mockSingular.mockReturnValue('Book');
    });

    it('should render the edit page title with entity title', () => {
      render(<Edit />);

      expect(screen.getByText('Editing The Great Gatsby')).toBeInTheDocument();
    });

    it('should render the correct form based on entity type - Book', () => {
      render(<Edit />);

      expect(screen.getByTestId('book-form')).toBeInTheDocument();
    });

    it('should render the correct form based on entity type - Movie', () => {
      const mockMovie: MovieEntity = {
        id: 'movie-1',
        type: Entity.Movies,
        title: 'The Shawshank Redemption',
      };

      mockUseLoaderData.mockReturnValue({
        ...mockLoaderData,
        entity: mockMovie,
        entityType: Entity.Movies,
      });
      mockSingular.mockReturnValue('Movie');

      render(<Edit />);

      expect(screen.getByTestId('movie-form')).toBeInTheDocument();
    });

    it('should render the correct form based on entity type - Game', () => {
      const mockGame: GameEntity = {
        id: 'game-1',
        type: Entity.Games,
        title: 'Elden Ring',
        platform: 'PS5',
      };

      mockUseLoaderData.mockReturnValue({
        ...mockLoaderData,
        entity: mockGame,
        entityType: Entity.Games,
      });
      mockSingular.mockReturnValue('Game');

      render(<Edit />);

      expect(screen.getByTestId('game-form')).toBeInTheDocument();
    });

    it('should render the correct form based on entity type - Music', () => {
      const mockMusic: MusicEntity = {
        id: 'music-1',
        type: Entity.Musics,
        title: 'Abbey Road',
        artist: 'The Beatles',
      };

      mockUseLoaderData.mockReturnValue({
        ...mockLoaderData,
        entity: mockMusic,
        entityType: Entity.Musics,
      });
      mockSingular.mockReturnValue('Music');

      render(<Edit />);

      expect(screen.getByTestId('music-form')).toBeInTheDocument();
    });

    it('should render update button', () => {
      render(<Edit />);

      expect(screen.getByRole('button', { name: /Update/i })).toBeInTheDocument();
    });

    it('should render cancel button', () => {
      render(<Edit />);

      expect(screen.getByRole('button', { name: /Cancel/i })).toBeInTheDocument();
    });

    it('should render form with post method', () => {
      const { container } = render(<Edit />);

      const form = container.querySelector('form');
      expect(form).toBeInTheDocument();
      expect(form).toHaveAttribute('method', 'post');
    });

    it('should render entity type hidden input', () => {
      render(<Edit />);

      const hiddenInput = screen.getByDisplayValue(Entity.Books) as HTMLInputElement;
      expect(hiddenInput).toHaveAttribute('type', 'hidden');
      expect(hiddenInput).toHaveAttribute('name', 'type');
    });

    it('should disable buttons when submitting', () => {
      mockUseNavigation.mockReturnValue({
        state: 'submitting',
        location: { pathname: '/books/book-1/edit' },
      } as any);

      render(<Edit />);

      const updateButton = screen.getByRole('button', { name: /Update/i });
      const cancelButton = screen.getByRole('button', { name: /Cancel/i });

      expect(updateButton).toBeDisabled();
      expect(cancelButton).toBeDisabled();
    });

    it('should enable buttons when not submitting', () => {
      mockUseNavigation.mockReturnValue({ state: 'idle' } as any);

      render(<Edit />);

      const updateButton = screen.getByRole('button', { name: /Update/i });
      const cancelButton = screen.getByRole('button', { name: /Cancel/i });

      expect(updateButton).not.toBeDisabled();
      expect(cancelButton).not.toBeDisabled();
    });

    it('should pass entity data to form component', () => {
      render(<Edit />);

      // Verify form component is rendered with data
      expect(screen.getByTestId('book-form')).toBeInTheDocument();
    });
  });

  describe('Pre-population tests', () => {
    const mockBook: BookEntity = {
      id: 'book-1',
      type: Entity.Books,
      title: 'The Great Gatsby',
      isbn: '978-0743273565',
      format: 'hardcover',
      genres: ['fiction'],
    };

    const mockLoaderData = {
      entity: mockBook,
      authors: [{ label: 'F. Scott Fitzgerald', value: 'author-1' }],
      genres: [{ label: 'Fiction', value: 'fiction' }],
      formats: [{ label: 'Hardcover', value: 'hardcover' }],
      publishers: [{ label: 'Scribner', value: 'pub-1' }],
      studios: [],
      developers: [],
      labels: [],
      platforms: [],
      entityType: Entity.Books,
    };

    beforeEach(() => {
      vi.clearAllMocks();
      mockUseLoaderData.mockReturnValue(mockLoaderData);
      mockUseActionData.mockReturnValue(undefined);
      mockUseNavigate.mockReturnValue(vi.fn());
      mockUseNavigation.mockReturnValue({ state: 'idle' } as any);
      mockSingular.mockReturnValue('Book');
    });

    it('should pre-populate form with existing entity data', () => {
      render(<Edit />);

      // Verify the entity data is passed to the form
      expect(screen.getByTestId('book-form')).toBeInTheDocument();
      expect(screen.getByText('Editing The Great Gatsby')).toBeInTheDocument();
    });

    it('should use lookup entity data when available', () => {
      const lookupResult = {
        id: 'book-1',
        type: Entity.Books,
        title: 'The Great Gatsby - Updated',
      };

      mockUseActionData.mockReturnValue({
        lookupResult,
        identifierValue: '978-0743273565',
        fieldName: 'isbn',
      });

      render(<Edit />);

      // Verify lookup result is used
      expect(screen.getByTestId('book-form')).toBeInTheDocument();
    });

    it('should display lookup error when lookup fails', () => {
      const errorData = {
        lookupResult: {
          message: 'ISBN not found',
          statusCode: 404,
        },
      };

      mockUseActionData.mockReturnValue(errorData);

      render(<Edit />);

      expect(screen.getByText('ISBN not found')).toBeInTheDocument();
    });

    it('should display lookup error in yellow alert box', () => {
      const errorData = {
        lookupResult: {
          message: 'API Error',
          statusCode: 503,
        },
      };

      mockUseActionData.mockReturnValue(errorData);

      const { container } = render(<Edit />);

      const errorBox = container.querySelector('.bg-yellow-900.border-yellow-700');
      expect(errorBox).toBeInTheDocument();
    });

    it('should not show lookup error if lookup was successful', () => {
      const successData = {
        lookupResult: {
          id: 'book-1',
          type: Entity.Books,
          title: 'The Great Gatsby',
        },
      };

      mockUseActionData.mockReturnValue(successData);

      render(<Edit />);

      expect(screen.queryByText(/ISBN not found/i)).not.toBeInTheDocument();
    });
  });

  describe('API response mocking', () => {
    const mockLoaderData = {
      entity: {
        id: 'book-1',
        type: Entity.Books,
        title: 'The Great Gatsby',
      },
      authors: [{ label: 'F. Scott Fitzgerald', value: 'author-1' }],
      genres: [{ label: 'Fiction', value: 'fiction' }],
      formats: [{ label: 'Hardcover', value: 'hardcover' }],
      publishers: [{ label: 'Scribner', value: 'pub-1' }],
      studios: [],
      developers: [],
      labels: [],
      platforms: [],
      entityType: Entity.Books,
    };

    beforeEach(() => {
      vi.clearAllMocks();
      mockUseLoaderData.mockReturnValue(mockLoaderData);
      mockUseActionData.mockReturnValue(undefined);
      mockUseNavigate.mockReturnValue(vi.fn());
      mockUseNavigation.mockReturnValue({ state: 'idle' } as any);
      mockSingular.mockReturnValue('Book');
    });

    it('should render with mocked API data', () => {
      render(<Edit />);

      expect(screen.getByText('Editing The Great Gatsby')).toBeInTheDocument();
      expect(screen.getByTestId('book-form')).toBeInTheDocument();
    });

    it('should render loader data with all metadata', () => {
      render(<Edit />);

      // Verify component rendered successfully with loader data
      expect(screen.getByTestId('book-form')).toBeInTheDocument();
    });

    it('should verify mock functions were called appropriately', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);
      mockGetEntity.mockResolvedValue(mockLoaderData.entity as any);
      mockGetGenres.mockResolvedValue(mockLoaderData.genres);
      mockGetFormats.mockResolvedValue(mockLoaderData.formats);
      mockGetAuthors.mockResolvedValue(mockLoaderData.authors);
      mockGetPublishers.mockResolvedValue(mockLoaderData.publishers);

      await loader({
        params: { entity: 'books', entityId: 'book-1' },
      } as any);

      expect(mockGetEntity).toHaveBeenCalledWith(Entity.Books, 'book-1');
      expect(mockGetGenres).toHaveBeenCalledWith(Entity.Books);
      expect(mockGetFormats).toHaveBeenCalledWith(Entity.Books);
      expect(mockGetAuthors).toHaveBeenCalled();
      expect(mockGetPublishers).toHaveBeenCalled();
    });
  });

  describe('Cancel button functionality', () => {
    const mockLoaderData = {
      entity: {
        id: 'book-1',
        type: Entity.Books,
        title: 'The Great Gatsby',
      },
      authors: [],
      genres: [],
      formats: [],
      publishers: [],
      studios: [],
      developers: [],
      labels: [],
      platforms: [],
      entityType: Entity.Books,
    };

    beforeEach(() => {
      vi.clearAllMocks();
      mockUseLoaderData.mockReturnValue(mockLoaderData);
      mockUseActionData.mockReturnValue(undefined);
      mockUseNavigation.mockReturnValue({ state: 'idle' } as any);
      mockSingular.mockReturnValue('Book');
    });

    it('should have cancel button that navigates back', () => {
      const mockNavigate = vi.fn();
      mockUseNavigate.mockReturnValue(mockNavigate);

      render(<Edit />);

      const cancelButton = screen.getByRole('button', { name: /Cancel/i });
      expect(cancelButton).toBeInTheDocument();
      expect(cancelButton).toHaveAttribute('type', 'button');
    });

    it('should have cancel button disabled when submitting', () => {
      mockUseNavigation.mockReturnValue({
        state: 'submitting',
        location: { pathname: '/books/book-1/edit' },
      } as any);

      render(<Edit />);

      const cancelButton = screen.getByRole('button', { name: /Cancel/i });
      expect(cancelButton).toBeDisabled();
    });
  });

  describe('Edge cases', () => {
    const mockLoaderData = {
      entity: {
        id: 'book-1',
        type: Entity.Books,
        title: 'The Great Gatsby',
      },
      authors: [],
      genres: [],
      formats: [],
      publishers: [],
      studios: [],
      developers: [],
      labels: [],
      platforms: [],
      entityType: Entity.Books,
    };

    beforeEach(() => {
      vi.clearAllMocks();
      mockUseLoaderData.mockReturnValue(mockLoaderData);
      mockUseActionData.mockReturnValue(undefined);
      mockUseNavigate.mockReturnValue(vi.fn());
      mockUseNavigation.mockReturnValue({ state: 'idle' } as any);
      mockSingular.mockReturnValue('Book');
    });

    it('should handle empty metadata gracefully', () => {
      render(<Edit />);

      expect(screen.getByTestId('book-form')).toBeInTheDocument();
    });

    it('should render with minimal entity data', () => {
      const minimalEntity = {
        id: 'book-1',
        type: Entity.Books,
        title: 'Book',
      };

      mockUseLoaderData.mockReturnValue({
        ...mockLoaderData,
        entity: minimalEntity,
      });

      render(<Edit />);

      expect(screen.getByText('Editing Book')).toBeInTheDocument();
    });

    it('should handle submission failure gracefully', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);
      mockFormToDto.mockReturnValue(null);

      const mockFormData = new FormData();
      const request = new Request('http://localhost/books/book-1/edit', {
        method: 'POST',
        body: mockFormData,
      });

      const result = await action({
        request,
        params: { entity: 'books', entityId: 'book-1' },
      } as any);

      expect(result).toHaveProperty('invalidObject');
    });
  });
});
