# Changelog

## [0.4.0](https://github.com/paulmfischer/MediaSet/compare/v0.3.4...v0.4.0) (2025-11-06)


### Features

* **ui:** add Home icon and make header clickable closes [#254](https://github.com/paulmfischer/MediaSet/issues/254) ([#256](https://github.com/paulmfischer/MediaSet/issues/256)) ([218cf47](https://github.com/paulmfischer/MediaSet/commit/218cf4718d5ee6d23909c2d468454ce3ea562dd3))


### Bug Fixes

* **ci:** trigger docker workflows on release events closes [#245](https://github.com/paulmfischer/MediaSet/issues/245) ([#250](https://github.com/paulmfischer/MediaSet/issues/250)) ([b4f4c60](https://github.com/paulmfischer/MediaSet/commit/b4f4c601262cf5f14fd20adb0f2bf9cb461dfab1))

## [0.3.4](https://github.com/paulmfischer/MediaSet/compare/v0.3.3...v0.3.4) (2025-11-05)


### Bug Fixes

* **ui:** match add/edit form widths [AI-assisted] closes [#211](https://github.com/paulmfischer/MediaSet/issues/211) ([#248](https://github.com/paulmfischer/MediaSet/issues/248)) ([9244056](https://github.com/paulmfischer/MediaSet/commit/92440568b99f29f6648f5477928fdc73be4e3a8f))


### Tests

* **api:** cleanup test warnings [AI-assisted] closes [#216](https://github.com/paulmfischer/MediaSet/issues/216) ([#246](https://github.com/paulmfischer/MediaSet/issues/246)) ([2f5cc89](https://github.com/paulmfischer/MediaSet/commit/2f5cc893f790d7a3540f7079c81cb4b5ea0b09cb))

## [0.3.3](https://github.com/paulmfischer/MediaSet/compare/v0.3.2...v0.3.3) (2025-11-05)


### Bug Fixes

* **api:** robust movie format extraction and title cleaning [AI-assisted] closes [#229](https://github.com/paulmfischer/MediaSet/issues/229) ([#244](https://github.com/paulmfischer/MediaSet/issues/244)) ([b9b8717](https://github.com/paulmfischer/MediaSet/commit/b9b8717bd77a1a29312e128c3120cb4d4a4cdd49))


### Documentation

* improve AI instructions per issue requirements refs [#232](https://github.com/paulmfischer/MediaSet/issues/232) ([#242](https://github.com/paulmfischer/MediaSet/issues/242)) ([941fb30](https://github.com/paulmfischer/MediaSet/commit/941fb30fb748170c57d7d616db2702875e2d57f8))

## [0.3.2](https://github.com/paulmfischer/MediaSet/compare/v0.3.1...v0.3.2) (2025-11-05)


### Miscellaneous

* **ci:** simplify Docker workflows to only build on version tags [AI-assisted] ([#240](https://github.com/paulmfischer/MediaSet/issues/240)) ([8ca6d61](https://github.com/paulmfischer/MediaSet/commit/8ca6d61c50dffaa24b990c3690e32500ab037c47))

## [0.3.1](https://github.com/paulmfischer/MediaSet/compare/v0.3.0...v0.3.1) (2025-11-05)


### Bug Fixes

* **ci:** ensure version tags build both API and UI images [AI-assisted] refs [#234](https://github.com/paulmfischer/MediaSet/issues/234) ([#237](https://github.com/paulmfischer/MediaSet/issues/237)) ([51a27e7](https://github.com/paulmfischer/MediaSet/commit/51a27e762d3f32928b3ea4e1603ca57470396786))
* **ci:** use job-level conditional instead of duplicate push triggers [AI-assisted] refs [#234](https://github.com/paulmfischer/MediaSet/issues/234) ([#239](https://github.com/paulmfischer/MediaSet/issues/239)) ([7ea23a5](https://github.com/paulmfischer/MediaSet/commit/7ea23a558c9f5adde404d1e84f3748b8ba159b75))

## [0.3.0](https://github.com/paulmfischer/MediaSet/compare/v0.2.0...v0.3.0) (2025-11-05)


### Features

* **ci:** enhance PR checks to detect changes in API and UI files ([#226](https://github.com/paulmfischer/MediaSet/issues/226)) ([5efdcfb](https://github.com/paulmfischer/MediaSet/commit/5efdcfb4a424fd7dec8e3e07c374913686b811c9))


### Bug Fixes

* **ci:** add missing release-please manifest file [AI-assisted] ([#221](https://github.com/paulmfischer/MediaSet/issues/221)) ([1a1b128](https://github.com/paulmfischer/MediaSet/commit/1a1b12847d167ed58ab87b06bae22342ca0d7d0e))
* **ci:** remove package-name to create v* tags instead of mediaset-v* [AI-assisted] fixes [#234](https://github.com/paulmfischer/MediaSet/issues/234) ([#235](https://github.com/paulmfischer/MediaSet/issues/235)) ([14da54c](https://github.com/paulmfischer/MediaSet/commit/14da54c7aaf20da554db25e024917ce8a0ebbf58))
* **ci:** update release-please action and fix warnings [AI-assisted] ([#219](https://github.com/paulmfischer/MediaSet/issues/219)) ([49df55e](https://github.com/paulmfischer/MediaSet/commit/49df55e1def404f0e644cb1c4a9c29511adcaaa8))


### Documentation

* update commit message attribution guidelines to include issue references closes [#228](https://github.com/paulmfischer/MediaSet/issues/228) ([#230](https://github.com/paulmfischer/MediaSet/issues/230)) ([c1373d0](https://github.com/paulmfischer/MediaSet/commit/c1373d068d022bcf3fe6ae1604bd02cc59aff06a))


### Miscellaneous

* **ci:** force initial release as 0.1.0 and pre-major behavior [AI-assisted]\n\n- Set release-as: 0.1.0 for the first release\n- Enable bump-minor-pre-major to avoid jumping to 1.0.0 before 1.0\n\nCo-authored-by: GitHub Copilot &lt;copilot@github.com&gt; ([#223](https://github.com/paulmfischer/MediaSet/issues/223)) ([55fca77](https://github.com/paulmfischer/MediaSet/commit/55fca7796b65bc666218764e2bafac6b6820b483))
* **ci:** remove release-as config after initial 0.1.0 release [AI-assisted] ([#224](https://github.com/paulmfischer/MediaSet/issues/224)) ([7ffc8b6](https://github.com/paulmfischer/MediaSet/commit/7ffc8b6fb3f5244ff6b6c3a9adb9695cf9cced15))
* **main:** release mediaset 0.1.0 ([#222](https://github.com/paulmfischer/MediaSet/issues/222)) ([b98ef4b](https://github.com/paulmfischer/MediaSet/commit/b98ef4bdc4a204f488ec6a18e6ff6aa3930e0caf))
* **main:** release mediaset 0.2.0 ([#225](https://github.com/paulmfischer/MediaSet/issues/225)) ([d915eab](https://github.com/paulmfischer/MediaSet/commit/d915eab86aa99d08d64a0db4436eb605e0a5ba41))
* update copilot instructions with branch protection and code style guidelines [AI-assisted] ([#195](https://github.com/paulmfischer/MediaSet/issues/195)) ([2930a47](https://github.com/paulmfischer/MediaSet/commit/2930a477213835e41deac48f6d350d6b3c12416b))

## [0.2.0](https://github.com/paulmfischer/MediaSet/compare/mediaset-v0.1.0...mediaset-v0.2.0) (2025-11-05)


### Features

* **ci:** enhance PR checks to detect changes in API and UI files ([#226](https://github.com/paulmfischer/MediaSet/issues/226)) ([5efdcfb](https://github.com/paulmfischer/MediaSet/commit/5efdcfb4a424fd7dec8e3e07c374913686b811c9))


### UI Enhancements

* **Home Dashboard Redesign** ([#231](https://github.com/paulmfischer/MediaSet/pull/231))
  - Modernized home dashboard layout and visual design
  - Improved statistics display and user experience
  - Enhanced responsive design for better mobile support


### API Enhancements

* **Health Endpoint Versioning** ([#227](https://github.com/paulmfischer/MediaSet/pull/227))
  - Added version field to health endpoint response
  - Returns application version from assembly info for better observability


### Documentation

* update commit message attribution guidelines to include issue references closes [#228](https://github.com/paulmfischer/MediaSet/issues/228) ([#230](https://github.com/paulmfischer/MediaSet/issues/230)) ([c1373d0](https://github.com/paulmfischer/MediaSet/commit/c1373d068d022bcf3fe6ae1604bd02cc59aff06a))


### Miscellaneous

* **ci:** remove release-as config after initial 0.1.0 release [AI-assisted] ([#224](https://github.com/paulmfischer/MediaSet/issues/224)) ([7ffc8b6](https://github.com/paulmfischer/MediaSet/commit/7ffc8b6fb3f5244ff6b6c3a9adb9695cf9cced15))
* **VS Code Configuration** ([#231](https://github.com/paulmfischer/MediaSet/pull/231))
  - Added `.vscode/settings.json` to `.gitignore`
  - Prevents personal IDE configurations from being committed
  - Reduces merge conflicts from developer-specific settings

## 0.1.0 (2025-11-04)


### Bug Fixes

* **ci:** add missing release-please manifest file [AI-assisted] ([#221](https://github.com/paulmfischer/MediaSet/issues/221)) ([1a1b128](https://github.com/paulmfischer/MediaSet/commit/1a1b12847d167ed58ab87b06bae22342ca0d7d0e))
* **ci:** update release-please action and fix warnings [AI-assisted] ([#219](https://github.com/paulmfischer/MediaSet/issues/219)) ([49df55e](https://github.com/paulmfischer/MediaSet/commit/49df55e1def404f0e644cb1c4a9c29511adcaaa8))


### Miscellaneous

* **ci:** force initial release as 0.1.0 and pre-major behavior [AI-assisted]\n\n- Set release-as: 0.1.0 for the first release\n- Enable bump-minor-pre-major to avoid jumping to 1.0.0 before 1.0\n\nCo-authored-by: GitHub Copilot &lt;copilot@github.com&gt; ([#223](https://github.com/paulmfischer/MediaSet/issues/223)) ([55fca77](https://github.com/paulmfischer/MediaSet/commit/55fca7796b65bc666218764e2bafac6b6820b483))
* update copilot instructions with branch protection and code style guidelines [AI-assisted] ([#195](https://github.com/paulmfischer/MediaSet/issues/195)) ([2930a47](https://github.com/paulmfischer/MediaSet/commit/2930a477213835e41deac48f6d350d6b3c12416b))
