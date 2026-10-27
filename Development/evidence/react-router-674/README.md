# React Router shell checkpoint — 2026-10-27

Implements #674 under #673. Integration branch `feat/react-router-7` was created
from latest fetched `origin/main`, `dd3dc31`, before the topic branch
`feat/react-router-7-shell`. Topic PR targets the integration branch. No production
deployment or existing-service operation was performed.

## Version and migration decisions

Followed the documented [Remix 2 to Router 7 migration](https://reactrouter.com/7.18.4/upgrading/remix):
replace CLI/plugin/imports, add explicit routes/config, framework type generation,
and [server](https://reactrouter.com/7.18.4/api/framework-conventions/entry.server.tsx)/
[client](https://reactrouter.com/7.18.4/api/framework-conventions/entry.client.tsx)
entries. The original app is left intact; future/single-fetch semantics in the
replacement are the Router 7 defaults, not compatibility aliases back to Remix.

| Package group | Declared baseline → resolved | Selected replacement |
| --- | --- | --- |
| Remix node/react/dev | ^2.17.0 → 2.17.4 | Router framework 7.18.4 |
| Remix serve | ^2.16.4 → 2.17.4 | @react-router/serve 7.18.4 |
| react-router (baseline transitive) | Remix dependency → 6.30.3 | 7.18.4 |
| React / React DOM | ^18.2.0 → 18.3.1 | exact 18.3.1 |
| Vite | ^6.4.1 → 6.4.1 | exact 6.4.1, held |
| Tailwind | ^3.4.19 → 3.4.19 | exact 3.4.19 |
| TypeScript | ^5.7.3 → 5.9.3 | exact 5.9.3 |
| Node | .nvmrc / engines | 24.22.1; npm 11.19.1 |
| happy-dom | ^20.0.10 → 20.3.4 | exact 21.0.4 security exception |

Official npm registry documents for `react-router`, `@react-router/dev`,
`@react-router/node`, `@react-router/serve` were rechecked on October 27. All four
7.18.4 packages were published September 15 (42 days old), are not deprecated,
and require Node >=20. Router peers React/DOM >=18; dev peers Vite 5/6/7/8 and
TypeScript ^5.1 or ^6; node/serve align to Router 7.18.4. RSC/Wrangler peers are
optional and not installed. React 19, Router 8, Remix 3 and Tailwind 4 are held.

All selected direct versions are exact in `MediaSet.Router/package.json` and the
new lockfile. Existing CSS/config, icons, React 18 types, isbot, TypeScript,
ESLint/Prettier and Vitest/testing-library tool versions retain baseline resolved
versions. `@eslint/js`, `globals` and Node types become explicit direct dependencies
because configuration/SSR imports use them. Node types target 24 instead of the
baseline's incidental transitive 25. UI styles and public assets are copied,
not shared through the original runtime.

Remix packages/devtools are replaced/omitted from the new runtime; unused
Tailwind 4 PostCSS adapter, React Vite plugin and mkcert plugin are omitted.
ZXing and tiny-invariant are deferred until their actual feature consumers arrive
in later slices; nothing is removed from the current frontend. No framework
devtools replacement or incidental library modernization is included.

The new graph resolves 579 installed packages after the happy-dom fix versus
1083 for the baseline (`npm ci` package count, excluding root); new lock resolution
also selects eligible transitive versions rather than carrying Remix peers into
the replacement. Use the lockfiles for exact transitive versions. `npm ls` verifies
there are no invalid/unsatisfied installed peers.

## Security holds and exception

`happy-dom` 21.0.4 was published October 20 (7 days old) and supports Node >=20.
It is an explicit exception to the normal 14-day stability window because the
[maintainer advisory](https://github.com/capricorn86/happy-dom/security/advisories/GHSA-834v-8h66-3h79)
identifies host code execution through 21.0.3. The replacement's six tests
passed with this fixed test environment; broader test migration remains #667.

`npm audit --omit=dev` reports **zero** vulnerabilities. Full audit reports five
affected development package nodes: Vite (high), PostCSS (high), Vitest and its
coverage adapter (critical), and Vitest mocker (moderate). These baseline versions
are deliberately retained for the shell checkpoint and are not a security-clean
claim. Test UI/API serving is not enabled, and dev listens on loopback by default.
Do not expose this checkpoint to untrusted networks. Scoped servicing belongs to
#663 (PostCSS/styling), #667 (Vitest/mocker/coverage), #668 (Vite/utilities). ESLint
9.39.2's npm deprecation/support notice is held for #666's coordinated lint work.
No `npm audit fix --force`, broad upgrade, or production approval is implied.

Relevant audit IDs: Vite GHSA-p9ff-h696-f583, GHSA-587h-7wmj-h5pm;
PostCSS GHSA-6g55-p6wh-862q, GHSA-r28c-9q8g-f849;
Vitest GHSA-5xrq-8626-4rwp; mocker GHSA-82fw-gwwq-j7x9,
GHSA-qxg6-89mg-rqv8. Exact full-audit details can be reproduced with `npm audit`.

## Verification

Commands are in [the application README](../../../MediaSet.Router/README.md).
Local checks used Node 24.22.1 and .NET SDK 10.0.112:

| Area | Result |
| --- | --- |
| API restore/build/test | 0 build warnings/errors; 771 passed, 1 skipped |
| Existing Remix npm ci/lint/format/type/build/test | passed; 1053 tests, 38 files |
| Replacement npm ci/lint/format/type/build/test | passed; 6 tests, 2 files |
| Production API graph audit | zero vulnerabilities |
| Whitespace | git diff --check, staged and origin/main branch checks |

Runtime verification used a fresh `mediaset-674-mongo` container, no mounted
volume, loopback 27028, database `MediaSet674`; the existing API ran on loopback
5100 with `/tmp/mediaset-674-images`, no User Secrets or external logging. Original
MongoDB on 27017 and all production infrastructure remained untouched.

Headless Chromium: baseline 3001, replacement dev 3100 and production 3101 all
returned 200. Production HTML included API status `healthy`; private database
name and private health address were absent. Desktop Tools dropdown and mobile
menu worked after hydration. Normal-page console/page errors were empty. `/missing`
returned 404 with the existing error/recovery shell (expected console 404 and
root error diagnostics were recorded separately). Asset requests succeeded.
HMR changed the home heading while preserving `window.hmrSentinel === 674`, with
zero page errors; the source was restored afterward.

Mobile HTTPS configuration failed explicitly without certificate settings. With
an ephemeral one-day localhost certificate and a 390×844 Chromium viewport,
HTTPS returned 200, secureContext/mediaDevices were true, the menu hydrated,
and same-origin `/dev-api/health` returned `healthy` without browser errors.
The browser harness bypassed the self-signed test certificate error only; no
host/device trust store was changed. This is configuration/emulation evidence,
not real-device camera or Safari/Firefox/Edge parity evidence. Those remain the
explicit #664/#679 gates.

Screenshots below use matching dark media preference. The shell preserves the
header/navigation/footer/CSS; dashboard and collection content migrate later.

- [Baseline desktop](mediaset-674-baseline.png)
- [Baseline mobile](mediaset-674-baseline-mobile.png)
- [Production shell desktop](mediaset-674-shell-production.png)
- [Production shell mobile](mediaset-674-shell-production-mobile.png)
- [404 shell](mediaset-674-not-found.png)
- [HTTPS mobile shell](mediaset-674-https-mobile.png)

## Follow-ups

- Existing companion issues #663/#666/#667/#668 own the held development tooling
  advisories/support work above; #664/#679 own real-device and browser parity.
- Existing API registration fails for every route when OpenLibrary/TMDB/MusicBrainz
  configuration sections are all absent: unregistered LookupStrategyFactory is
  inferred as a body parameter on a GET endpoint. Reproduced in the disposable
  Production API; supplying an inert OpenLibrary BaseUrl enables verification.
  This pre-existing backend issue is outside #674 and needs separate tracking.
