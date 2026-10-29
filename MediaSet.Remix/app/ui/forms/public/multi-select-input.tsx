import { clientEntry, on, ref, type Handle } from 'remix/component';

import type { Option } from '../../../models.ts';
import { Icon } from '../../icon.tsx';
import { X } from '../../icons.ts';
import { filterOptions } from './select-options.ts';

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
    let filterText = '';
    let open = false;
    let activeIndex = 0;
    let input: HTMLInputElement;

    const listboxId = () => `${handle.props.name}-listbox`;
    const optionId = (index: number) => `${handle.props.name}-option-${index}`;
    const visible = () => filterOptions(handle.props.options, filterText, handle.props.addLabel);

    const render = () => {
      handle.update();
      // Keep the keyboard-active option in view once it is in the DOM.
      handle.queueTask(() => document.getElementById(optionId(activeIndex))?.scrollIntoView({ block: 'nearest' }));
    };

    const setOpen = (value: boolean) => {
      if (open === value) return;
      open = value;
      if (!open) filterText = '';
      activeIndex = 0;
      render();
    };

    const toggle = (option: Option) => {
      selected = selected.includes(option.value)
        ? selected.filter((value) => value !== option.value)
        : [...selected, option.value];
      filterText = '';
      activeIndex = 0;
      render();
    };

    const remove = (value: string) => {
      selected = selected.filter((current) => current !== value);
      render();
      input.focus();
    };

    const onKeyDown = (event: KeyboardEvent) => {
      const options = visible();
      if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
        event.preventDefault();
        if (!open) return setOpen(true);
        const step = event.key === 'ArrowDown' ? 1 : -1;
        activeIndex = Math.min(Math.max(activeIndex + step, 0), Math.max(options.length - 1, 0));
        render();
      } else if (event.key === 'Enter') {
        // Never submit the form from the filter box.
        event.preventDefault();
        if (!open) return setOpen(true);
        const option = options[activeIndex];
        if (option) toggle(option);
      } else if (event.key === 'Escape') {
        if (open) {
          event.preventDefault();
          setOpen(false);
        }
      } else if (event.key === 'Tab') {
        setOpen(false);
      } else if (event.key === 'Backspace' && filterText === '' && selected.length > 0) {
        selected = selected.slice(0, -1);
        render();
      }
    };

    return () => {
      const { name, placeholder, invalid, describedBy } = handle.props;
      const options = visible();
      return (
        <div
          class="relative"
          mix={[
            // Close when focus leaves the control and its list.
            on('focusout', (event) => {
              if (!(event.currentTarget as Element).contains(event.relatedTarget as Node | null)) setOpen(false);
            }),
          ]}
        >
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
              value={filterText}
              placeholder={placeholder}
              autocomplete="off"
              role="combobox"
              aria-expanded={open ? 'true' : 'false'}
              aria-controls={listboxId()}
              aria-autocomplete="list"
              aria-activedescendant={open && options.length > 0 ? optionId(activeIndex) : undefined}
              aria-invalid={invalid ? 'true' : undefined}
              aria-describedby={describedBy}
              mix={[
                ref((node) => (input = node as HTMLInputElement)),
                on('focus', () => setOpen(true)),
                on('input', (event) => {
                  filterText = (event.currentTarget as HTMLInputElement).value;
                  activeIndex = 0;
                  open = true;
                  render();
                }),
                on('keydown', (event) => onKeyDown(event)),
              ]}
            />
            {selected.map((value) => (
              <input key={value} type="hidden" name={name} value={value} />
            ))}
          </div>
          {open ? (
            <div
              id={listboxId()}
              role="listbox"
              aria-multiselectable="true"
              class="absolute left-0 right-0 top-full mt-1 py-2 z-30 rounded-md max-h-80 overflow-auto bg-gray-700 border border-gray-600 shadow-lg"
            >
              {options.map((option, index) => (
                <div
                  key={`${option.isNew ? 'new-' : ''}${option.value}`}
                  id={optionId(index)}
                  role="option"
                  aria-selected={selected.includes(option.value) ? 'true' : 'false'}
                  class={`px-3 py-2 text-white cursor-pointer hover:bg-gray-600 ${
                    selected.includes(option.value) || index === activeIndex ? 'bg-gray-600' : ''
                  }`}
                  mix={[
                    on('mouseenter', () => {
                      activeIndex = index;
                      handle.update();
                    }),
                    // mousedown, with the default prevented, keeps focus in the filter input.
                    on('mousedown', (event) => {
                      event.preventDefault();
                      toggle(option);
                    }),
                  ]}
                >
                  {option.label}
                </div>
              ))}
              {options.length === 0 ? <div class="px-3 py-2 text-gray-400">No options</div> : null}
            </div>
          ) : null}
        </div>
      );
    };
  }
);
