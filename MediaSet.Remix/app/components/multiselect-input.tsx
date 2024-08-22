import { useEffect, useState } from "react";
import Badge from "./badge";
import { IconX } from "@tabler/icons-react";

type Option = {
  label: string;
  value: string;
  isNew?: boolean | undefined;
}

type MultiselectProps = {
  name: string;
  selectText: string;
  options: Option[];
};

export default function MultiselectInput(props: MultiselectProps) {
  const [selected, setSelected] = useState<Option[]>([]);
  const [filterText, setFilterText] = useState('');
  const [filtered, setFiltered] = useState<Option[]>(props.options);
  const [displayOptions, setDisplayOptions] = useState(false);
  const [inputEl, setInputEl] = useState<HTMLElement | null>(null);

  useEffect(() => {
    setInputEl(document.getElementById(`multi-select-input-${props.name}`));
  });

  useEffect(() => {
    if (filterText == null || filterText == '') {
      setFiltered(props.options);
    }
  }, [filterText, setFiltered]);

  useEffect(() => {
    if (!displayOptions) {
      setFilterText('');
    }
  }, [displayOptions, setFilterText]);

  const isSelected = (option: Option) => selected.find(op => op.value.includes(option.value));
  const toggleSelected = (option: Option) => {
    if (isSelected(option)) {
      setSelected(selected.filter(op => op.value !== option.value));
    } else {
      if (option.isNew) {
        option.label = option.value;
      }
      setSelected([...selected, option]);
    }
  };
  const filterResults = (value: string) => {
    if (value) {
      const selectOptions = [
        ...props.options.filter(op => op.label.toLowerCase().includes(value.toLowerCase())),
        { label: `Add new Author: ${value}`, value, isNew: true }
      ];
      setFiltered(selectOptions);
    } else {
      setFiltered(props.options);
    }
  };

  return (
    <>
      <div className={`absolute z-40 w-full h-full ${displayOptions ? '' : 'hidden'}`} onClick={() => setDisplayOptions(false)}></div>
      <div className="flex flex-col gap-2 z-50">
        <div className="flex gap-2 bg-white p-1 rounded-sm" id={`multi-select-input-${props.name}`}>
          {selected.map(selected => (<Badge key={selected.value}><div className="flex gap-2" onClick={() => toggleSelected(selected)}>{selected.label}<IconX size={16} /></div></Badge>))}
          <input
            type="text"
            className="flex-1 pl-1 p-0 outline-none"
            value={filterText}
            onFocus={() => setDisplayOptions(true)}
            onChange={(event) => {
              filterResults(event.target.value);
              setFilterText(event.target.value);
            }} />
          <input type="hidden" name={props.name} value={selected.map(op => op.value).join(',')} />
        </div>
        <div 
          className={`fixed py-2 rounded-md max-h-80 min-w-80 overflow-scroll dark:bg-zinc-700 ${displayOptions ? '' : 'hidden'}`}
          style={{ top: (inputEl?.offsetTop ?? 0) + (inputEl?.offsetHeight ?? 0) + 5, left: inputEl?.offsetLeft, width: inputEl?.offsetWidth }}
        >
          {filtered.map(option => (
            <div 
              key={option.value}
              onClick={(_) => {
                toggleSelected(option);
                setDisplayOptions(false);
              }}
              className={`px-2 dark:hover:bg-zinc-800 ${isSelected(option) ? 'dark:bg-zinc-800' : ''}`}
            >
              {option.label}
            </div>
          ))}
        </div>
      </div>
    </>
  );
}