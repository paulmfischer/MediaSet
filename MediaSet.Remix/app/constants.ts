// export const baseUrl = 'http://localhost:7130';
export const baseUrl = process.env.apiUrl;

export const entities = {
  books: "Books",
  movies: "Movies",
};

export enum Entities {
  Books = "Books",
  Movies = "Movies",
};