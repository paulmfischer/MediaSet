import { baseUrl } from "./constants.server";
import { singular } from "./helpers";
import { BookEntity, Entity, MovieEntity } from "./models";

type Link = {
  name: string;
  url: string;
};

type BookLookupResponse = {
  title: string;
  subtitle: string;
  authors: Array<Link>;
  numberOfPages: number;
  publishers: Array<Omit<Link, 'url'>>;
  publishDate: string;
  subjects: Array<Link>;
  format?: string;
};

type MovieLookupResponse = {
  title: string;
  studios: Array<string>;
  genres: Array<string>;
  releaseDate: string;
  rating: string;
  runtime?: number;
  plot: string;
  format?: string;
  isTvSeries: boolean;
};

export type LookupError = {
  error: { notFound: string }
};

const linkMap = (link: Link) => link.name;

export async function lookup(entityType: Entity, identifierType: string, identifierValue: string): Promise<BookEntity | MovieEntity | LookupError> {
  const entityTypeStr = entityType.toLowerCase();
  const response = await fetch(`${baseUrl}/lookup/${entityTypeStr}/${identifierType}/${identifierValue}`);
  
  if (response.status === 404) {
    return { error: { notFound: `No ${singular(entityType)} found for ${identifierType.toUpperCase()} ${identifierValue}` } };
  }

  if (entityType === Entity.Books) {
    const bookLookup = await response.json() as BookLookupResponse;
    return {
      type: Entity.Books,
      authors: bookLookup.authors?.map(linkMap),
      pages: bookLookup.numberOfPages,
      isbn: identifierValue,
      publicationDate: bookLookup.publishDate,
      publisher: bookLookup.publishers.map(pub => pub.name)[0],
      title: bookLookup.title,
      subtitle: bookLookup.subtitle,
      genres: bookLookup.subjects?.map(linkMap),
      format: bookLookup.format
    } as BookEntity;
  }

  if (entityType === Entity.Movies) {
    const movieLookup = await response.json() as MovieLookupResponse;
    return {
      type: Entity.Movies,
      title: movieLookup.title,
      barcode: identifierValue,
      releaseDate: movieLookup.releaseDate,
      rating: movieLookup.rating,
      runtime: movieLookup.runtime,
      studios: movieLookup.studios,
      genres: movieLookup.genres,
      plot: movieLookup.plot,
      format: movieLookup.format,
      isTvSeries: movieLookup.isTvSeries
    } as MovieEntity;
  }

  return { error: { notFound: `Unsupported entity type: ${entityType}` } };
}

// Backward compatibility wrapper for book lookups
export async function lookupBook(barcode: string): Promise<BookEntity | LookupError> {
  return lookup(Entity.Books, "isbn", barcode) as Promise<BookEntity | LookupError>;
}

// New movie lookup function
export async function lookupMovie(barcode: string): Promise<MovieEntity | LookupError> {
  return lookup(Entity.Movies, "upc", barcode) as Promise<MovieEntity | LookupError>;
}