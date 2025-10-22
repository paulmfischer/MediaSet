import { Params } from "@remix-run/react";
import { BaseEntity, Book, BookEntity, Entity, Game, GameEntity, Movie, MovieEntity, Music, MusicEntity, Disc } from "./models";

export function toTitleCase(str: string | undefined) {
  if (str == undefined) {
    return '';
  }

  return str.replace(/\w\S*/g, (text: string) => text.charAt(0).toUpperCase() + text.substring(1).toLowerCase());
}

export function getEntityFromParams(params: Params<string>) {
  return Entity[toTitleCase(params.entity) as keyof typeof Entity];
}

export function singular(entityType: Entity) {
  return entityType.slice(0, entityType.length - 1);
}

function formDataToType<T>(formData: FormData): T {
  const data: Record<string, FormDataEntryValue> = {};
  for (let [key, value] of formData) {
    data[key] = value;
  }
  return data as T;
}

function getValue(val: any): any | undefined {
  return val == '' ? undefined : val;
}

function baseToBookEntity(data: BaseEntity): BookEntity {
  const book = data as Book;
  return {
    type: book.type,
    authors: book.authors ? book.authors.split(',') : undefined,
    format: getValue(book.format),
    genres: book.genres ? book.genres.split(',') : undefined,
    id: getValue(book.id),
    isbn: getValue(book.isbn),
    pages: getValue(book.pages),
    plot: getValue(book.plot),
    publicationDate: getValue(book.publicationDate),
    publisher: getValue(book.publisher),
    subtitle: getValue(book.subtitle),
    title: getValue(book.title),
  };
}

function baseToMovieEntity(data: BaseEntity): MovieEntity {
  const movie = data as Movie;
  const isTvSeries = (movie.isTvSeries as unknown) as string;
  return {
    type: movie.type,
    studios: movie.studios ? movie.studios.split(',') : undefined,
    format: getValue(movie.format),
    genres: movie.genres ? movie.genres.split(',') : undefined,
    id: getValue(movie.id),
    barcode: getValue(movie.barcode),
    releaseDate: getValue(movie.releaseDate),
    plot: getValue(movie.plot),
    runtime: getValue(movie.runtime),
    title: getValue(movie.title),
    isTvSeries: getValue(isTvSeries) ? true : false,
  };
}

function baseToGameEntity(data: BaseEntity): GameEntity {
  const game = data as Game;
  return {
    type: game.type,
    developers: game.developers ? game.developers.split(',') : undefined,
    publishers: game.publishers ? game.publishers.split(',') : undefined,
    format: getValue(game.format),
    genres: game.genres ? game.genres.split(',') : undefined,
    id: getValue(game.id),
    barcode: getValue(game.barcode),
    releaseDate: getValue(game.releaseDate),
    rating: getValue(game.rating),
    platform: getValue(game.platform),
    description: getValue(game.description),
    title: getValue(game.title),
  };
}

function baseToMusicEntity(data: BaseEntity): MusicEntity {
  const music = data as Music;
  
  // Parse disc list from form data
  const discList: Disc[] = [];
  let i = 0;
  while (true) {
    const trackNumberKey = `discList[${i}].trackNumber` as keyof Music;
    const titleKey = `discList[${i}].title` as keyof Music;
    const durationKey = `discList[${i}].duration` as keyof Music;
    
    const trackNumber = (music as any)[trackNumberKey];
    const title = (music as any)[titleKey];
    const duration = (music as any)[durationKey];
    
    if (trackNumber === undefined && title === undefined && duration === undefined) {
      break;
    }
    
    if (title || duration) {
      discList.push({
        trackNumber: parseInt(trackNumber) || i + 1,
        title: title || '',
        duration: duration || '',
      });
    }
    i++;
  }
  
  return {
    type: music.type,
    id: getValue(music.id),
    title: getValue(music.title),
    format: getValue(music.format),
    artist: getValue(music.artist),
    releaseDate: getValue(music.releaseDate),
    genres: music.genres ? music.genres.split(',') : undefined,
    duration: getValue(music.duration),
    label: getValue(music.label),
    barcode: getValue(music.barcode),
    tracks: getValue(music.tracks),
    discs: getValue(music.discs),
    discList: discList.length > 0 ? discList : undefined,
  };
}

export function formToDto(formData: FormData): BaseEntity | null {
  const data = formDataToType<BaseEntity>(formData);
  if (data.type === Entity.Books) {
    return baseToBookEntity(data);
  } else if (data.type === Entity.Movies) {
    return baseToMovieEntity(data);
  } else if (data.type === Entity.Games) {
    return baseToGameEntity(data);
  } else if (data.type === Entity.Musics) {
    return baseToMusicEntity(data);
  } else {
    return null;
  }
}