import type { InferOutput } from 'remix/data-schema';
import * as f from 'remix/data-schema/form-data';

import { Entity, type BookEntity } from '../../../models.ts';
import { optionalWholeNumber, requiredText, text, textList } from '../../forms/form-schema.ts';

export const bookForm = f.object({
  title: f.field(requiredText('Title is required')),
  isbn: f.field(text()),
  subtitle: f.field(text()),
  format: f.field(text()),
  pages: f.field(optionalWholeNumber('Pages must be a whole number, 0 or more')),
  publicationDate: f.field(text()),
  authors: textList('authors'),
  genres: textList('genres'),
  publisher: f.field(text()),
  plot: f.field(text()),
});

export const BOOK_FIELDS = {
  single: ['title', 'isbn', 'subtitle', 'format', 'pages', 'publicationDate', 'publisher', 'plot'],
  repeated: ['authors', 'genres'],
} as const;

export type BookFormValues = {
  title: string;
  isbn: string;
  subtitle: string;
  format: string;
  pages: string;
  publicationDate: string;
  publisher: string;
  plot: string;
  authors: string[];
  genres: string[];
};

export const emptyBookValues: BookFormValues = {
  title: '',
  isbn: '',
  subtitle: '',
  format: '',
  pages: '',
  publicationDate: '',
  publisher: '',
  plot: '',
  authors: [],
  genres: [],
};

/** Builds the API payload from validated form output; empty optional text is left out. */
export function toBookEntity(parsed: InferOutput<typeof bookForm>): BookEntity {
  const { pages, authors, genres, ...strings } = parsed;
  const present = Object.fromEntries(Object.entries(strings).filter(([, value]) => value !== ''));
  return { type: Entity.Books, ...present, ...(pages === undefined ? {} : { pages }), authors, genres } as BookEntity;
}
