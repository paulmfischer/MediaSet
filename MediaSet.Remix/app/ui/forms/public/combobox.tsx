import { on, ref, type Handle } from 'remix/component';

import type { Option } from '../../../models.ts';
import { filterOptions } from './select-options.ts';

export interface ComboboxConfig {
  /** Form field name; also the input `id` and the prefix of the listbox and option ids. */
  name: () => string;
  options: () => Option[];
  addLabel: () => string;
  /** Currently selected values, so a selected value is never offered again as "add new". */
  selected: () => string[];
  isSelected: (option: Option) => boolean;
  /** Choose (single) or toggle (multiple) an option. */
  pick: (option: Option) => void;
  /** Backspace in an empty filter box; returns whether a value was removed. */
  removeLast: () => boolean;
  /** Single-select closes the list after a pick; multi-select keeps it open. */
  closeOnPick: boolean;
  multiple: boolean;
}

// Filter box + dropdown behaviour shared by the single- and multi-select islands: opening, the
// filtered/"add new" options, keyboard navigation and the listbox markup.
export function createCombobox<P>(handle: Handle<P>, config: ComboboxConfig) {
  let open = false;
  let filterText = '';
  let activeIndex = 0;
  // Whether the user moved through the list with the arrow keys; gates Enter selecting an option.
  let navigated = false;
  let input: HTMLInputElement | null = null;

  const options = () => filterOptions(config.options(), filterText, config.addLabel(), config.selected());
  const listboxId = () => `${config.name()}-listbox`;
  const optionId = (index: number) => `${config.name()}-option-${index}`;

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
    navigated = false;
    render();
  };

  const pick = (option: Option) => {
    config.pick(option);
    filterText = '';
    activeIndex = 0;
    navigated = false;
    if (config.closeOnPick) open = false;
    render();
  };

  const onKeyDown = (event: KeyboardEvent) => {
    const visible = options();
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      if (!open) return setOpen(true);
      const step = event.key === 'ArrowDown' ? 1 : -1;
      activeIndex = Math.min(Math.max(activeIndex + step, 0), Math.max(visible.length - 1, 0));
      navigated = true;
      render();
    } else if (event.key === 'Enter') {
      // With nothing typed or navigated to, Enter is not a selection: let it submit the form.
      if (!open || (filterText === '' && !navigated)) return setOpen(false);
      event.preventDefault();
      const option = visible[activeIndex];
      if (option) pick(option);
    } else if (event.key === 'Escape') {
      if (open) {
        event.preventDefault();
        setOpen(false);
      }
    } else if (event.key === 'Tab') {
      setOpen(false);
    } else if (event.key === 'Backspace' && filterText === '' && config.removeLast()) {
      render();
    }
  };

  return {
    options,
    listboxId,
    optionId,
    render,
    focusInput: () => input?.focus(),
    get open() {
      return open;
    },
    get activeIndex() {
      return activeIndex;
    },
    /** Mixes for the wrapper: close when focus leaves the control and its list. */
    rootMix: () => [
      on<HTMLDivElement, 'focusout'>('focusout', (event) => {
        if (!event.currentTarget.contains(event.relatedTarget as Node | null)) setOpen(false);
      }),
    ],
    /** Mixes for the filter input. A click reopens the list after a pick or Escape left focus in place. */
    inputMix: () => [
      ref((node) => (input = node as HTMLInputElement)),
      on<HTMLInputElement, 'focus'>('focus', () => setOpen(true)),
      on<HTMLInputElement, 'click'>('click', () => setOpen(true)),
      on<HTMLInputElement, 'input'>('input', (event) => {
        filterText = event.currentTarget.value;
        activeIndex = 0;
        navigated = false;
        open = true;
        render();
      }),
      on<HTMLInputElement, 'keydown'>('keydown', (event) => onKeyDown(event)),
    ],
    inputProps: () => {
      const visible = options();
      return {
        value: filterText,
        autocomplete: 'off',
        role: 'combobox',
        'aria-expanded': open ? 'true' : 'false',
        'aria-controls': listboxId(),
        'aria-autocomplete': 'list',
        'aria-activedescendant': open && visible.length > 0 ? optionId(activeIndex) : undefined,
      } as const;
    },
    listbox: () => {
      if (!open) return null;
      const visible = options();
      return (
        <div
          id={listboxId()}
          role="listbox"
          aria-multiselectable={config.multiple ? 'true' : undefined}
          class="absolute left-0 right-0 top-full mt-1 py-2 z-30 rounded-md max-h-80 overflow-auto bg-gray-700 border border-gray-600 shadow-lg"
        >
          {visible.map((option, index) => (
            <div
              key={`${option.isNew ? 'new-' : ''}${option.value}`}
              id={optionId(index)}
              role="option"
              aria-selected={config.isSelected(option) ? 'true' : 'false'}
              class={`px-3 py-2 text-white cursor-pointer hover:bg-gray-600 ${
                config.isSelected(option) || index === activeIndex ? 'bg-gray-600' : ''
              }`}
              mix={[
                on('mouseenter', () => {
                  activeIndex = index;
                  handle.update();
                }),
                // mousedown, with the default prevented, keeps focus in the filter input.
                on('mousedown', (event) => {
                  event.preventDefault();
                  pick(option);
                }),
              ]}
            >
              {option.label}
            </div>
          ))}
          {visible.length === 0 ? <div class="px-3 py-2 text-gray-400">No options</div> : null}
        </div>
      );
    },
  };
}
