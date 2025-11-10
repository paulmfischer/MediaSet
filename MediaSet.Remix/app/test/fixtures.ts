/**
 * Test data fixtures for MediaSet entities
 * These provide consistent, realistic test data for use across test suites
 */

// Book fixtures
export const mockBook = {
  id: 'book-1',
  title: 'The Great Gatsby',
  author: 'F. Scott Fitzgerald',
  isbn: '978-0743273565',
  releaseDate: '1925-04-10',
  language: 'English',
  genre: 'Fiction',
  rating: 4.5,
  notes: 'A classic American novel',
};

export const mockBooks = [mockBook];

// Game fixtures
export const mockGame = {
  id: 'game-1',
  title: 'The Legend of Zelda: Breath of the Wild',
  developer: 'Nintendo',
  platform: 'Nintendo Switch',
  releaseDate: '2017-03-03',
  genre: 'Action-Adventure',
  rating: 4.9,
  notes: 'Open-world adventure game',
};

export const mockGames = [mockGame];

// Movie fixtures
export const mockMovie = {
  id: 'movie-1',
  title: 'The Shawshank Redemption',
  director: 'Frank Darabont',
  releaseDate: '1994-10-14',
  runtime: 142,
  genre: 'Drama',
  rating: 4.9,
  notes: 'A masterpiece of cinema',
};

export const mockMovies = [mockMovie];

// Music fixtures
export const mockMusic = {
  id: 'music-1',
  title: 'Abbey Road',
  artist: 'The Beatles',
  releaseDate: '1969-09-26',
  genre: 'Rock',
  format: 'CD',
  rating: 4.8,
  notes: 'One of the greatest albums ever',
};

export const mockMusicCollection = [mockMusic];

/**
 * Mock API responses for common endpoints
 */
export const mockApiResponses = {
  // List endpoints
  books: { data: mockBooks, total: 1 },
  games: { data: mockGames, total: 1 },
  movies: { data: mockMovies, total: 1 },
  music: { data: mockMusicCollection, total: 1 },

  // Single item endpoints
  book: mockBook,
  game: mockGame,
  movie: mockMovie,
  musicItem: mockMusic,
};

/**
 * Create mock statistics data
 */
export function createMockStats() {
  return {
    books: {
      total: 42,
      avgRating: 4.2,
      languages: ['English', 'Spanish', 'French'],
    },
    games: {
      total: 28,
      avgRating: 4.1,
      platforms: ['PlayStation 5', 'Xbox Series X', 'Nintendo Switch'],
    },
    movies: {
      total: 156,
      avgRating: 4.3,
      genres: ['Drama', 'Action', 'Comedy', 'Horror'],
    },
    music: {
      total: 87,
      avgRating: 4.0,
      genres: ['Rock', 'Pop', 'Jazz', 'Classical'],
    },
  };
}
