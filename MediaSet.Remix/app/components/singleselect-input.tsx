import { useEffect, useMemo, useRef, useState } from 'react';
import type { KeyboardEvent as ReactKeyboardEvent } from 'react';
import { Option } from '~/models';

type SingleselectProps = {
  name: string;
  addLabel: string;
  placeholder: string;
  options: Option[];
  selectedValue?: string;
};

function initializeSelected(selectedValue?: string): Option | null {
  if (selectedValue) {
    return { label: selectedValue, value: selectedValue };
  }
  return null;
}

export default function SingleselectInput(props: SingleselectProps) {
  const [selected, setSelected] = useState<Option | null>(() => initializeSelected(props.selectedValue));
  const [filterText, setFilterText] = useState('');
  const [displayOptions, setDisplayOptions] = useState(false);
  const [activeIndex, setActiveIndex] = useState(0);

  // Use a ref instead of querying the DOM on every render
  const containerRef = useRef<HTMLDivElement | null>(null);
  const inputRef = useRef<HTMLInputElement | null>(null);
  const optionRefs = useRef<Array<HTMLDivElement | null>>([]);

  // Re-sync selected when props.selectedValue changes (e.g., lookup)
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setSelected(initializeSelected(props.selectedValue));
  }, [props.selectedValue]);

  const selectOption = (option: Option) => {
    const next: Option = option.isNew
      ? { label: option.value, value: option.value }
      : { label: option.label, value: option.value };
    setSelected(next);
    setDisplayOptions(false);
    setFilterText('');
  };

  const clearSelection = () => {
    setSelected(null);
    setFilterText('');
    inputRef.current?.focus();
  };

  // Derive filtered options instead of storing in state
  const filteredOptions = useMemo(() => {
    const base =
      filterText.trim().length > 0
        ? props.options.filter((op) => op.label.toLowerCase().includes(filterText.toLowerCase()))
        : props.options;

    const addNew =
      filterText.trim().length > 0
        ? [{ label: `${props.addLabel} ${filterText}`, value: filterText, isNew: true } as Option]
        : [];

    return [...base, ...addNew];
  }, [filterText, props.options, props.addLabel]);

  // Compute menu position only when open, and update on resize/scroll
  const [menuPos, setMenuPos] = useState<{ left: number; top: number; width: number } | null>(null);
  useEffect(() => {
    if (!displayOptions) return;

    const update = () => {
      const el = containerRef.current;
      if (!el) return;
      const rect = el.getBoundingClientRect();
      setMenuPos({ left: rect.left, top: rect.bottom + 5, width: rect.width });
    };

    update();
    window.addEventListener('resize', update);
    // capture scroll on ancestors too
    window.addEventListener('scroll', update, true);
    return () => {
      window.removeEventListener('resize', update);
      window.removeEventListener('scroll', update, true);
    };
  }, [displayOptions]);

  // Clear the filter when closing the menu
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (!displayOptions) setFilterText('');
  }, [displayOptions]);

  // Keyboard handler moved out of JSX for readability
  const handleKeyDown = (e: ReactKeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      if (!displayOptions) setDisplayOptions(true);
      setActiveIndex((idx) => Math.min(idx + 1, Math.max(0, filteredOptions.length - 1)));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      if (!displayOptions) setDisplayOptions(true);
      setActiveIndex((idx) => Math.max(0, idx - 1));
    } else if (e.key === 'Enter') {
      if (!displayOptions) {
        setDisplayOptions(true);
        return;
      }
      e.preventDefault();
      const option = filteredOptions[activeIndex];
      if (option) {
        selectOption(option);
      }
    } else if (e.key === 'Escape') {
      e.preventDefault();
      setDisplayOptions(false);
    } else if (e.key === 'Tab') {
      // When tabbing away, close the menu so the backdrop/dropdown doesn't linger
      setDisplayOptions(false);
    } else if (e.key === 'Backspace' && filterText === '' && selected) {
      // Clear selection when input is empty
      clearSelection();
    }
  };

  // Close the menu when focus moves outside of the component/menu (handles tabbing out)
  const handleBlur = () => {
    // Defer check so document.activeElement reflects the newly focused element
    setTimeout(() => {
      const active = document.activeElement as HTMLElement | null;
      const container = containerRef.current;
      const menuEl = document.getElementById(`${props.name}-listbox`);
      if (!container) return;
      // If the newly focused element is inside our container or the menu, keep open
      if (active && (container.contains(active) || menuEl?.contains(active))) return;
      setDisplayOptions(false);
    }, 0);
  };

  // Reset active index when opening or when the filtered list changes
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (displayOptions) setActiveIndex(0);
  }, [displayOptions, filteredOptions.length]);

  // Keep the active option in view when navigating with the keyboard
  useEffect(() => {
    if (!displayOptions) return;
    const el = optionRefs.current[activeIndex];
    el?.scrollIntoView({ block: 'nearest' });
  }, [activeIndex, displayOptions]);

  // Determine what to display in the input; when opening with a value, keep showing it until user types
  const displayValue = filterText !== '' ? filterText : (selected?.label ?? '');

  return (
    <>
      {/* click-away overlay */}
      <button
        type="button"
        aria-label="Close dropdown"
        className={`absolute top-0 left-0 z-10 w-full h-full ${displayOptions ? '' : 'hidden'}`}
        onMouseDown={() => setDisplayOptions(false)}
      />

      <div className="flex flex-col">
        <div
          ref={containerRef}
          className="w-full flex items-center z-20 bg-gray-800 border border-gray-600 px-3 py-2 rounded-md text-white shadow-sm focus-within:ring-2 focus-within:ring-blue-400 focus-within:border-blue-400"
          id={`single-select-input-${props.name}`}
        >
          <input
            type="text"
            className="flex-1 min-w-0 outline-none bg-transparent text-white placeholder-gray-400 p-0"
            value={displayValue}
            placeholder={props.placeholder}
            onFocus={() => setDisplayOptions(true)}
            onBlur={handleBlur}
            onChange={(event) => setFilterText(event.target.value)}
            ref={inputRef}
            role="combobox"
            aria-expanded={displayOptions}
            aria-controls={`${props.name}-listbox`}
            aria-autocomplete="list"
            aria-activedescendant={
              displayOptions && filteredOptions.length > 0 ? `${props.name}-option-${activeIndex}` : undefined
            }
            onKeyDown={handleKeyDown}
          />
          <input type="hidden" name={props.name} value={selected?.value ?? ''} />
        </div>

        <div
          className={`fixed py-2 z-30 rounded-md max-h-80 min-w-80 overflow-scroll bg-gray-700 border border-gray-600 shadow-lg ${
            displayOptions ? '' : 'hidden'
          }`}
          style={{
            top: menuPos?.top ?? 0,
            left: menuPos?.left ?? 0,
            width: menuPos?.width ?? undefined,
          }}
          role="listbox"
          id={`${props.name}-listbox`}
        >
          {filteredOptions.map((option, idx) => {
            const activeFlag = idx === activeIndex;
            return (
              <div
                key={`${option.isNew ? 'new-' : ''}${option.value}`}
                id={`${props.name}-option-${idx}`}
                ref={(el) => (optionRefs.current[idx] = el)}
                role="option"
                aria-selected={activeFlag}
                tabIndex={0}
                onMouseEnter={() => setActiveIndex(idx)}
                onMouseDown={(e) => {
                  e.preventDefault();
                  selectOption(option);
                }}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault();
                    selectOption(option);
                  }
                }}
                className={`px-3 py-2 text-white cursor-pointer hover:bg-gray-600 ${activeFlag ? 'bg-gray-600' : ''}`}
              >
                {option.label}
              </div>
            );
          })}
        </div>
      </div>
    </>
  );
}
