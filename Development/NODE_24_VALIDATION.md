# Node 24 runtime alignment (#653)

## Selection (2026-10-26)

The reproducible application target is Node **24.22.1 LTS (Krypton)**. The
application engine and passive setup diagnosis accept `>=24.22.1 <25`.
`.nvmrc` pins development and CI; both Docker stages use
`node:24.22.1-alpine`. Docker patch tags still receive Alpine/base-image updates;
record the resolved image digest when validating or deploying.

This is an explicit security exception to #652's normal 14-day release-age
policy: 24.22.1 was released October 21 (five days before this decision).
The [official release notes](https://nodejs.org/en/blog/release/v24.22.1)
identify it as an LTS security release, including DNS/TLS validation,
Brotli memory-corruption and HTTP Agent fixes. The
[October security advisory](https://nodejs.org/en/blog/vulnerability/october-2026-security-releases)
includes Node 24 among affected release lines. Holding an older patch for age
would omit these fixes. The
[Node release table](https://nodejs.org/en/about/previous-releases) identifies
24 as LTS and 20 as end-of-life. Recheck these sources for the next update.

## Starting inventory and dependency impact

Before this change, application engines and agent prerequisites allowed Node
20+, CI used `20.x`, both active Docker stages used `20-alpine`, and README and
development instructions requested Node 22. The validation host originally ran
Node 22.23.4 / npm 10.9.9. CI now reads `.nvmrc` and includes it in the dependency
cache key. Production installation uses the current `npm ci --omit=dev` spelling.
The tracked legacy `MediaSet.Remix/Dockerfile.bak` example's image pin also moves
to 24.22.1; its old clone/install recipe remains archival and is not a supported
build path. Container validation uses the active two-stage `Dockerfile`.

Only the root package engine metadata changes in `package-lock.json`; dependency
versions, integrity hashes and resolution URLs remain fixed. Declared/resolved
framework versions intentionally held for the separate #652 migration slices:

| Package | Declared | Resolved |
| --- | --- | --- |
| Remix dev/react/node | ^2.17.0 | 2.17.4 |
| React | ^18.2.0 | 18.3.1 |
| Vite | ^6.4.1 | 6.4.1 |
| Tailwind CSS | ^3.4.19 | 3.4.19 |
| Vitest | ^4.0.8 | 4.0.17 |
| happy-dom | ^20.0.10 | 20.3.4 |

Their lockfile engine declarations admit Node 24. Remix remains 2, React 18,
Vite 6 and Tailwind 3; this change neither adopts Remix 3 nor changes frontend
behavior. The existing optional Tailwind oxide 4.1.18 dependency comes from
`@tailwindcss/postcss`; it does not upgrade the application Tailwind major.

Native/platform-sensitive dependencies include esbuild 0.17.6/0.21.5/0.25.12/
0.27.2, Rollup 4.53.2, oxide 4.1.18, unrs-resolver 1.11.1 and macOS-only
fsevents 2.3.3. esbuild and unrs-resolver have install scripts; the Linux clean
install and Alpine build/runtime exercise their supported platform packages.
Optional binaries for other CPU/OS targets remain in the lock; they are not
evidence of running those platforms. No peer-dependency or engine override is
part of setup, Docker or CI.

The 2026-10-26 `npm audit --json` snapshot reports **64 vulnerable package
entries** (2 low, 21 moderate, 37 high, 4 critical). These are existing locked
dependencies: parsed base/current lockfiles are identical after normalizing the
root engine field. Node security fixes do not resolve npm package advisories.
No `npm audit fix` or unrelated dependency upgrade is included. Explicit holds:

| Existing exposure | Resolved / audit range | Tracked migration slice |
| --- | --- | --- |
| Vitest and coverage-v8, critical | 4.0.17; Vitest aggregate range 2.1.0-beta.1–4.1.10 | #667 test tooling |
| proxy-addr, critical | 2.0.7; affected 1.1.0–2.0.7 | #654 framework/server dependency tree |
| tar, critical | 6.2.1; audit aggregate range <=7.5.20 | #668 utility dependencies and owning dependency trees |
| Remix packages, high | 2.17.4 | #654 framework |
| Vite, high | 6.4.1; audit includes <=6.4.3 | #654 framework/build tooling |
| PostCSS, high | 8.5.6; audit includes <=8.5.22 | #663 styling |
| happy-dom, high | 20.3.4; audit includes <=21.0.3 | #667 test tooling |

Representative advisories include
[Vitest UI execution (<4.1.0)](https://github.com/advisories/GHSA-5xrq-8626-4rwp),
[Vitest mocker path traversal (<4.1.11)](https://github.com/advisories/GHSA-82fw-gwwq-j7x9),
[proxy-addr trust-subnet spoofing (<2.0.8)](https://github.com/advisories/GHSA-jqcg-44mw-7w3h),
[tar hardlink traversal (<7.5.7)](https://github.com/advisories/GHSA-34x7-hfp2-rc4v),
[Vite optimized-dependency path traversal (<=6.4.1)](https://github.com/advisories/GHSA-4w7w-66w2-5vf9),
[PostCSS source-map read (<=8.5.22)](https://github.com/advisories/GHSA-fxqj-rqcc-2cmp),
and [happy-dom import alias execution (<=21.0.3)](https://github.com/advisories/GHSA-834v-8h66-3h79).
Audit ranges combine multiple advisories and dependency propagation; the counts
are package entries, not a claim that every advisory is reachable in production.
The sibling slices must refresh the full audit and resolve their dependency trees.

## Reproduce

Install nvm using its own instructions if desired, then from this checkout run
`nvm install` and `nvm use` (or install the exact `.nvmrc` version with the
official Node installer). Check `node --version` and `npm --version`, then run:

```bash
bash scripts/agents/setup.sh bootstrap
python3 -B scripts/agents/verify.py
podman build -t mediaset-ui-node24-smoke -f MediaSet.Remix/Dockerfile MediaSet.Remix
```

The bootstrap performs a clean lockfile install without dependency overrides.
Container smoke testing must use disposable local services/data, an isolated
network and loopback-only published ports. Never use a production database or
modify an existing service to perform this check.

## Validation evidence

On 2026-10-26 (local time), nvm selected Node **24.22.1** and its bundled
**npm 11.19.1**; the backend SDK resolved to **10.0.112**. Official Linux x64
tarball SHA-256 checked during runtime preparation:
`6c172a9ab6718f7b801fb5a7bd486af2ee5e32758086a04b84fcce2aa77095e6`.
The clean checkout install added 1,083 packages using `npm ci --no-audit
--no-fund` with the checkout-local cache, without peer/engine override flags or
peer/engine warnings. npm reported six install scripts requiring allowScripts
review (the esbuild versions listed above and unrs-resolver); no blanket script
approval was added. The build and lint/tests exercise the installed platform
binaries; the warning remains visible for deliberate review on later upgrades.

`python3 -B scripts/agents/verify.py` selected all three groups from the complete
branch/local change set against `origin/main` at `7eeff9603bc02f3242a2f293f48da81e830aa1a4`.
All nine checks passed: whitespace, 116 agent contracts, backend build (zero
warnings/errors), backend tests (771 passed, 1 existing skipped), frontend lint,
formatting, TypeScript, production build and 1,053 tests in 38 files.
No selected check was omitted. Local ignored evidence:
`.agent-local/verification/run-20261027T011327752605Z-237b16d25d654c28b5290641ef6eaf71/summary.json`.

The first sandbox run passed all frontend and agent checks but backend test
communication failed with `SocketException (13): Permission denied`; its backend
build returned exit 1 without diagnostics. The unrestricted run above passed
both backend checks. These were sandbox execution limits, not ignored failures.
Original evidence is in
`.agent-local/verification/run-20261027T010201926659Z-89c5f9b4f4b04085aa7b43d580cbc879/`.

Production smoke passed using Podman 5.8.9 on Linux x64. Both active Dockerfiles
built successfully, including clean full and production-only npm installations.
The UI ran as UID 100 with Node 24.22.1, npm 11.19.1, OpenSSL 3.5.9 and undici
7.30.1. Resolved immutable evidence (patch tags themselves remain mutable):

| Image | Digest |
| --- | --- |
| `node:24.22.1-alpine` platform image | `sha256:1200d815de24f8dcca745fbf7651d166034a35adbcad8860a8538ffff44f1420` |
| Built UI | `sha256:dce041595e65ff568dcf0dbbf1b395ef57c7be87b2041cacc65ba91978dba33c` |
| Built API | `sha256:b1efa6c71c11a628f8ac485de0e330a23f8c8e9cd58eca4b6dcce5a390312b82` |
| Disposable `mongo:8.0` | `sha256:2711bb79d785c58ce13a8f97e082539d40d65fe7810268b33f12fc68757af48d` |

The test created a dedicated network and MongoDB tmpfs, with no API or database
ports published and only the UI published to an ephemeral loopback port. It
posted `Node 24 isolated smoke` to the real API (HTTP 201), verified one book and
123 pages in real statistics, and received HTTP 200 from the production Remix
dashboard and `/books`, asserting the seeded title in the rendered book page.
The captured UI HTML and API logs demonstrate server-side API communication.
All three test containers and their network were removed after testing.
Evidence: `.agent-local/node24-smoke-1793063991225418004/` (runtime versions,
image inspection, seed/statistics response, HTML and service/cleanup logs).

The first disposable smoke revealed the existing minimal-production lookup
registration bug tracked in [#670](https://github.com/paulmfischer/MediaSet/issues/670).
The successful smoke supplied an inert
`OpenLibraryConfiguration__BaseUrl=http://127.0.0.1:9/` to satisfy the current
registration condition; it performed no external lookups. Backend source is
unchanged. Initial failure evidence remains in
`.agent-local/node24-smoke-1793062903109442178/`.

## Limits and holds

No production deployment is included. Node alignment alone does not validate a
framework migration. Browser layout and mobile/browser-matrix validation remain
with their respective migration slices; this runtime change requires a
representative production page/API smoke, not a browser-support claim.
No browser-driven layout walkthrough or screenshot was selected because no UI
component, style or browser behavior changed; the production HTTP/page checks
above cover this runtime slice. No live production service or data was used.
