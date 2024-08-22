import { useEffect, useState } from "react";
import Badge from "./badge";

type Option = {
  label: string;
  value: string;
}

type MultiselectProps = {
  name: string;
  selectText: string;
  options: Option[];
};

export default function MultiselectInput(props: MultiselectProps) {
  const [selected, setSelected] = useState<string[]>([]);
  const [filtered, setFiltered] = useState<Option[]>(props.options);
  const [displayOptions, setDisplayOptions] = useState(false);

  const isSelected = (option: Option) => selected.includes(option.value);
  const toggleSelected = (option: Option) => {
    if (isSelected(option)) {
      setSelected(selected.filter(x => x !== option.value));
    } else {
      setSelected([...selected, option.value]);
    }
  };
  const filterResults = (value: string) => {
    if (value) {
      setFiltered(props.options.filter(op => op.label.toLowerCase().includes(value.toLowerCase())));
    } else {
      setFiltered(props.options);
    }
  };

  return (
    <div className="flex flex-col gap-2">
      <div className="flex gap-2">
        {selected.map(selected => (<Badge>{selected}</Badge>))}
      </div>
      <input type="text" id="multi-select-input" onFocus={() => setDisplayOptions(true)} onChange={(event) => filterResults(event.target.value)} />
      <div className={`h-80 overflow-scroll ${displayOptions ? '' : 'hidden'}`}>
        {filtered.map(option => (
          <div key={option.value} onClick={(_) => { toggleSelected(option); setDisplayOptions(false); }} className={`dark:hover:bg-zinc-800 ${isSelected(option) ? 'dark:bg-zinc-800' : ''}`}>{option.label}</div>
        ))}
      </div>
    </div>
  );
}