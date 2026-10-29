import { clientEntry, on, ref, type Handle } from 'remix/component';

import type { Option } from '../../../models.ts';
import { Icon } from '../../icon.tsx';
import { X } from '../../icons.ts';
import { filterOptions } from './select-options.ts';

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
    let filterText = '';
    let open = false;
    let activeIndex = 0;
    let input: HTMLInputElement;

    const listboxId = () => `${handle.props.name}-listbox`;
    const optionId = (index: number) => `${handle.props.name}-option-${index}`;
    const visible = () => filterOptions(handle.props.options, filterText, handle.props.addLabel);

    const render = () => {
      handle.update();
      handle.queueTask(() => document.getElementById(optionId(activeIndex))?.scrollIntoView({ block: 'nearest' }));
    };

    const setOpen = (value: boolean) => {
      if (open === value) return;
      open = value;
      if (!open) filterText = '';
      activeIndex = 0;
      render();
    };

    const choose = (option: Option) => {
      selected = option.value;
      filterText = '';
      open = false;
      render();
    };

    const clear = () => {
      selected = '';
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
        event.preventDefault();
        if (!open) return setOpen(true);
        const option = options[activeIndex];
        if (option) choose(option);
      } else if (event.key === 'Escape') {
        if (open) {
          event.preventDefault();
          setOpen(false);
        }
      } else if (event.key === 'Tab') {
        setOpen(false);
      } else if (event.key === 'Backspace' && filterText === '' && selected) {
        selected = '';
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
            on('focusout', (event) => {
              if (!(event.currentTarget as Element).contains(event.relatedTarget as Node | null)) setOpen(false);
            }),
          ]}
        >
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
              value={filterText}
              placeholder={selected ? '' : placeholder}
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
            <input type="hidden" name={name} value={selected} />
          </div>
          {open ? (
            <div
              id={listboxId()}
              role="listbox"
              class="absolute left-0 right-0 top-full mt-1 py-2 z-30 rounded-md max-h-80 overflow-auto bg-gray-700 border border-gray-600 shadow-lg"
            >
              {options.map((option, index) => (
                <div
                  key={`${option.isNew ? 'new-' : ''}${option.value}`}
                  id={optionId(index)}
                  role="option"
                  aria-selected={option.value === selected ? 'true' : 'false'}
                  class={`px-3 py-2 text-white cursor-pointer hover:bg-gray-600 ${
                    option.value === selected || index === activeIndex ? 'bg-gray-600' : ''
                  }`}
                  mix={[
                    on('mouseenter', () => {
                      activeIndex = index;
                      handle.update();
                    }),
                    on('mousedown', (event) => {
                      event.preventDefault();
                      choose(option);
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
