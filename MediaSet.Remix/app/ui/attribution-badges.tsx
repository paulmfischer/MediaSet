import type { Handle } from 'remix/component';

import type { Integration } from '../api/integrations-data.ts';

interface AttributionBadgesProps {
  integrations: Integration[];
}

// Logos and credit lines for the enabled lookup integrations that images and metadata come from.
export function AttributionBadges(handle: Handle<AttributionBadgesProps>) {
  return () => {
    const enabled = handle.props.integrations.filter((integration) => integration.enabled && integration.logoPath);
    if (enabled.length === 0) return null;

    return (
      <section aria-labelledby="attribution-heading" class="mt-8 border-t border-zinc-800/60 pt-6">
        <h2 id="attribution-heading" class="mb-3 text-sm font-semibold text-zinc-400">
          Attribution
        </h2>
        <div class="flex flex-wrap items-start justify-center gap-4 w-full max-w-7xl mx-auto">
          {enabled.map((integration) => (
            <div class="inline-block w-auto">
              <a
                href={integration.attributionUrl ?? '#'}
                target="_blank"
                rel="noopener noreferrer"
                aria-label={integration.displayName}
                class="group block"
              >
                <div class="bg-zinc-800 ring-1 ring-zinc-800 rounded-md p-3 flex items-center justify-center shadow-sm">
                  <img
                    src={integration.logoPath ?? ''}
                    alt={integration.displayName}
                    class="h-8 w-auto object-contain"
                  />
                </div>
              </a>
              {integration.attributionText ? (
                integration.attributionUrl ? (
                  <a
                    href={integration.attributionUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    class="mt-2 block text-xs text-zinc-400 max-w-[280px] text-center underline hover:text-zinc-200"
                  >
                    {integration.attributionText}
                  </a>
                ) : (
                  <p class="mt-2 text-xs text-zinc-400 max-w-[280px] text-center">{integration.attributionText}</p>
                )
              ) : null}
            </div>
          ))}
        </div>
      </section>
    );
  };
}
