# UI Dependencies Update Implementation

**Date**: January 22, 2026  
**Tracking Issue**: #413  
**Status**: Planning Phase

## Overview

This document details the plan to update UI dependencies (Remix.js frontend) to the latest versions. Updates will be performed incrementally, with each dependency or dependency group updated separately, allowing for isolated testing and easier debugging of any issues.

## Current Versions Summary

### Runtime Dependencies
| Package | Current | Target | Type |
|---------|---------|--------|------|
| @remix-run/node | 2.17.0 | 2.17.4 | Patch |
| @remix-run/react | 2.17.2 | 2.17.4 | Patch |
| @remix-run/serve | 2.17.0 | 2.17.4 | Patch |
| isbot | 5.1.22 | 5.1.33 | Patch |
| lucide-react | 0.474.0 | 0.562.0 | Minor |
| react | 18.3.1 | 18.3.1 | (No update) |
| react-dom | 18.3.1 | 18.3.1 | (No update) |
| tiny-invariant | 1.3.3 | 1.3.3 | (No update) |

### Dev Dependencies
| Package | Current | Target | Type |
|---------|---------|--------|------|
| @remix-run/dev | 2.17.0 | 2.17.4 | Patch |
| @testing-library/jest-dom | 6.9.1 | 6.9.1 | (No update) |
| @testing-library/react | 16.3.0 | 16.3.2 | Patch |
| @testing-library/user-event | 14.6.1 | 14.6.1 | (No update) |
| @types/react | 18.3.18 | 18.3.27 | Patch |
| @types/react-dom | 18.3.5 | 18.3.7 | Patch |
| @typescript-eslint/eslint-plugin | 8.22.0 | 8.53.1 | Patch |
| @typescript-eslint/parser | 8.22.0 | 8.53.1 | Patch |
| @vitejs/plugin-react | 4.7.0 | 5.1.2 | Minor |
| @vitest/coverage-v8 | 4.0.8 | 4.0.17 | Patch |
| autoprefixer | 10.4.20 | 10.4.23 | Patch |
| eslint | 9.19.0 | 9.39.2 | Patch |
| eslint-import-resolver-typescript | 3.7.0 | 4.4.4 | Major |
| eslint-plugin-import | 2.31.0 | 2.32.0 | Patch |
| eslint-plugin-jsx-a11y | 6.10.2 | 6.10.2 | (No update) |
| eslint-plugin-react | 7.37.4 | 7.37.5 | Patch |
| eslint-plugin-react-hooks | 5.1.0 | 7.0.1 | Major |
| happy-dom | 20.0.10 | 20.3.4 | Patch |
| postcss | 8.4.38 | 8.4.38 | (No update) |
| remix-development-tools | 4.7.7 | 4.7.7 | (No update) |
| tailwindcss | 3.4.17 | 4.1.18 | Major |
| typescript | 5.7.3 | 5.9.3 | Minor |
| vite | 5.4.19 | 6.4.1 | Major (cap per Remix peer) |
| vite-tsconfig-paths | 4.3.2 | 6.0.4 | Major |
| vitest | 4.0.8 | 4.0.17 | Patch |
| vitest-dom | 0.1.1 | 0.1.1 | (No update) |

## Breaking Changes Analysis

### Major Version Updates (Require Investigation)
1. **TailwindCSS 3 → 4** - Significant API changes. Requires `tailwind.config.ts` updates and possible CSS rewrites.
2. **Vite 5 → 6** - Major version jump (capped at 6.4.1 to satisfy Remix peer dependency).
3. **vite-tsconfig-paths 4 → 6** - Major version. May require config adjustments.
4. **eslint-plugin-react-hooks 5 → 7** - Major version. May have breaking config changes.
5. **eslint-import-resolver-typescript 3 → 4** - Major version. May affect ESLint resolver behavior.

### Remix Peer Dependency Alignment
- React must remain 18.x (Remix 2.17.x peer).
- Typescript must stay within ^5.1.x (5.9.3 is within range).
- Vite must stay within ^5.1.0 || ^6.x; target **6.4.1** (do not move to 7.x until Remix updates its peer range).
- @remix-run packages stay on 2.17.x range for compatibility.

### Minor/Patch Updates
- Patch updates are generally safe and primarily contain bug fixes
- Minor updates may introduce new features but maintain backward compatibility
- lucide-react 0.474 → 0.562: Minor bump, API should remain compatible

## Update Strategy

