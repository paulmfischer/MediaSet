import type { Handle } from 'remix/component';

export function Footer(handle: Handle<{ version?: string }>) {
  return () => {
    const { version } = handle.props;
    const year = new Date().getUTCFullYear();

    return (
      <footer class="min-h-12 flex flex-row items-center justify-between gap-2 px-4 dark:bg-zinc-700">
        <span>Copyright {year} Paul Fischer</span>
        {version ? <span class="text-sm text-slate-400">v{version}</span> : null}
      </footer>
    );
  };
}
