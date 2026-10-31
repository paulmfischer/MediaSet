import type { InferOutput } from 'remix/data-schema';
import * as f from 'remix/data-schema/form-data';

import { Entity, type MovieEntity } from '../../../models.ts';
import {
  checkbox,
  optionalHttpUrl,
  optionalWholeNumber,
  requiredText,
  text,
  textList,
} from '../../forms/form-schema.ts';

export const movieForm = f.object({
  title: f.field(requiredText('Title is required')),
  barcode: f.field(text()),
  format: f.field(text()),
  runtime: f.field(optionalWholeNumber('Runtime must be a whole number from 0 to 999999')),
  releaseDate: f.field(text()),
  isTvSeries: f.field(checkbox()),
  studios: textList('studios'),
  genres: textList('genres'),
  plot: f.field(text()),
  imageUrl: f.field(optionalHttpUrl('Enter a valid http or https image URL')),
});

// `isTvSeries` is a checkbox, so it is read separately from the text fields.
export const MOVIE_FIELDS = {
  single: ['title', 'barcode', 'format', 'runtime', 'releaseDate', 'isTvSeries', 'plot', 'imageUrl'],
  repeated: ['studios', 'genres'],
} as const;

export type MovieFormValues = {
  title: string;
  barcode: string;
  format: string;
  runtime: string;
  releaseDate: string;
  /** `'true'` when checked, so it travels like the other submitted text. */
  isTvSeries: string;
  plot: string;
  imageUrl: string;
  studios: string[];
  genres: string[];
};

export const emptyMovieValues: MovieFormValues = {
  title: '',
  barcode: '',
  format: '',
  runtime: '',
  releaseDate: '',
  isTvSeries: '',
  plot: '',
  imageUrl: '',
  studios: [],
  genres: [],
};

type ParsedMovie = InferOutput<typeof movieForm>;

/** Builds the API payload from validated form output; empty optional text is left out. */
export function toMovieEntity(parsed: ParsedMovie): MovieEntity {
  const { runtime, isTvSeries, studios, genres, ...strings } = parsed;
  const present = Object.fromEntries(Object.entries(strings).filter(([, value]) => value !== ''));
  return {
    type: Entity.Movies,
    ...present,
    ...(runtime === undefined ? {} : { runtime }),
    isTvSeries,
    studios,
    genres,
  } as MovieEntity;
}

/** Form values for an existing movie, as the edit form shows them. */
export function toMovieValues(movie: MovieEntity): MovieFormValues {
  return {
    title: movie.title ?? '',
    barcode: movie.barcode ?? '',
    format: movie.format ?? '',
    runtime: movie.runtime === undefined ? '' : String(movie.runtime),
    releaseDate: movie.releaseDate ?? '',
    isTvSeries: movie.isTvSeries ? 'true' : '',
    plot: movie.plot ?? '',
    imageUrl: movie.imageUrl ?? '',
    studios: movie.studios ?? [],
    genres: movie.genres ?? [],
  };
}

/**
 * The API payload for an edit: the existing movie with the form's fields applied; cleared fields are
 * dropped, and `removeImage` drops the saved poster.
 */
export function toMovieUpdate(existing: MovieEntity, parsed: ParsedMovie, removeImage = false): MovieEntity {
  const { runtime, isTvSeries, studios, genres, ...strings } = parsed;
  const updated: Record<string, unknown> = { ...existing, isTvSeries, studios, genres };
  for (const [name, value] of Object.entries(strings)) {
    if (value === '') delete updated[name];
    else updated[name] = value;
  }
  if (runtime === undefined) delete updated.runtime;
  else updated.runtime = runtime;
  if (removeImage) {
    // The API deletes the stored image when the update carries none; a newly entered URL still replaces it.
    delete updated.coverImage;
    if (updated.imageUrl === existing.imageUrl) delete updated.imageUrl;
  }
  return updated as unknown as MovieEntity;
}
