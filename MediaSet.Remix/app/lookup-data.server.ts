import { baseUrl } from "./constants.server";
import { singular } from "./helpers";
import { 
  BookEntity, 
  MovieEntity, 
  Entity, 
  IdentifierType, 
  BookLookupResponse,
  MovieLookupResponse,
  LookupError
} from "./models";

type Link = {
  name: string;
  url: string;
};

const linkMap = (link: Link) => link.name;

export function isLookupError(result: any): result is LookupError {
  return result && typeof result.message === 'string' && typeof result.statusCode === 'number';
}

export function getIdentifierTypeForField(entityType: Entity, fieldName: string): IdentifierType {
  if (entityType === Entity.Books && fieldName === 'isbn') {
    return 'isbn';
  }
  if ((entityType === Entity.Movies || entityType === Entity.Games || entityType === Entity.Musics) && fieldName === 'barcode') {
    return 'upc';
  }
  return 'isbn';
}

export async function lookup(
  entityType: Entity, 
  identifierType: IdentifierType, 
  identifierValue: string
): Promise<BookEntity | MovieEntity | LookupError> {
  const response = await fetch(`${baseUrl}/lookup/${entityType}/${identifierType}/${identifierValue}`);
  
  if (response.status === 404) {
    return { 
      message: `No ${singular(entityType)} found for ${identifierType.toUpperCase()} ${identifierValue}`,
      statusCode: 404
    } as LookupError;
  }

  if (!response.ok) {
    const errorText = await response.text();
    return {
      message: errorText || `Failed to lookup ${singular(entityType)}`,
      statusCode: response.status
    } as LookupError;
  }

  if (entityType === Entity.Books) {
    const bookLookup = await response.json() as BookLookupResponse;
    return {
      type: Entity.Books,
      authors: bookLookup.authors?.map(linkMap),
      pages: bookLookup.numberOfPages,
      isbn: identifierValue,
      publicationDate: bookLookup.publishDate,
      publisher: bookLookup.publishers?.[0]?.name,
      title: bookLookup.title,
      subtitle: bookLookup.subtitle,
      genres: bookLookup.subjects?.map(linkMap),
      format: bookLookup.format
    } as BookEntity;
  } else if (entityType === Entity.Movies) {
    const movieLookup = await response.json() as MovieLookupResponse;
    return {
      type: Entity.Movies,
      title: movieLookup.title,
      genres: movieLookup.genres,
      studios: movieLookup.studios,
      releaseDate: movieLookup.releaseDate,
      rating: movieLookup.rating,
      runtime: movieLookup.runtime ?? undefined,
      plot: movieLookup.plot,
      barcode: identifierValue,
      format: movieLookup.format
    } as MovieEntity;
  }

  return {
    message: `Unsupported entity type: ${entityType}`,
    statusCode: 400
  } as LookupError;
}
