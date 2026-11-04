import * as s from 'remix/data-schema';
import type { InferOutput } from 'remix/data-schema';
import * as f from 'remix/data-schema/form-data';

import { Entity, type Disc, type MusicEntity } from '../../../models.ts';
import {
  blankToUndefined,
  fieldErrors,
  optionalHttpUrl,
  optionalWholeNumber,
  requiredText,
  submittedValues,
  text,
  textList,
} from '../../forms/form-schema.ts';

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
    'imageUrl',
  ],
  repeated: ['genres'],
} as const;

/** One row of the track editor, as the text the user entered. */
export interface TrackRowValues {
  trackNumber: string;
  title: string;
  minutes: string;
  seconds: string;
}

export const TRACK_ROW_FIELDS = {
  trackNumber: 'trackNumber',
  title: 'trackTitle',
  minutes: 'trackMinutes',
  seconds: 'trackSeconds',
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
  /** The track editor's rows, in the order shown; validated separately by `parseTrackRows`. */
  trackList: TrackRowValues[];
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
  trackList: [],
  imageUrl: '',
  genres: [],
};

type ParsedMusic = InferOutput<typeof musicForm>;

const isBlankRow = (row: TrackRowValues) => Object.values(row).every((value) => value.trim() === '');

/** The track rows as submitted (repeated `trackNumber`, `trackTitle`, `trackMinutes`, `trackSeconds` fields, paired by index). */
export function readTrackRows(formData: FormData): TrackRowValues[] {
  const column = (name: string) => formData.getAll(name).filter((value): value is string => typeof value === 'string');
  const numbers = column(TRACK_ROW_FIELDS.trackNumber);
  const titles = column(TRACK_ROW_FIELDS.title);
  const minutes = column(TRACK_ROW_FIELDS.minutes);
  const seconds = column(TRACK_ROW_FIELDS.seconds);
  const count = Math.max(numbers.length, titles.length, minutes.length, seconds.length);
  return Array.from({ length: count }, (_, index) => ({
    trackNumber: numbers[index] ?? '',
    title: titles[index] ?? '',
    minutes: minutes[index] ?? '',
    seconds: seconds[index] ?? '',
  }));
}

const trackNumberSchema = optionalWholeNumber('Track number must be a whole number from 1 to 999999');
const trackMinutesSchema = optionalWholeNumber('Minutes must be a whole number from 0 to 9999', 4);
const trackSecondsSchema = optionalWholeNumber('Seconds must be a whole number from 0 to 59', 2, 59);

/**
 * Validates the track rows. Fully blank rows are dropped; every other row needs a track number
 * (1 to 999999) and a title, and may have a duration (minutes 0 to 9999, seconds 0 to 59, both blank
 * for none). Errors are keyed `track.<row>.<field>` by the row's 1-based position as submitted.
 */
export function parseTrackRows(rows: readonly TrackRowValues[]): { discs: Disc[]; errors: Record<string, string> } {
  const discs: Disc[] = [];
  const errors: Record<string, string> = {};
  rows.forEach((row, index) => {
    if (isBlankRow(row)) return;
    const key = (field: string) => `track.${index + 1}.${field}`;

    const number = s.parseSafe(trackNumberSchema, row.trackNumber);
    let trackNumber = 0;
    if (!number.success) errors[key('trackNumber')] = number.issues[0]?.message ?? '';
    else if (number.value === undefined) errors[key('trackNumber')] = 'Track number is required';
    else if (number.value < 1) errors[key('trackNumber')] = 'Track number must be a whole number from 1 to 999999';
    else trackNumber = number.value;

    const title = row.title.trim();
    if (title === '') errors[key('trackTitle')] = 'Title is required';

    const minutes = s.parseSafe(trackMinutesSchema, row.minutes);
    const seconds = s.parseSafe(trackSecondsSchema, row.seconds);
    if (!minutes.success) errors[key('trackMinutes')] = minutes.issues[0]?.message ?? '';
    if (!seconds.success) errors[key('trackSeconds')] = seconds.issues[0]?.message ?? '';

    if (Object.keys(errors).some((name) => name.startsWith(`track.${index + 1}.`))) return;
    const duration = toMilliseconds(
      minutes.success ? minutes.value : undefined,
      seconds.success ? seconds.value : undefined
    );
    discs.push({ trackNumber, title, duration: duration ?? null });
  });
  return { discs, errors };
}

