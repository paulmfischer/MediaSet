import { baseUrl } from '~/constants.server';
import { singular } from '~/utils/helpers';
import {
  BookEntity,
  MovieEntity,
  GameEntity,
  MusicEntity,
  Entity,
  IdentifierType,
  BookLookupResponse,
  MovieLookupResponse,
  GameLookupResponse,
  MusicLookupResponse,
  LookupError,
} from '~/models';

type Link = {
  name: string;
  url: string;
};

const linkMap = (link: Link) => link.name;

export function isLookupError(result: unknown): result is LookupError {
  return (
    typeof result === 'object' &&
    result !== null &&
    typeof (result as LookupError).message === 'string' &&
    typeof (result as LookupError).statusCode === 'number'
  );
}

export function getIdentifierTypeForField(entityType: Entity, fieldName: string): IdentifierType {
  if (fieldName === 'title') {
    return 'entity';
  }
  if (entityType === Entity.Books && fieldName === 'isbn') {
    return 'isbn';
  }
  if (
    (entityType === Entity.Movies || entityType === Entity.Games || entityType === Entity.Musics) &&
    fieldName === 'barcode'
  ) {
    return 'upc';
  }
  return 'isbn';
}

const VALID_IDENTIFIER_TYPES = new Set<string>(['isbn', 'lccn', 'oclc', 'olid', 'upc', 'ean', 'entity']);

export async function lookup(
  entityType: Entity,
  identifierType: IdentifierType,
  searchParams: Record<string, string>
): Promise<Array<BookEntity | MovieEntity | GameEntity | MusicEntity> | LookupError> {
  if (!VALID_IDENTIFIER_TYPES.has(identifierType)) {
    return { message: `Invalid identifier type: ${identifierType}`, statusCode: 400 } as LookupError;
  }

  let url: string;
  if (identifierType === 'entity') {
    const qs = new URLSearchParams(searchParams).toString();
    url = `${baseUrl}/lookup/${entityType}/entity?${qs}`;
  } else {
    const identifierValue = searchParams[identifierType] ?? Object.values(searchParams)[0] ?? '';
    url = `${baseUrl}/lookup/${entityType}/${identifierType}/${encodeURIComponent(identifierValue)}`;
  }

  const response = await fetch(url);

  if (!response.ok) {
    const errorText = await response.text();
    return {
      message: errorText || `Failed to lookup ${singular(entityType)}`,
      statusCode: response.status,
    } as LookupError;
  }

  const isEntitySearch = identifierType === 'entity';

  if (entityType === Entity.Books) {
    const bookLookups = (await response.json()) as BookLookupResponse[];
    return bookLookups.map(
      (bookLookup) =>
        ({
          type: Entity.Books,
          authors: bookLookup.authors?.map(linkMap),
          pages: bookLookup.numberOfPages,
          isbn: isEntitySearch ? undefined : searchParams['isbn'],
          publicationDate: bookLookup.publishDate,
          publisher: bookLookup.publishers?.[0]?.name,
          title: bookLookup.title,
          subtitle: bookLookup.subtitle,
          genres: bookLookup.subjects?.map(linkMap),
          format: bookLookup.format,
          imageUrl: bookLookup.imageUrl,
        }) as BookEntity
    );
  } else if (entityType === Entity.Movies) {
    const movieLookups = (await response.json()) as MovieLookupResponse[];
    return movieLookups.map(
      (movieLookup) =>
        ({
          type: Entity.Movies,
          title: movieLookup.title,
          genres: movieLookup.genres,
          studios: movieLookup.studios,
          releaseDate: movieLookup.releaseDate,
          rating: movieLookup.rating,
          runtime: movieLookup.runtime ?? undefined,
          plot: movieLookup.plot,
          barcode: isEntitySearch ? undefined : (searchParams['upc'] ?? searchParams['ean'] ?? searchParams['barcode']),
          format: movieLookup.format,
          imageUrl: movieLookup.imageUrl,
        }) as MovieEntity
    );
  } else if (entityType === Entity.Games) {
    const gameLookups = (await response.json()) as GameLookupResponse[];
    return gameLookups.map(
      (gameLookup) =>
        ({
          type: Entity.Games,
          title: gameLookup.title,
          platform: gameLookup.platform,
          genres: gameLookup.genres,
          developers: gameLookup.developers,
          publishers: gameLookup.publishers,
          releaseDate: gameLookup.releaseDate,
          rating: gameLookup.rating,
          description: gameLookup.description,
          barcode: isEntitySearch ? undefined : (searchParams['upc'] ?? searchParams['ean'] ?? searchParams['barcode']),
          format: gameLookup.format,
          imageUrl: gameLookup.imageUrl,
        }) as GameEntity
    );
  } else if (entityType === Entity.Musics) {
    const musicLookups = (await response.json()) as MusicLookupResponse[];
    return musicLookups.map(
      (musicLookup) =>
        ({
          type: Entity.Musics,
          title: musicLookup.title,
          artist: musicLookup.artist,
          releaseDate: musicLookup.releaseDate,
          genres: musicLookup.genres,
          duration: musicLookup.duration ?? undefined,
          label: musicLookup.label,
          tracks: musicLookup.tracks ?? undefined,
          discs: musicLookup.discs ?? undefined,
          discList: musicLookup.discList,
          barcode: isEntitySearch ? undefined : (searchParams['upc'] ?? searchParams['ean'] ?? searchParams['barcode']),
          format: musicLookup.format,
          imageUrl: musicLookup.imageUrl,
        }) as MusicEntity
    );
  }

  return {
    message: `Unsupported entity type: ${entityType}`,
    statusCode: 400,
  } as LookupError;
}
