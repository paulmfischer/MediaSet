import type { Handle, RemixNode } from 'remix/component';

interface LookupPanelProps {
  /** Heading that names what the fields look up, for example "Book Lookup". */
  label: string;
  /** Metadata lookup is available; when it isn't the fields are plain form fields with no panel. */
  enabled: boolean;
  children?: RemixNode;
}

// Groups the fields a metadata lookup searches by, in a panel tinted with the entity's colour (the
// page's `entity-*` class), so they stand apart from the fields the lookup fills in.
export function LookupPanel(handle: Handle<LookupPanelProps>) {
  return () => {
    const { label, enabled, children } = handle.props;
    if (!enabled) return <>{children}</>;
    return (
      <div
        role="group"
        aria-label={label}
        class="flex flex-col gap-4 rounded-lg border border-entity/20 bg-entity/10 p-4"
      >
        <p class="text-sm font-semibold text-gray-300">{label}</p>
        {children}
      </div>
    );
  };
}