/** The values a failed submission or lookup re-renders: the text fields plus the track rows, unchanged. */
export function submittedMusicValues(formData: FormData): MusicFormValues {
  return { ...submittedValues(formData, MUSIC_FIELDS), trackList: readTrackRows(formData) };
}

export type MusicSubmission =
  | { success: true; value: ParsedMusic; discList: Disc[] }
  | { success: false; errors: Record<string, string> };

/** Validates the whole form: the fields by schema and the track rows by `parseTrackRows`. */
export function parseMusicSubmission(formData: FormData): MusicSubmission {
  const parsed = s.parseSafe(musicForm, formData);
  const tracks = parseTrackRows(readTrackRows(formData));
  if (!parsed.success || Object.keys(tracks.errors).length > 0) {
    return { success: false, errors: { ...(parsed.success ? {} : fieldErrors(parsed.issues)), ...tracks.errors } };
  }
  return { success: true, value: parsed.value, discList: tracks.discs };
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
export function toMusicEntity(parsed: ParsedMusic, discList: Disc[] = []): MusicEntity {
  return {
    type: Entity.Musics,
    title: parsed.title,
    artist: parsed.artist,
    barcode: blankToUndefined(parsed.barcode),
    format: blankToUndefined(parsed.format),
    releaseDate: blankToUndefined(parsed.releaseDate),
    label: blankToUndefined(parsed.label),
    tracks: parsed.tracks,
    discs: parsed.discs,
    duration: toMilliseconds(parsed.durationMinutes, parsed.durationSeconds),
    discList: discList.length === 0 ? undefined : discList,
    imageUrl: blankToUndefined(parsed.imageUrl),
    genres: parsed.genres,
  };
}

/** A saved track as an editor row. */
export function toTrackRow(disc: Disc): TrackRowValues {
  const { minutes, seconds } = fromMilliseconds(disc.duration ?? undefined);
  return { trackNumber: String(disc.trackNumber), title: disc.title, minutes, seconds };
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
    trackList: (music.discList ?? []).map(toTrackRow),
    imageUrl: music.imageUrl ?? '',
    genres: music.genres ?? [],
  };
}

/**
 * The API payload for an edit: the existing album with the form's fields applied; cleared fields are
 * dropped, the track list is replaced by the submitted one (none removes it), and `removeImage` drops the saved cover.
 */
export function toMusicUpdate(
  existing: MusicEntity,
  parsed: ParsedMusic,
  discList: Disc[],
  removeImage = false
): MusicEntity {
  const entity = toMusicEntity(parsed, discList);

  // The form shows whole seconds, so an unchanged duration keeps its saved milliseconds.
  const unchangedDuration =
    entity.duration !== undefined &&
    existing.duration !== undefined &&
    Math.floor(existing.duration / 1000) * 1000 === entity.duration;

  // The form shows whole seconds, so an unchanged saved track keeps its milliseconds.
  const keepSaved = (disc: Disc): Disc => {
    const saved = existing.discList?.find(
      (old) =>
        old.trackNumber === disc.trackNumber &&
        old.title === disc.title &&
        old.duration !== null &&
        disc.duration !== null &&
        Math.floor(old.duration / 1000) * 1000 === disc.duration
    );
    return saved ?? disc;
  };

  // The API deletes the stored image when the update carries none; a newly entered URL still replaces it.
  return {
    ...existing,
    ...entity,
    duration: unchangedDuration ? existing.duration : entity.duration,
    discList: entity.discList?.map(keepSaved),
    coverImage: removeImage ? undefined : existing.coverImage,
    imageUrl: removeImage && entity.imageUrl === existing.imageUrl ? undefined : entity.imageUrl,
  };
}
