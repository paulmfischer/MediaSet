import type { InferOutput } from 'remix/data-schema';
import * as f from 'remix/data-schema/form-data';

import { Entity, type Disc, type MusicEntity } from '../../../models.ts';
import { optionalHttpUrl, optionalWholeNumber, requiredText, text, textList } from '../../forms/form-schema.ts';

export const musicForm = f.object({
  title: f.field(requiredText('Title is required')),
  artist: f.field(requiredText('Artist is required')),
  barcode: f.field(text()),
  format: f.field(text()),
  releaseDate: f.field(text()),
  label: f.field(text()),
  tracks: f.field(optionalWholeNumber('Tracks must be a whole number from 0 to 999999')),
  discs: f.field(optionalWholeNumber('Discs must be a whole number from 0 to 999999')),
  // The duration is entered as minutes and seconds and stored in milliseconds.
  durationMinutes: f.field(optionalWholeNumber('Minutes must be a whole number from 0 to 9999', 4)),
  durationSeconds: f.field(optionalWholeNumber('Seconds must be a whole number from 0 to 59', 2, 59)),
  // The track list is not editable; a lookup fills it and it travels as JSON in a hidden field.
  discList: f.field(text()),
  genres: textList('genres'),
  imageUrl: f.field(optionalHttpUrl('Enter a valid http or https image URL')),
});

export const MUSIC_FIELDS = {
  single: [
    'title',
    'artist',
    'barcode',
    'format',
    'releaseDate',
    'label',
    'tracks',
    'discs',
    'durationMinutes',
    'durationSeconds',
    'discList',
    'imageUrl',
  ],
  repeated: ['genres'],
} as const;

export type MusicFormValues = {
  title: string;
  artist: string;
  barcode: string;
  format: string;
  releaseDate: string;
  label: string;
  tracks: string;
  discs: string;
  durationMinutes: string;
  durationSeconds: string;
  /** The looked-up track list as JSON; empty when there is none. Not editable. */
  discList: string;
  imageUrl: string;
  genres: string[];
};

export const emptyMusicValues: MusicFormValues = {
  title: '',
  artist: '',
  barcode: '',
  format: '',
  releaseDate: '',
  label: '',
  tracks: '',
  discs: '',
  durationMinutes: '',
  durationSeconds: '',
  discList: '',
  imageUrl: '',
  genres: [],
};

type ParsedMusic = InferOutput<typeof musicForm>;

/** The track list from its hidden-field JSON; anything that is not a list of tracks is ignored. */
export function parseDiscList(value: string): Disc[] | undefined {
  if (value === '') return undefined;
  try {
    const parsed: unknown = JSON.parse(value);
    if (!Array.isArray(parsed)) return undefined;
    const discs: Disc[] = [];
    for (const item of parsed) {
      if (typeof item !== 'object' || item === null) return undefined;
      const { trackNumber, title, duration } = item as Record<string, unknown>;
      if (typeof trackNumber !== 'number' || typeof title !== 'string') return undefined;
      if (duration !== null && duration !== undefined && typeof duration !== 'number') return undefined;
      discs.push({ trackNumber, title, duration: duration ?? null });
    }
    return discs;
  } catch {
    return undefined;
  }
}

/** Minutes and seconds to milliseconds; `undefined` when both are blank. */
export function toMilliseconds(minutes: number | undefined, seconds: number | undefined): number | undefined {
  if (minutes === undefined && seconds === undefined) return undefined;
  return ((minutes ?? 0) * 60 + (seconds ?? 0)) * 1000;
}

/** Milliseconds to the form's minutes and seconds text (whole seconds); blank when there is no duration. */
export function fromMilliseconds(duration: number | undefined): { minutes: string; seconds: string } {
  if (duration === undefined || duration === null) return { minutes: '', seconds: '' };
  const totalSeconds = Math.floor(duration / 1000);
  return { minutes: String(Math.floor(totalSeconds / 60)), seconds: String(totalSeconds % 60) };
}

/** Builds the API payload from validated form output; empty optional text is left out. */
export function toMusicEntity(parsed: ParsedMusic): MusicEntity {
  const { tracks, discs, durationMinutes, durationSeconds, discList, genres, ...strings } = parsed;
  const present = Object.fromEntries(Object.entries(strings).filter(([, value]) => value !== ''));
  const duration = toMilliseconds(durationMinutes, durationSeconds);
  const disclist = parseDiscList(discList);
  return {
    type: Entity.Musics,
    ...present,
    ...(tracks === undefined ? {} : { tracks }),
    ...(discs === undefined ? {} : { discs }),
    ...(duration === undefined ? {} : { duration }),
    ...(disclist === undefined ? {} : { discList: disclist }),
    genres,
  } as MusicEntity;
}

/** Form values for an existing album, as the edit form shows them. */
export function toMusicValues(music: MusicEntity): MusicFormValues {
  const { minutes, seconds } = fromMilliseconds(music.duration);
  return {
    title: music.title ?? '',
    artist: music.artist ?? '',
    barcode: music.barcode ?? '',
    format: music.format ?? '',
    releaseDate: music.releaseDate ?? '',
    label: music.label ?? '',
    tracks: music.tracks === undefined ? '' : String(music.tracks),
    discs: music.discs === undefined ? '' : String(music.discs),
    durationMinutes: minutes,
    durationSeconds: seconds,
    discList: music.discList && music.discList.length > 0 ? JSON.stringify(music.discList) : '',
    imageUrl: music.imageUrl ?? '',
    genres: music.genres ?? [],
  };
}

/**
 * The API payload for an edit: the existing album with the form's fields applied; cleared fields are
 * dropped, the track list is kept as saved, and `removeImage` drops the saved cover.
 */
export function toMusicUpdate(existing: MusicEntity, parsed: ParsedMusic, removeImage = false): MusicEntity {
  const { tracks, discs, durationMinutes, durationSeconds, genres, ...strings } = parsed;
  const updated: Record<string, unknown> = { ...existing, genres };
  for (const [name, value] of Object.entries(strings)) {
    if (name === 'discList') continue; // not editable: the saved track list stays
    if (value === '') delete updated[name];
    else updated[name] = value;
  }
  if (tracks === undefined) delete updated.tracks;
  else updated.tracks = tracks;
  if (discs === undefined) delete updated.discs;
  else updated.discs = discs;

  const duration = toMilliseconds(durationMinutes, durationSeconds);
  // The form shows whole seconds, so an unchanged duration keeps its saved milliseconds.
  const unchanged =
    duration !== undefined &&
    existing.duration !== undefined &&
    Math.floor(existing.duration / 1000) * 1000 === duration;
  if (duration === undefined) delete updated.duration;
  else if (!unchanged) updated.duration = duration;

  if (removeImage) {
    // The API deletes the stored image when the update carries none; a newly entered URL still replaces it.
    delete updated.coverImage;
    if (updated.imageUrl === existing.imageUrl) delete updated.imageUrl;
  }
  return updated as unknown as MusicEntity;
}
