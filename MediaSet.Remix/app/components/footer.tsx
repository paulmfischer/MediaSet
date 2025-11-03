interface FooterProps {
  version?: string;
}

export default function Footer({ version }: FooterProps) {
  const year = new Date().getUTCFullYear();
  
  return (
    <footer className="min-h-12 flex flex-row items-center justify-between px-4 dark:bg-zinc-700">
      <span>Copyright {year} Paul Fischer</span>
      {version && (
        <span className="text-sm text-slate-400">
          v{version}
        </span>
      )}
    </footer>
  );
}
