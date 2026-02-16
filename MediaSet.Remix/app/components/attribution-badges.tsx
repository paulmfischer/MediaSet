import type { Integration } from "~/api/integrations-data";

type Props = {
  integrations?: Integration[];
};

export default function AttributionBadges({ integrations = [] }: Props) {
  const enabled = integrations.filter((i) => i && i.enabled && i.logoPath);
  if (!enabled.length) return null;

  return (
    <section aria-labelledby="attribution-heading" className="mt-8 border-t border-zinc-800/60 pt-6">
      <h3 id="attribution-heading" className="mb-3 text-sm font-semibold text-zinc-400">
        Attribution
      </h3>
      <div className="flex flex-wrap items-start gap-4 justify-center w-full max-w-7xl mx-auto">
        {enabled.map((i) => (
          <div key={i.key} className="inline-block w-auto">
            <a
              href={i.attributionUrl ?? "#"}
              target="_blank"
              rel="noopener noreferrer"
              aria-label={i.displayName}
              className="group block"
            >
              <div className="bg-zinc-800 ring-1 ring-zinc-800 rounded-md p-3 flex items-center justify-center transition-transform group-hover:scale-105 shadow-sm">
                <img src={i.logoPath!} alt={i.displayName} className="h-8 w-auto object-contain" />
              </div>
            </a>
            {i.attributionText ? (
              i.attributionUrl ? (
                <a
                  href={i.attributionUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="mt-2 block text-xs text-zinc-400 max-w-[280px] text-center underline hover:text-zinc-200"
                >
                  {i.attributionText}
                </a>
              ) : (
                <p className="mt-2 text-xs text-zinc-400 max-w-[280px] text-center">{i.attributionText}</p>
              )
            ) : null}
          </div>
        ))}
      </div>
    </section>
  );
}
