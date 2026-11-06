import type { InferOutput } from 'remix/data-schema';
import * as f from 'remix/data-schema/form-data';

import { Entity, type GameEntity } from '../../../models.ts';
import { blankToUndefined, optionalHttpUrl, requiredText, text, textList } from '../../forms/form-schema.ts';

export const gameForm = f.object({
  title: f.field(requiredText('Title is required')),
  barcode: f.field(text()),
  format: f.field(text()),
  platform: f.field(text()),
  releaseDate: f.field(text()),
  rating: f.field(text()),
  developers: textList('developers'),
  publishers: textList('publishers'),
  genres: textList('genres'),
  description: f.field(text()),
  imageUrl: f.field(optionalHttpUrl('Enter a valid http or https image URL')),
});

export const GAME_FIELDS = {
  single: ['title', 'barcode', 'format', 'platform', 'releaseDate', 'rating', 'description', 'imageUrl'],
  repeated: ['developers', 'publishers', 'genres'],
} as const;

export type GameFormValues = {
  title: string;
  barcode: string;
  format: string;
  platform: string;
  releaseDate: string;
  rating: string;
  description: string;
  imageUrl: string;
  developers: string[];
  publishers: string[];
  genres: string[];
};

export const emptyGameValues: GameFormValues = {
  title: '',
  barcode: '',
  format: '',
  platform: '',
  releaseDate: '',
  rating: '',
  description: '',
  imageUrl: '',
  developers: [],
  publishers: [],
  genres: [],
};

type ParsedGame = InferOutput<typeof gameForm>;

/** Builds the API payload from validated form output; empty optional text is left out. */
export function toGameEntity(parsed: ParsedGame): GameEntity {
  return {
    type: Entity.Games,
    title: parsed.title,
    barcode: blankToUndefined(parsed.barcode),
    format: blankToUndefined(parsed.format),
    platform: blankToUndefined(parsed.platform),
    releaseDate: blankToUndefined(parsed.releaseDate),
    rating: blankToUndefined(parsed.rating),
    description: blankToUndefined(parsed.description),
    imageUrl: blankToUndefined(parsed.imageUrl),
    developers: parsed.developers,
    publishers: parsed.publishers,
    genres: parsed.genres,
  };
}

/** Form values for an existing game, as the edit form shows them. */
export function toGameValues(game: GameEntity): GameFormValues {
  return {
    title: game.title ?? '',
    barcode: game.barcode ?? '',
    format: game.format ?? '',
    platform: game.platform ?? '',
    releaseDate: game.releaseDate ?? '',
    rating: game.rating ?? '',
    description: game.description ?? '',
    imageUrl: game.imageUrl ?? '',
    developers: game.developers ?? [],
    publishers: game.publishers ?? [],
    genres: game.genres ?? [],
  };
}

/**
 * The API payload for an edit: the existing game with the form's fields applied; cleared fields are
 * dropped, and `removeImage` drops the saved cover.
 */
export function toGameUpdate(existing: GameEntity, parsed: ParsedGame, removeImage = false): GameEntity {
  // The API deletes the stored image when the update carries none; a newly entered URL still replaces it.
  const entity = toGameEntity(parsed);
  return {
    ...existing,
    ...entity,
    coverImage: removeImage ? undefined : existing.coverImage,
    imageUrl: removeImage && entity.imageUrl === existing.imageUrl ? undefined : entity.imageUrl,
  };
}
