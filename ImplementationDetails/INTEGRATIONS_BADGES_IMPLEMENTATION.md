# Integrations Badges / Attribution — Implementation Details

Summary
- Add conditional display of attribution badges for external integrations (e.g., TMDB, MusicBrainz, OpenLibrary, UPCitemdb, Giantbomb) when the corresponding API integration is configured.
- Place badges at the bottom of the home page (above the global footer) with a short header (`Attribution`). Do not place additional wording next to badges.

Goals
- Ensure required attribution requirements for each provider are met.
- Only show badges when the server-side API configuration indicates the provider is enabled/available.
- Keep badges accessible and responsive; provide links to provider terms/licensing.

High-level approach
1. Author a single ImplementationDetails document (this file) describing license checks, UI placement, and data flow.
2. Backend: expose which integrations are currently configured via an API endpoint (e.g., `GET /api/config/integrations` or include in public `/api/health` payload). This should be read-only and safe for public consumption (no keys returned), returning booleans and optional attribution metadata (displayName, link, logoUrl).
3. Frontend Remix: fetch the integrations config on the home route loader (server-side) and render a `Badges` component at the bottom of the home page (above the footer). The component must render a short header `Attribution` above the horizontal badge list and must not include any extra wording adjacent to the badges.
4. Assets: use provider-provided logos/badges where appropriate; host locally in `public/` or link to provider-hosted badge if allowed by terms.
5. Testing: unit tests for the new backend endpoint; frontend tests for `Badges` component conditional rendering.

Backend details
- New DTO: `IntegrationAttributionDto { string Key; string DisplayName; bool Enabled; string? AttributionUrl; string? LogoPath; }`
- New endpoint: `GET /api/config/integrations` returns `IntegrationAttributionDto[]`. Must not return API keys or secrets.
- Source of truth: configuration flags (appsettings.json / environment variables) and any runtime plugin flags.

Frontend details (Remix)
- Data loading: call the new endpoint in the root loader or the home route loader to get the list.
- Component: `Badges` (React) accepts `IntegrationAttributionDto[]` and renders a horizontal list in the footer. Each badge includes an accessible alt and links to the provider's attribution page or homepage.
- SSR: fetch integrations during server render to avoid flicker.
- Styling: ensure small, consistent sizing and mobile wrapping.

Provider review (initial research)
- TheMovieDB (TMDB)
  - See: https://developer.themoviedb.org/docs/faq#what-are-the-attribution-requirements
  - TMDB requires attribution when using images/data; typically a TMDB logo and link to themoviedb.org is acceptable. Prefer using their provided badge guidelines if available.
- MusicBrainz
  - See: https://musicbrainz.org/doc/MusicBrainz_API
  - No explicit badge required, but attribution/credit is recommended.
- OpenLibrary
  - See: https://openlibrary.org/developers/licensing
  - No strict badge requirement; include link/credit if used.
- UPCitemdb
  - See: https://devs.upcitemdb.com/
  - No explicit requirements found; review TOS before linking logos.
- Giantbomb
  - See: https://www.giantbomb.com/api/
  - API currently in limbo; skip unless enabled. Check Terms of Use for attribution requirements.

Assets and hosting
- Host provider-provided badge images locally under `MediaSet.Remix/public/integrations/` to ensure availability and compliance. The images must be the official badges or logos provided by each service when available; do not invent or modify trademarked badges.
- Filenames (recommended): `tmdb.svg`, `musicbrainz.svg`, `openlibrary.svg`, `upcitemdb.svg`, `giantbomb.svg`.
- Each badge must be wrapped in a link to the provider's attribution/terms page. There must be no extra descriptive text next to the badges — only the `Attribution` header above them.
- Use `aria-label` for accessibility and include a tooltip linking to the provider's legal/attribution page if needed.

Edge cases and security
- The API must never reveal API keys/secrets - only boolean flags and safe metadata (display name, link, logo path).
- If runtime configuration can change, the frontend should refresh these badges periodically or fetch on each page load (depending on caching strategy).

Testing
- Backend: unit test to validate endpoint returns expected shape and respects config flags.
- Frontend: component tests for `Badges` to confirm proper rendering for enabled/disabled lists.

Tasks (implementation order)
1. Add ImplementationDetails doc (this file).
2. Backend: add DTO + controller/endpoint and wire config flags.
3. Frontend: add `Badges` component, fetch/load data, place in footer or About page.
4. Add badge assets to `MediaSet.Remix/public/integrations/`.
5. Add tests, update docs, and create PR.

Open questions / decisions for owner
- Preferred placement: footer on home page vs separate About/Attribution page? (Footer preferred unless owner objects.)
- Where to host badge images: local (recommended) or provider-hosted?
- Exact wording or additional legal text to display alongside badges?

References
- Issue: https://github.com/paulmfischer/MediaSet/issues/398
- TMDB attribution FAQ: https://developer.themoviedb.org/docs/faq#what-are-the-attribution-requirements
- MusicBrainz API: https://musicbrainz.org/doc/MusicBrainz_API
- OpenLibrary licensing: https://openlibrary.org/developers/licensing
- UPCitemdb: https://devs.upcitemdb.com/
- Giantbomb API: https://www.giantbomb.com/api/



