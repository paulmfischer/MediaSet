import type { Handle } from 'remix/component';
import type { Issue } from 'remix/data-schema';

import { routes } from '../../routes.ts';
import { Document } from '../../ui/document.tsx';
import { SaveButton } from './public/save-button.tsx';

interface AddPageProps {
  entity: string;
  values?: { title: string; year: string; genres: string[] };
  issues?: readonly Issue[];
}

export function AddPage(handle: Handle<AddPageProps>) {
  return () => {
    let { entity, values, issues = [] } = handle.props;
    let issueFor = (name: string) => issues.find((i) => i.path?.[0] === name);
    let field = (name: 'title' | 'year', label: string) => {
      let issue = issueFor(name);
      return (
        <div class="flex flex-col gap-1">
          <label htmlFor={name}>{label}</label>
          <input
            id={name}
            name={name}
            defaultValue={values?.[name] ?? ''}
            aria-invalid={issue ? true : undefined}
            aria-describedby={issue ? `${name}-error` : undefined}
          />
          {issue && (
            <p id={`${name}-error`} class="text-red-400">
              {issue.message}
            </p>
          )}
        </div>
      );
    };
    return (
      <Document title={`Add to ${entity}`} path={`/${entity}/add`}>
        <form method="post" action={routes.add.action.href({ entity })} class="flex flex-col gap-3 max-w-sm">
          {issues.length > 0 && <p role="alert">Please correct the fields below.</p>}
          {field('title', 'Title')}
          {field('year', 'Year')}
          {['scifi', 'fantasy', 'noir'].map((g) => (
            <label key={g}>
              <input type="checkbox" name="genres" value={g} defaultChecked={values?.genres.includes(g)} /> {g}
            </label>
          ))}
          <SaveButton />
        </form>
      </Document>
    );
  };
}
