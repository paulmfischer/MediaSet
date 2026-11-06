import { clientEntry, on, type Handle } from 'remix/component';

import type { Option } from '../../../models.ts';
import { Icon } from '../../icon.tsx';
import { X } from '../../icons.ts';
import { createCombobox } from './combobox.tsx';

interface SingleSelectInputProps {
  /** Form field name; a hidden input carries the selected value (empty when none). */
  name: string;
  placeholder: string;
  addLabel: string;
  options: Option[];
  selectedValue: string;
  invalid?: boolean;
  describedBy?: string | undefined;
}

// Hydrated single-select: filter, pick or add a new value, clear with the chip or Backspace.
export const SingleSelectInput = clientEntry(
  import.meta.url,
  function SingleSelectInput(handle: Handle<SingleSelectInputProps>) {
    let selected = handle.props.selectedValue;

    const combobox = createCombobox(handle, {
      name: () => handle.props.name,
      options: () => handle.props.options,
      addLabel: () => handle.props.addLabel,
      selected: () => (selected ? [selected] : []),
      isSelected: (option) => option.value === selected,
      pick: (option) => {
        selected = option.value;
      },
      removeLast: () => {
        if (!selected) return false;
        selected = '';
        return true;
      },
      closeOnPick: true,
      multiple: false,
    });

    const clear = () => {
      selected = '';
      combobox.render();
      combobox.focusInput();
    };

    return () => {
      const { name, placeholder, invalid, describedBy } = handle.props;
      return (
        <div class="relative" mix={combobox.rootMix()}>
          <div class="flex flex-wrap gap-2 bg-gray-800 border border-gray-600 p-2 rounded-md">
            {selected ? (
              <button
                type="button"
                class="inline-flex items-center gap-1 px-2 py-1 rounded-full text-sm bg-blue-600 text-white hover:bg-blue-700"
                aria-label={`Clear ${selected}`}
                mix={on('click', clear)}
              >
                {selected}
                <Icon icon={X} size={16} />
              </button>
            ) : null}
            <input
              id={name}
              type="text"
              class="flex-1 min-w-32 pl-1 p-0 outline-none bg-transparent text-white placeholder-gray-400"
              placeholder={selected ? '' : placeholder}
              aria-invalid={invalid ? 'true' : undefined}
              aria-describedby={describedBy}
              {...combobox.inputProps()}
              mix={combobox.inputMix()}
            />
            <input type="hidden" name={name} value={selected} />
          </div>
          {combobox.listbox()}
        </div>
      );
    };
  }
);
