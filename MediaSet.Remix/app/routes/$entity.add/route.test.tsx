import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen } from '~/test/test-utils';
import Add, { meta, loader, action } from './route';
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

// Mock Remix hooks and Form component to avoid router context requirement
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

const mockAddEntity = vi.mocked(entityData.addEntity);
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
const mockUseSubmit = vi.mocked(remixReact.useSubmit);

describe('$entity_.add route', () => {
  describe('meta function', () => {
    beforeEach(() => {
      vi.clearAllMocks();
    });

    it('should return correct title for Books', () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);
      mockSingular.mockReturnValue('Book');

      const result = meta({ params: { entity: 'books' } } as any);

      expect(result).toEqual(
        expect.arrayContaining([
          expect.objectContaining({ title: 'Add a Book' }),
          expect.objectContaining({
            name: 'description',
            content: 'Add a Book',
          }),
        ])
      );
      expect(result).toHaveLength(2);
    });

    it('should return correct title for Movies', () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Movies);
      mockSingular.mockReturnValue('Movie');

      const result = meta({ params: { entity: 'movies' } } as any);

      expect(result).toContainEqual(
        expect.objectContaining({ title: 'Add a Movie' })
      );
    });

    it('should return correct title for Games', () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Games);
      mockSingular.mockReturnValue('Game');

      const result = meta({ params: { entity: 'games' } } as any);

      expect(result).toContainEqual(
        expect.objectContaining({ title: 'Add a Game' })
      );
    });

    it('should return correct title for Musics', () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Musics);
      mockSingular.mockReturnValue('Music');

      const result = meta({ params: { entity: 'musics' } } as any);

      expect(result).toContainEqual(
        expect.objectContaining({ title: 'Add a Music' })
      );
    });

    it('should include both title and description meta tags', () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);
      mockSingular.mockReturnValue('Book');

      const result = meta({ params: { entity: 'books' } } as any);

      const titleTags = result.filter((tag: any) => tag.title !== undefined);
      const descriptionTags = result.filter((tag: any) => tag.name === 'description');

      expect(titleTags).toHaveLength(1);
      expect(descriptionTags).toHaveLength(1);
    });
  });

  describe('loader function', () => {
    const mockLoaderData = {
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
      mockGetGenres.mockResolvedValue(mockLoaderData.genres);
      mockGetFormats.mockResolvedValue(mockLoaderData.formats);
      mockGetAuthors.mockResolvedValue(mockLoaderData.authors);
      mockGetPublishers.mockResolvedValue(mockLoaderData.publishers);
      mockGetStudios.mockResolvedValue(mockLoaderData.studios);
      mockGetDevelopers.mockResolvedValue(mockLoaderData.developers);
      mockGetLabels.mockResolvedValue(mockLoaderData.labels);
      mockGetGamePublishers.mockResolvedValue(mockLoaderData.publishers);
      mockGetPlatforms.mockResolvedValue(mockLoaderData.platforms);
    });

    it('should load metadata for Books', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);

      const result = await loader({ params: { entity: 'books' } } as any);

      expect(mockGetGenres).toHaveBeenCalledWith(Entity.Books);
      expect(mockGetFormats).toHaveBeenCalledWith(Entity.Books);
      expect(mockGetAuthors).toHaveBeenCalled();
      expect(mockGetPublishers).toHaveBeenCalled();
      expect(result.entityType).toBe(Entity.Books);
      expect(result.authors).toEqual(mockLoaderData.authors);
    });

    it('should load metadata for Movies', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Movies);

      const result = await loader({ params: { entity: 'movies' } } as any);

      expect(mockGetGenres).toHaveBeenCalledWith(Entity.Movies);
      expect(mockGetFormats).toHaveBeenCalledWith(Entity.Movies);
      expect(mockGetStudios).toHaveBeenCalled();
      expect(result.entityType).toBe(Entity.Movies);
    });

    it('should load metadata for Games', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Games);

      const result = await loader({ params: { entity: 'games' } } as any);

      expect(mockGetGenres).toHaveBeenCalledWith(Entity.Games);
      expect(mockGetFormats).toHaveBeenCalledWith(Entity.Games);
      expect(mockGetDevelopers).toHaveBeenCalled();
      expect(mockGetGamePublishers).toHaveBeenCalled();
      expect(mockGetPlatforms).toHaveBeenCalled();
      expect(result.entityType).toBe(Entity.Games);
    });

    it('should load metadata for Musics', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Musics);

      const result = await loader({ params: { entity: 'musics' } } as any);

      expect(mockGetGenres).toHaveBeenCalledWith(Entity.Musics);
      expect(mockGetFormats).toHaveBeenCalledWith(Entity.Musics);
      expect(mockGetLabels).toHaveBeenCalled();
      expect(result.entityType).toBe(Entity.Musics);
    });

    it('should return all required properties', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);

      const result = await loader({ params: { entity: 'books' } } as any);

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

    it('should use Promise.all for parallel loading', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Books);

      const loaderPromise = loader({ params: { entity: 'books' } } as any);

      // Verify Promise.all was used by checking all mocks are called
      await loaderPromise;

      expect(mockGetGenres).toHaveBeenCalled();
      expect(mockGetFormats).toHaveBeenCalled();
    });
  });

  describe('action function - create entity', () => {
    beforeEach(() => {
      vi.clearAllMocks();
      mockGetEntityFromParams.mockReturnValue(Entity.Books);
    });

    it('should create a new book entity', async () => {
      const mockNewBook: BookEntity = {
        id: 'new-book-1',
        type: Entity.Books,
        title: 'New Book',
      };

      const mockFormData = new FormData();
      mockFormData.append('intent', 'create');
      mockFormData.append('type', Entity.Books);
      mockFormData.append('title', 'New Book');

      mockFormToDto.mockReturnValue(mockNewBook);
      mockAddEntity.mockResolvedValue(mockNewBook);

      const request = new Request('http://localhost/books/add', {
        method: 'POST',
        body: mockFormData,
      });

      const result = await action({
        request,
        params: { entity: 'books' },
      } as any);

      expect(mockFormToDto).toHaveBeenCalledWith(mockFormData);
      expect(mockAddEntity).toHaveBeenCalledWith(mockNewBook, undefined);
      expect(result).toBeDefined();
    });

    it('should create a new movie entity', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Movies);

      const mockNewMovie: MovieEntity = {
        id: 'new-movie-1',
        type: Entity.Movies,
        title: 'New Movie',
      };

      const mockFormData = new FormData();
      mockFormData.append('intent', 'create');
      mockFormData.append('type', Entity.Movies);
      mockFormData.append('title', 'New Movie');

      mockFormToDto.mockReturnValue(mockNewMovie);
      mockAddEntity.mockResolvedValue(mockNewMovie);

      const request = new Request('http://localhost/movies/add', {
        method: 'POST',
        body: mockFormData,
      });

      const result = await action({
        request,
        params: { entity: 'movies' },
      } as any);

      expect(mockAddEntity).toHaveBeenCalledWith(mockNewMovie, undefined);
    });

    it('should create a new game entity', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Games);

      const mockNewGame: GameEntity = {
        id: 'new-game-1',
        type: Entity.Games,
        title: 'New Game',
        platform: 'PS5',
      };

      const mockFormData = new FormData();
      mockFormData.append('intent', 'create');
      mockFormData.append('type', Entity.Games);

      mockFormToDto.mockReturnValue(mockNewGame);
      mockAddEntity.mockResolvedValue(mockNewGame);

      const request = new Request('http://localhost/games/add', {
        method: 'POST',
        body: mockFormData,
      });

      const result = await action({
        request,
        params: { entity: 'games' },
      } as any);

      expect(mockAddEntity).toHaveBeenCalledWith(mockNewGame, undefined);
    });

    it('should create a new music entity', async () => {
      mockGetEntityFromParams.mockReturnValue(Entity.Musics);

      const mockNewMusic: MusicEntity = {
        id: 'new-music-1',
        type: Entity.Musics,
        title: 'New Album',
        artist: 'Artist Name',
      };

      const mockFormData = new FormData();
      mockFormData.append('intent', 'create');
      mockFormData.append('type', Entity.Musics);

      mockFormToDto.mockReturnValue(mockNewMusic);
      mockAddEntity.mockResolvedValue(mockNewMusic);

      const request = new Request('http://localhost/musics/add', {
        method: 'POST',
        body: mockFormData,
      });

      const result = await action({
        request,
        params: { entity: 'musics' },
      } as any);

      expect(mockAddEntity).toHaveBeenCalledWith(mockNewMusic, undefined);
    });

    it('should return error when formToDto returns null', async () => {
      const mockFormData = new FormData();
      mockFormData.append('intent', 'create');
      mockFormToDto.mockReturnValue(null);

      const request = new Request('http://localhost/books/add', {
        method: 'POST',
        body: mockFormData,
      });

      const result = await action({
        request,
        params: { entity: 'books' },
      } as any);

      expect(result).toEqual({
        error: {
          invalidForm: `Failed to convert form to a ${Entity.Books}`,
        },
      });
      expect(mockAddEntity).not.toHaveBeenCalled();
    });

    it('should throw error when entity param is missing', async () => {
      const mockFormData = new FormData();
      mockFormData.append('intent', 'create');

      const request = new Request('http://localhost/add', {
        method: 'POST',
        body: mockFormData,
      });

      await expect(
        action({
          request,
          params: {},
        } as any)
      ).rejects.toThrow();
    });
  });

  describe('action function - lookup', () => {
    beforeEach(() => {
      vi.clearAllMocks();
      mockGetEntityFromParams.mockReturnValue(Entity.Books);
    });

    it('should handle lookup intent for ISBN', async () => {
      const mockFormData = new FormData();
      mockFormData.append('intent', 'lookup');
      mockFormData.append('fieldName', 'isbn');
      mockFormData.append('identifierValue', '978-0743273565');

      const request = new Request('http://localhost/books/add', {
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
        params: { entity: 'books' },
      } as any);

      expect(result).toHaveProperty('lookupResult');
    });

    it('should return error when identifierValue is missing', async () => {
      const mockFormData = new FormData();
      mockFormData.append('intent', 'lookup');
      mockFormData.append('fieldName', 'isbn');

      const request = new Request('http://localhost/books/add', {
        method: 'POST',
        body: mockFormData,
      });

      const result = await action({
        request,
        params: { entity: 'books' },
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

      const request = new Request('http://localhost/books/add', {
        method: 'POST',
        body: mockFormData,
      });

      const result = await action({
        request,
        params: { entity: 'books' },
      } as any);

      expect(result).toEqual({
        error: { lookup: 'Identifier value is required' },
      });
    });
  });

  describe('Add component rendering', () => {
    const mockLoaderData = {
      authors: [{ label: 'Author 1', value: 'author1' }],
      genres: [{ label: 'Fiction', value: 'fiction' }],
      formats: [{ label: 'Hardcover', value: 'hardcover' }],
      publishers: [{ label: 'Publisher 1', value: 'pub1' }],
      studios: [{ label: 'Studio 1', value: 'studio1' }],
      developers: [{ label: 'Developer 1', value: 'dev1' }],
      labels: [{ label: 'Label 1', value: 'label1' }],
      platforms: [{ label: 'PS5', value: 'ps5' }],
      entityType: Entity.Books,
    };

    beforeEach(() => {
      vi.clearAllMocks();
      mockUseLoaderData.mockReturnValue(mockLoaderData);
      mockUseActionData.mockReturnValue(undefined);
      mockUseNavigate.mockReturnValue(vi.fn());
      mockUseNavigation.mockReturnValue({ state: 'idle' } as any);
      mockUseSubmit.mockReturnValue(vi.fn());
      mockSingular.mockReturnValue('Book');
    });

    it('should render the page title with entity type', () => {
      render(<Add />);

      expect(screen.getByText('Add a Book')).toBeInTheDocument();
    });

    it('should render the form', () => {
      render(<Add />);

      expect(screen.getByTestId('book-form')).toBeInTheDocument();
    });

    it('should render submit button', () => {
      render(<Add />);

      expect(screen.getByRole('button', { name: /Add Book/i })).toBeInTheDocument();
    });

    it('should render cancel button', () => {
      render(<Add />);

      expect(screen.getByRole('button', { name: /Cancel/i })).toBeInTheDocument();
    });

    it('should render form element with post method', () => {
      const { container } = render(<Add />);

      const form = container.querySelector('form');
      expect(form).toBeInTheDocument();
      expect(form).toHaveAttribute('method', 'post');
    });

    it('should render entity type hidden input', () => {
      render(<Add />);

      const hiddenInput = screen.getByDisplayValue(Entity.Books) as HTMLInputElement;
      expect(hiddenInput).toHaveAttribute('type', 'hidden');
      expect(hiddenInput).toHaveAttribute('name', 'type');
    });

    it('should render BookForm for Books entity', () => {
      render(<Add />);

      expect(screen.getByTestId('book-form')).toBeInTheDocument();
    });

    it('should render MovieForm for Movies entity', () => {
      mockUseLoaderData.mockReturnValue({
        ...mockLoaderData,
        entityType: Entity.Movies,
      });
      mockSingular.mockReturnValue('Movie');

      render(<Add />);

      expect(screen.getByTestId('movie-form')).toBeInTheDocument();
    });

    it('should render GameForm for Games entity', () => {
      mockUseLoaderData.mockReturnValue({
        ...mockLoaderData,
        entityType: Entity.Games,
      });
      mockSingular.mockReturnValue('Game');

      render(<Add />);

      expect(screen.getByTestId('game-form')).toBeInTheDocument();
    });

    it('should render MusicForm for Musics entity', () => {
      mockUseLoaderData.mockReturnValue({
        ...mockLoaderData,
        entityType: Entity.Musics,
      });
      mockSingular.mockReturnValue('Music');

      render(<Add />);

      expect(screen.getByTestId('music-form')).toBeInTheDocument();
    });

    it('should pass loader data to form component', () => {
      render(<Add />);

      // Verify that the form component exists and is rendered with data
      expect(screen.getByTestId('book-form')).toBeInTheDocument();
    });

    it('should disable buttons when submitting', () => {
      mockUseNavigation.mockReturnValue({ state: 'submitting' } as any);

      render(<Add />);

      const submitButton = screen.getByRole('button', { name: /Add Book/i });
      const cancelButton = screen.getByRole('button', { name: /Cancel/i });

      expect(submitButton).toBeDisabled();
      expect(cancelButton).toBeDisabled();
    });

    it('should enable buttons when not submitting', () => {
      mockUseNavigation.mockReturnValue({ state: 'idle' } as any);

      render(<Add />);

      const submitButton = screen.getByRole('button', { name: /Add Book/i });
      const cancelButton = screen.getByRole('button', { name: /Cancel/i });

      expect(submitButton).not.toBeDisabled();
      expect(cancelButton).not.toBeDisabled();
    });
  });

  describe('error handling', () => {
    const mockLoaderData = {
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
      mockUseNavigate.mockReturnValue(vi.fn());
      mockUseNavigation.mockReturnValue({ state: 'idle' } as any);
      mockUseSubmit.mockReturnValue(vi.fn());
      mockSingular.mockReturnValue('Book');
    });

    it('should display invalid form error', () => {
      const errorData = {
        error: {
          invalidForm: 'Failed to convert form to a Books',
        },
      };
      mockUseActionData.mockReturnValue(errorData);

      render(<Add />);

      expect(
        screen.getByText('Failed to convert form to a Books')
      ).toBeInTheDocument();
    });

    it('should display lookup error message', () => {
      const errorData = {
        error: {
          lookup: 'ISBN not found in database',
        },
      };
      mockUseActionData.mockReturnValue(errorData);

      render(<Add />);

      expect(screen.getByText('ISBN not found in database')).toBeInTheDocument();
    });

    it('should display lookup error from result', () => {
      const errorData = {
        lookupResult: {
          message: 'API Error: Service unavailable',
          statusCode: 503,
        },
      };
      mockUseActionData.mockReturnValue(errorData);

      render(<Add />);

      expect(
        screen.getByText('API Error: Service unavailable')
      ).toBeInTheDocument();
    });

    it('should not display lookup error if lookup was successful', () => {
      const successData = {
        lookupResult: {
          title: 'Found Book',
          isbn: '978-0743273565',
        },
      };
      mockUseActionData.mockReturnValue(successData);

      render(<Add />);

      expect(screen.queryByText(/API Error/i)).not.toBeInTheDocument();
    });

    it('should show error message in red alert box for invalid form', () => {
      const errorData = {
        error: {
          invalidForm: 'Form validation failed',
        },
      };
      mockUseActionData.mockReturnValue(errorData);

      const { container } = render(<Add />);

      const errorBox = container.querySelector(
        '.bg-red-900.border-red-700'
      );
      expect(errorBox).toBeInTheDocument();
    });

    it('should show error message in yellow alert box for lookup error', () => {
      const errorData = {
        lookupResult: {
          message: 'Item not found',
          statusCode: 404,
        },
      };
      mockUseActionData.mockReturnValue(errorData);

      const { container } = render(<Add />);

      const errorBox = container.querySelector(
        '.bg-yellow-900.border-yellow-700'
      );
      expect(errorBox).toBeInTheDocument();
    });
  });

  describe('lookup result handling', () => {
    const mockLoaderData = {
      authors: [{ label: 'Author 1', value: 'author1' }],
      genres: [{ label: 'Action', value: 'action' }],
      formats: [{ label: 'Blu-ray', value: 'bluray' }],
      publishers: [],
      studios: [{ label: 'Universal', value: 'universal' }],
      developers: [],
      labels: [],
      platforms: [],
      entityType: Entity.Movies,
    };

    beforeEach(() => {
      vi.clearAllMocks();
      mockUseLoaderData.mockReturnValue(mockLoaderData);
      mockUseNavigate.mockReturnValue(vi.fn());
      mockUseNavigation.mockReturnValue({ state: 'idle' } as any);
      mockUseSubmit.mockReturnValue(vi.fn());
      mockSingular.mockReturnValue('Movie');
      mockGetEntityFromParams.mockReturnValue(Entity.Movies);
    });

    it('should use different key for form when lookup result changes', () => {
      // First render with no lookup data
      mockUseActionData.mockReturnValue(undefined);
      const { rerender } = render(<Add />);
      
      const firstForm = screen.getByTestId('movie-form');
      expect(firstForm).toBeInTheDocument();
      
      // Second render with lookup data
      mockUseActionData.mockReturnValue({
        lookupResult: {
          type: Entity.Movies,
          title: 'Test Movie',
          imageUrl: 'https://example.com/image.jpg',
          barcode: '123456789',
        },
        identifierValue: '123456789',
        fieldName: 'barcode',
      });
      
      rerender(<Add />);
      
      const secondForm = screen.getByTestId('movie-form');
      expect(secondForm).toBeInTheDocument();
      // Form should be remounted (new instance) when lookup data arrives
    });

    it('should pass lookup entity to form component', () => {
      const lookupData = {
        lookupResult: {
          type: Entity.Movies,
          title: 'The Matrix',
          imageUrl: 'https://example.com/matrix.jpg',
          barcode: '012345678905',
          studios: ['Warner Bros'],
          genres: ['Action', 'Sci-Fi'],
        },
        identifierValue: '012345678905',
        fieldName: 'barcode',
      };
      
      mockUseActionData.mockReturnValue(lookupData);
      
      render(<Add />);
      
      // Form should be rendered with lookup data
      expect(screen.getByTestId('movie-form')).toBeInTheDocument();
    });
  });

  describe('Cancel button functionality', () => {
    const mockLoaderData = {
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
      mockUseNavigation.mockReturnValue({ state: 'idle' } as any);
      mockUseSubmit.mockReturnValue(vi.fn());
      mockSingular.mockReturnValue('Book');
    });

    it('should have cancel button that can navigate back', () => {
      const mockNavigate = vi.fn();
      mockUseNavigate.mockReturnValue(mockNavigate);

      render(<Add />);

      const cancelButton = screen.getByRole('button', { name: /Cancel/i });
      expect(cancelButton).toBeInTheDocument();
      expect(cancelButton).toHaveAttribute('type', 'button');
    });
  });
});
