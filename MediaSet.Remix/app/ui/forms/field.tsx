import type { Handle, RemixNode } from 'remix/component';

export const inputClasses =
  'w-full px-3 py-2 border border-gray-600 bg-gray-800 text-white rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-blue-400';

export const errorId = (name: string) => `${name}-error`;

interface FieldProps {
  /** Field name; the control's `id` is the same and the error is `${name}-error`. */
  name: string;
  label: string;
  error?: string | undefined;
  required?: boolean;
  children?: RemixNode;
}

// Label, control and the field's validation error. The control links to the error with aria-describedby.
export function Field(handle: Handle<FieldProps>) {
  return () => {
    const { name, label, error, required, children } = handle.props;
    return (
      <div>
        <label for={name} class="block text-sm font-medium text-gray-200 mb-1">
          {label}
          {required ? <span aria-hidden="true"> *</span> : null}
        </label>
        {children}
        {error ? (
          <p id={errorId(name)} class="mt-1 text-sm text-red-400">
            {error}
          </p>
        ) : null}
      </div>
    );
  };
}

/** Props a control needs to flag its error to assistive tech. */
export const errorProps = (name: string, error?: string) =>
  error ? ({ 'aria-invalid': true, 'aria-describedby': errorId(name) } as const) : {};
