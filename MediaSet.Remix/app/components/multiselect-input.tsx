import { useEffect, useMemo, useRef, useState } from "react";
import type { KeyboardEvent as ReactKeyboardEvent } from "react";
import Badge from "./badge";
import { X } from "lucide-react";

type Option = {
  label: string;
  value: string;
  isNew?: boolean | undefined;
}

type MultiselectProps = {
  name: string;
  addLabel: string;
  selectText: string;
  options: Option[];
  selectedValues?: string[];
};

function initializeSelected(selectedValues?: string[]): Option[] {
  if (selectedValues?.length) {
    return selectedValues.map(value => ({ label: value, value}))
  }

  return [];
}

export default function MultiselectInput(props: MultiselectProps) {
  const [selected, setSelected] = useState<Option[]>(() => initializeSelected(props.selectedValues));
  const [filterText, setFilterText] = useState('');
  const [displayOptions, setDisplayOptions] = useState(false);
  const [activeIndex, setActiveIndex] = useState(0);

  // Use a ref instead of querying the DOM on every render
  const containerRef = useRef<HTMLDivElement | null>(null);
  const inputRef = useRef<HTMLInputElement | null>(null);
  const optionRefs = useRef<Array<HTMLDivElement | null>>([]);

  // Re-sync selected when props.selectedValues changes (e.g., ISBN lookup)
  useEffect(() => {
    setSelected(initializeSelected(props.selectedValues));
  }, [props.selectedValues]);

  // Fast membership check for selection
  const selectedSet = useMemo(
    () => new Set(selected.map((op) => op.value)),
    [selected]
  );
  const isSelected = (option: Option) => selectedSet.has(option.value);

  const toggleSelected = (option: Option) => {
    if (isSelected(option)) {
      setSelected((prev) => prev.filter((op) => op.value !== option.value));
    } else {
      const next: Option = option.isNew
        ? { label: option.value, value: option.value }
        : { label: option.label, value: option.value };
      setSelected((prev) => [...prev, next]);
    }
  };

  // Derive filtered options instead of storing in state
  const filteredOptions = useMemo(() => {
    const base =
      filterText.trim().length > 0
        ? props.options.filter((op) =>
            op.label.toLowerCase().includes(filterText.toLowerCase())
          )
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
    console.log('displayOptions changed:', displayOptions);
    if (!displayOptions) return;

    const update = () => {
      const el = containerRef.current;
      if (!el) return;
      const rect = el.getBoundingClientRect();
      setMenuPos({ left: rect.left, top: rect.bottom + 5, width: rect.width });
    };

    update();
    window.addEventListener("resize", update);
    // capture scroll on ancestors too
    window.addEventListener("scroll", update, true);
    return () => {
      window.removeEventListener("resize", update);
      window.removeEventListener("scroll", update, true);
    };
  }, [displayOptions]);

  // Clear the filter when closing the menu
  useEffect(() => {
    if (!displayOptions) setFilterText("");
  }, [displayOptions]);

  // Keyboard handler moved out of JSX for readability
  const handleKeyDown = (e: ReactKeyboardEvent<HTMLInputElement>) => {
    if (e.key === "ArrowDown") {
      e.preventDefault();
      if (!displayOptions) setDisplayOptions(true);
      setActiveIndex((idx) => Math.min(idx + 1, Math.max(0, filteredOptions.length - 1)));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      if (!displayOptions) setDisplayOptions(true);
      setActiveIndex((idx) => Math.max(0, idx - 1));
    } else if (e.key === "Enter") {
      if (!displayOptions) {
        setDisplayOptions(true);
        return;
      }
      e.preventDefault();
      const option = filteredOptions[activeIndex];
      if (option) {
        toggleSelected(option);
        // keep list open; focus stays on input
      }
    } else if (e.key === "Escape") {
      e.preventDefault();
      setDisplayOptions(false);
    } else if (e.key === "Backspace" && filterText === "" && selected.length > 0) {
      // Remove last selected when input empty
      setSelected((prev) => prev.slice(0, -1));
    }
  };

  // Reset active index when opening or when the filtered list changes
  useEffect(() => {
    if (displayOptions) setActiveIndex(0);
  }, [displayOptions, filteredOptions.length]);

  // Keep the active option in view when navigating with the keyboard
  useEffect(() => {
    if (!displayOptions) return;
    const el = optionRefs.current[activeIndex];
    el?.scrollIntoView({ block: "nearest" });
  }, [activeIndex, displayOptions]);

  return (
    <>
      {/* click-away overlay */}
      <div
        className={`absolute top-0 left-0 z-10 w-full h-full ${displayOptions ? "" : "hidden"}`}
        onClick={() => setDisplayOptions(false)}
      ></div>

      <div className="flex flex-col gap-2">
        <div
          ref={containerRef}
          className="flex flex-wrap gap-2 z-20 bg-gray-800 border border-gray-600 p-2 rounded-md"
          id={`multi-select-input-${props.name}`}
        >
          {selected.map((sel) => (
            <Badge key={sel.value.replaceAll(" ", "")}>
              <div className="flex gap-2" onClick={() => toggleSelected(sel)}>
                {sel.label}
                <X size={16} />
              </div>
            </Badge>
          ))}

          <input
            type="text"
            className="flex-1 min-w-32 pl-1 p-0 outline-none bg-transparent text-white placeholder-gray-400"
            value={filterText}
            placeholder={props.selectText}
            onFocus={() => setDisplayOptions(true)}
            onChange={(event) => setFilterText(event.target.value)}
            ref={inputRef}
            role="combobox"
            aria-expanded={displayOptions}
            aria-controls={`${props.name}-listbox`}
            aria-autocomplete="list"
            aria-activedescendant={
              displayOptions && filteredOptions.length > 0
                ? `${props.name}-option-${activeIndex}`
                : undefined
            }
            onKeyDown={handleKeyDown}
          />
          <input type="hidden" name={props.name} value={selected.map((op) => op.value).join(",")} />
        </div>

        <div
          className={`fixed py-2 z-30 rounded-md max-h-80 min-w-80 overflow-scroll bg-gray-700 border border-gray-600 shadow-lg ${
            displayOptions ? "" : "hidden"
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
            const selectedFlag = isSelected(option);
            const activeFlag = idx === activeIndex;
            return (
              <div
                key={`${option.isNew ? "new-" : ""}${option.value}`}
                id={`${props.name}-option-${idx}`}
                ref={(el) => (optionRefs.current[idx] = el)}
                role="option"
                aria-selected={selectedFlag}
                onMouseEnter={() => setActiveIndex(idx)}
                onClick={() => {
                  toggleSelected(option);
                  // Keep menu open for multiple selections; refocus input for continued typing
                  inputRef.current?.focus();
                }}
                className={`px-3 py-2 text-white cursor-pointer hover:bg-gray-600 ${
                  selectedFlag ? "bg-gray-600" : ""
                } ${activeFlag ? "bg-gray-600" : ""}`}
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