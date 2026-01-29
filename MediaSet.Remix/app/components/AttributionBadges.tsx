import type { Integration } from "~/integrations-data";

type Props = {
  integrations: Integration[];
};

export default function AttributionBadges({ integrations }: Props) {
  const enabled = integrations.filter((i) => i.enabled && i.logoPath);
  if (!enabled.length) return null;

  return (
    <section aria-labelledby="attribution-heading" className="mt-8">
      <h3 id="attribution-heading" className="mb-3 text-sm font-semibold text-zinc-400">
        Attribution
      </h3>
      <div className="flex flex-wrap items-start gap-4">
        {enabled.map((i) => (
          <a
            key={i.key}
            href={i.attributionUrl ?? "#"}
            target="_blank"
            rel="noopener noreferrer"
            aria-label={i.displayName}
            className="inline-block w-auto"
          >
            <div className="flex flex-col items-center">
              <img src={i.logoPath!} alt={i.displayName} className="h-8 object-contain" />
              {i.attributionText ? (
                <p className="mt-1 text-xs text-zinc-400 max-w-xs text-center">
                  {i.attributionText}
                </p>
              ) : null}
            </div>
          </a>
        ))}
      </div>
    </section>
  );
}
