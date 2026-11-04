import { clientEntry, on, type Handle } from 'remix/component';

import type { TrackRowValues } from '../../../actions/entity/add/music-form.ts';
import { Icon } from '../../icon.tsx';
import { ChevronDown, ChevronUp, X } from '../../icons.ts';
import { inputClasses } from '../field.tsx';

interface TrackListInputProps {
  rows: TrackRowValues[];
  /** The form's errors; a row's are keyed `track.<row>.<field>` by its 1-based position. */
  errors: Record<string, string>;
}

type RowField = 'trackNumber' | 'trackTitle' | 'trackMinutes' | 'trackSeconds';

interface Row {
  /** Stable across moves, so each row keeps its inputs and focus. */
  id: number;
  trackNumber: string;
  trackTitle: string;
  trackMinutes: string;
  trackSeconds: string;
  errors: Partial<Record<RowField, string>>;
}

const ROW_FIELDS: RowField[] = ['trackNumber', 'trackTitle', 'trackMinutes', 'trackSeconds'];

// Hydrated track editor: add, edit, reorder and remove tracks. Every row submits its four fields
// under repeated names (`trackNumber`, `trackTitle`, `trackMinutes`, `trackSeconds`); the server
// pairs them by position, so the saved order is the row order. Moving a row does not renumber it.
export const TrackListInput = clientEntry(
  import.meta.url,
  function TrackListInput(handle: Handle<TrackListInputProps>) {
    let nextId = 1;
    const newRow = (values: Partial<Record<RowField, string>> = {}, errors: Row['errors'] = {}): Row => ({
      id: nextId++,
      trackNumber: values.trackNumber ?? '',
      trackTitle: values.trackTitle ?? '',
      trackMinutes: values.trackMinutes ?? '',
      trackSeconds: values.trackSeconds ?? '',
      errors,
    });

    const fromProps = (): Row[] =>
      handle.props.rows.map((row, index) =>
        newRow(
          { trackNumber: row.trackNumber, trackTitle: row.title, trackMinutes: row.minutes, trackSeconds: row.seconds },
          Object.fromEntries(
            ROW_FIELDS.flatMap((field) => {
              const message = handle.props.errors[`track.${index + 1}.${field}`];
              return message ? [[field, message]] : [];
            })
          )
        )
      );

    let rows: Row[] = fromProps();
    // A page that re-renders from the server (a failed save, a lookup) hands over new rows and errors.
    let seenRows = handle.props.rows;

    const focusLater = (id: string) =>
      handle.queueTask(() => {
        const element = document.getElementById(id);
        if (element instanceof HTMLElement && !(element as HTMLButtonElement).disabled) element.focus();
      });

    const add = () => {
      const last = Number(rows[rows.length - 1]?.trackNumber);
      const row = newRow({ trackNumber: String(Number.isInteger(last) && last > 0 ? last + 1 : rows.length + 1) });
      rows = [...rows, row];
      handle.update();
      focusLater(`track-${row.id}-title`);
    };

    const remove = (index: number) => {
      rows = rows.filter((_, current) => current !== index);
      handle.update();
      focusLater('track-add');
    };

    const move = (index: number, offset: -1 | 1) => {
      const target = index + offset;
      const row = rows[index];
      if (!row || target < 0 || target >= rows.length) return;
      const reordered = [...rows];
      reordered.splice(index, 1);
      reordered.splice(target, 0, row);
      rows = reordered;
      handle.update();
      // Keep the focus on the same direction button, or on the other one when it is now disabled.
      const stillCan = offset === -1 ? target > 0 : target < rows.length - 1;
      focusLater(`track-${row.id}-${stillCan ? (offset === -1 ? 'up' : 'down') : offset === -1 ? 'down' : 'up'}`);
    };

    const edit = (row: Row, field: RowField, value: string) => {
      row[field] = value;
      delete row.errors[field];
      // The inputs follow the state, so a render must not put back an older value.
      handle.update();
    };

    return () => {
      if (handle.props.rows !== seenRows) {
        seenRows = handle.props.rows;
        rows = fromProps();
      }
      const control = (row: Row, field: RowField, index: number, props: Record<string, unknown>, label: string) => {
        const error = row.errors[field];
        const errorElementId = `track-${row.id}-${field}-error`;
        return (
          <div class="flex flex-col gap-1">
            <input
              id={`track-${row.id}-${field === 'trackTitle' ? 'title' : field}`}
              name={field}
              class={inputClasses}
              aria-label={`Track ${index + 1} ${label}`}
              aria-invalid={error ? 'true' : undefined}
              aria-describedby={error ? errorElementId : undefined}
              value={row[field]}
              mix={on('input', (event) => edit(row, field, (event.currentTarget as HTMLInputElement).value))}
              {...props}
            />
            {error ? (
              <p id={errorElementId} class="text-sm text-red-400">
                {error}
              </p>
            ) : null}
          </div>
        );
      };

      return (
        <div class="flex flex-col gap-3">
          {rows.map((row, index) => (
            <div key={row.id} class="flex flex-wrap items-start gap-2" role="group" aria-label={`Track ${index + 1}`}>
              <div class="w-24">
                {control(
                  row,
                  'trackNumber',
                  index,
                  { type: 'number', min: '1', step: '1', placeholder: 'No.' },
                  'number'
                )}
              </div>
              <div class="min-w-48 flex-1">
                {control(row, 'trackTitle', index, { type: 'text', placeholder: 'Title' }, 'title')}
              </div>
              <div class="w-24">
                {control(
                  row,
                  'trackMinutes',
                  index,
                  { type: 'number', min: '0', step: '1', placeholder: 'Min' },
                  'minutes'
                )}
              </div>
              <div class="w-24">
                {control(
                  row,
                  'trackSeconds',
                  index,
                  { type: 'number', min: '0', max: '59', step: '1', placeholder: 'Sec' },
                  'seconds'
                )}
              </div>
              <div class="flex gap-1">
                <button
                  id={`track-${row.id}-up`}
                  type="button"
                  class="secondary"
                  aria-label={`Move track ${index + 1} up`}
                  disabled={index === 0}
                  mix={on('click', () => move(index, -1))}
                >
                  <Icon icon={ChevronUp} size={16} />
                </button>
                <button
                  id={`track-${row.id}-down`}
                  type="button"
                  class="secondary"
                  aria-label={`Move track ${index + 1} down`}
                  disabled={index === rows.length - 1}
                  mix={on('click', () => move(index, 1))}
                >
                  <Icon icon={ChevronDown} size={16} />
                </button>
                <button
                  type="button"
                  class="secondary"
                  aria-label={`Remove track ${index + 1}`}
                  mix={on('click', () => remove(index))}
                >
                  <Icon icon={X} size={16} />
                </button>
              </div>
            </div>
          ))}
          <div>
            <button id="track-add" type="button" class="secondary" mix={on('click', add)}>
              Add track
            </button>
          </div>
        </div>
      );
    };
  }
);
