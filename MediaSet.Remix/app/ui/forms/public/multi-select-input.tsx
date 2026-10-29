import { clientEntry, on, type Handle } from 'remix/component';

import type { Option } from '../../../models.ts';
import { Icon } from '../../icon.tsx';
import { X } from '../../icons.ts';
import { createCombobox } from './combobox.tsx';

interface MultiSelectInputProps {
  /** Form field name; one hidden input per selected value is submitted under it. */
  name: string;
  placeholder: string;
  addLabel: string;
  options: Option[];
  selectedValues: string[];
  invalid?: boolean;
  describedBy?: string | undefined;
}

// Hydrated multi-select: filter, pick or add new values, remove with the chips or Backspace.
export const MultiSelectInput = clientEntry(
  import.meta.url,
  function MultiSelectInput(handle: Handle<MultiSelectInputProps>) {
    let selected = [...handle.props.selectedValues];

    const combobox = createCombobox(handle, {
      name: () => handle.props.name,
      options: () => handle.props.options,
      addLabel: () => handle.props.addLabel,
      selected: () => selected,
      isSelected: (option) => selected.includes(option.value),
      pick: (option) => {
        selected = selected.includes(option.value)
          ? selected.filter((value) => value !== option.value)
          : [...selected, option.value];
      },
      removeLast: () => {
        if (selected.length === 0) return false;
        selected = selected.slice(0, -1);
        return true;
      },
      closeOnPick: false,
      multiple: true,
    });

    const remove = (value: string) => {
      selected = selected.filter((current) => current !== value);
      combobox.render();
      combobox.focusInput();
    };

    return () => {
      const { name, placeholder, invalid, describedBy } = handle.props;
      return (
        <div class="relative" mix={combobox.rootMix()}>
          <div class="flex flex-wrap gap-2 bg-gray-800 border border-gray-600 p-2 rounded-md">
            {selected.map((value) => (
              <button
                key={value}
                type="button"
                class="inline-flex items-center gap-1 px-2 py-1 rounded-full text-sm bg-blue-600 text-white hover:bg-blue-700"
                aria-label={`Remove ${value}`}
                mix={on('click', () => remove(value))}
              >
                {value}
                <Icon icon={X} size={16} />
              </button>
            ))}
            <input
              id={name}
              type="text"
              class="flex-1 min-w-32 pl-1 p-0 outline-none bg-transparent text-white placeholder-gray-400"
              placeholder={placeholder}
              aria-invalid={invalid ? 'true' : undefined}
              aria-describedby={describedBy}
              {...combobox.inputProps()}
              mix={combobox.inputMix()}
            />
            {selected.map((value) => (
              <input key={value} type="hidden" name={name} value={value} />
            ))}
          </div>
          {combobox.listbox()}
        </div>
      );
    };
  }
);