### Phase 1: Low-Risk Patch Updates (No Expected Breaking Changes)
1. Update Remix.js family (@remix-run/node, @remix-run/react, @remix-run/serve, @remix-run/dev)
2. Update testing libraries (@testing-library/react, vitest, @vitest/coverage-v8)
3. Update TypeScript ESLint packages (@typescript-eslint/eslint-plugin, @typescript-eslint/parser)
4. Update ESLint and eslint plugins (eslint, eslint-plugin-import, eslint-plugin-react)
5. Update utilities (autoprefixer, happy-dom, isbot)

### Phase 2: Minor Updates (Likely Safe)
1. Update TypeScript (5.7.3 → 5.9.3) - Run typecheck to validate
2. Update lucide-react (0.474 → 0.562)
3. Update @vitejs/plugin-react (4.7.0 → 5.1.2)

### Phase 3: Major Updates (Require Investigation & Code Changes)
1. **eslint-import-resolver-typescript (3 → 4)** - May affect ESLint config, validate resolver behavior
2. **eslint-plugin-react-hooks (5 → 7)** - Check ESLint config for any breaking changes
3. **vite-tsconfig-paths (4 → 6)** - Test vite config and path resolution
 4. **Vite (5 → 6)** - Check vite.config.ts for deprecated options, test build process; keep within Remix peer range (^5.1 || ^6.x)
5. **TailwindCSS (3 → 4)** - Major CSS framework update, requires `tailwind.config.ts` updates and CSS testing

**React stays on 18.x**: Remix 2.17.x peer dependency requires React 18. No React 19 upgrade until Remix expands support.

### Phase 4: Final Integration & Testing
1. Run full npm test suite
2. Run npm run typecheck
3. Run npm run build
4. Manual testing of application UI
5. Update package.json version for minor release (e.g., 1.2.0 → 1.3.0)

## Expected Code Changes

### 1. TailwindCSS v3 → v4
**Files Affected**: `tailwind.config.ts`, `app/styles/*.css`

**Changes Required**:
- Update `tailwind.config.ts` configuration syntax
- May need to update CSS custom properties and theme values
- Review component CSS for deprecated Tailwind utilities
- Update any custom CSS rules that rely on Tailwind internals

### 2. Vite 5 → 6
**Files Affected**: `vite.config.ts`

**Potential Changes**:
- Review deprecated config options
- Update plugin configurations if necessary
- Test HMR (Hot Module Replacement) functionality
- Verify build process and output

### 3. ESLint & Plugin Updates
**Files Affected**: `.eslintrc.json` (if exists) or ESLint config in `package.json`

**Potential Changes**:
- May need to update ESLint resolver configuration
- React Hooks plugin may have new rules or rule options
- May need to update rule severity levels

## Testing Checklist

- [ ] npm install (update node_modules)
- [ ] npm run typecheck (verify TypeScript)
- [ ] npm test (run test suite)
- [ ] npm run build (verify build succeeds)
- [ ] npm run lint (verify code style)
- [ ] Manual browser testing of:
  - [ ] Home page loads correctly
  - [ ] Navigation works
  - [ ] Form interactions
  - [ ] Styling appears correct (no CSS regressions)
  - [ ] Icons render correctly (lucide-react)

## Commit Strategy

Each logical update or group of updates will have its own commit:
- Patch updates for stable dependencies: grouped commits (e.g., "chore(deps): update remix packages")
- Each major version update: individual commit with focus on potential breaking changes
- Code changes: separate commits with clear descriptions of what changed and why
- Final version bump: single commit

**Commit format**: `chore(deps): <description> refs #413`

## Version Change

Current version: Check `package.json` for current version  
Expected bump: **Minor version** (no breaking changes to public API expected)

---

## Implementation Log

(To be updated as work progresses)

### Phase 1: Patch Updates
- [ ] Started: [date]
- [ ] Completed: [date]
- [ ] Issues: [any issues encountered]

### Phase 2: Minor Updates
- [ ] Started: [date]
- [ ] Completed: [date]
- [ ] Issues: [any issues encountered]

### Phase 3: Major Updates
- [ ] Started: [date]
- [ ] Completed: [date]
- [ ] Issues: [any issues encountered]

### Phase 4: Final Integration
- [ ] Started: [date]
- [ ] Completed: [date]
- [ ] All tests passing: [ ]
- [ ] PR created: [ ]
- [ ] PR merged: [ ]

