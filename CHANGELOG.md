# Changelog

## [3.0.0](https://github.com/paulmfischer/MediaSet/compare/v2.5.0...v3.0.0) (2026-02-03)


### ⚠ BREAKING CHANGES

* **license:** switch project to Apache-2.0 and add NOTICE ([#461](https://github.com/paulmfischer/MediaSet/issues/461))

### Features

* **api,ui:** hide barcode lookup when integrations not configured closes [#457](https://github.com/paulmfischer/MediaSet/issues/457) ([#469](https://github.com/paulmfischer/MediaSet/issues/469)) ([a23ab94](https://github.com/paulmfischer/MediaSet/commit/a23ab945c408872e561808c9d282a356a3209f5f))
* **api:** add background image lookup service closes [#434](https://github.com/paulmfischer/MediaSet/issues/434) ([#467](https://github.com/paulmfischer/MediaSet/issues/467)) ([563edb1](https://github.com/paulmfischer/MediaSet/commit/563edb1fec3058b9d5059b7882c007da72a3a1f2))
* **docker:** add Seq logging configuration and setup documentation ([#463](https://github.com/paulmfischer/MediaSet/issues/463)) ([c2155b9](https://github.com/paulmfischer/MediaSet/commit/c2155b9a0ae60fe99114f2a1e0ae4c6d5671c1f8))
* **license:** switch project to Apache-2.0 and add NOTICE ([#461](https://github.com/paulmfischer/MediaSet/issues/461)) ([bdbf5a3](https://github.com/paulmfischer/MediaSet/commit/bdbf5a3454deb4526afd24df411a1a5578bb7cb5))
* **ui:** add mobile barcode scanning capability closes [#280](https://github.com/paulmfischer/MediaSet/issues/280)  ([#465](https://github.com/paulmfischer/MediaSet/issues/465)) ([f9546a0](https://github.com/paulmfischer/MediaSet/commit/f9546a0553706779a9315bd9da9bda65f7fe3a7d))


### Bug Fixes

* **tests:** suppress bootstrap logger output during tests closes [#466](https://github.com/paulmfischer/MediaSet/issues/466) ([#470](https://github.com/paulmfischer/MediaSet/issues/470)) ([374162b](https://github.com/paulmfischer/MediaSet/commit/374162b07fe1f7b7599f7f40a863b4262110f60b))


### Documentation

* add CLAUDE.md and update backend to .NET 10 ([#468](https://github.com/paulmfischer/MediaSet/issues/468)) ([76b3142](https://github.com/paulmfischer/MediaSet/commit/76b3142bd61bcdefc02aa1b00124cc0a484b1fa7))

## [2.5.0](https://github.com/paulmfischer/MediaSet/compare/v2.4.0...v2.5.0) (2026-01-30)


### Features

* **api:** add distributed tracing with SerilogTracing activity spans ([#449](https://github.com/paulmfischer/MediaSet/issues/449)) ([4002c6e](https://github.com/paulmfischer/MediaSet/commit/4002c6ed9d15c74239059e29a6874a878c30b73e))
* **frontend:** preview image URL on add/edit forms [#433](https://github.com/paulmfischer/MediaSet/issues/433) [AI-assisted] ([#444](https://github.com/paulmfischer/MediaSet/issues/444)) ([09ad9c4](https://github.com/paulmfischer/MediaSet/commit/09ad9c49b28f29a7452c7c44c60b13759d2f6db2))
* **integrations:** add conditional attribution badges + integrations endpoint (refs [#398](https://github.com/paulmfischer/MediaSet/issues/398)) ([#453](https://github.com/paulmfischer/MediaSet/issues/453)) ([e8f4395](https://github.com/paulmfischer/MediaSet/commit/e8f4395ab105335dfdfd7798ddb7862ef67a9c98))
* **logging:** integrate Serilog with Seq for external logging ([#441](https://github.com/paulmfischer/MediaSet/issues/441)) ([db73613](https://github.com/paulmfischer/MediaSet/commit/db7361358980dac82ddf65cdca62b580626acdf4))


### Bug Fixes

* **api:** prevent host bin/obj churn by using named volumes ([#452](https://github.com/paulmfischer/MediaSet/issues/452)) ([27e9edd](https://github.com/paulmfischer/MediaSet/commit/27e9edd495211efc3888f584169e0a787f1e531a))
* **ui:** display full image without cropping in ImageDisplay closes [#443](https://github.com/paulmfischer/MediaSet/issues/443) ([#459](https://github.com/paulmfischer/MediaSet/issues/459)) ([e10b075](https://github.com/paulmfischer/MediaSet/commit/e10b075d1f2e249e1ac1dc7db19c21134653b126))


### Documentation

* **agent:** add sync with main as first workflow step [AI-assisted] ([#448](https://github.com/paulmfischer/MediaSet/issues/448)) ([7cbf3ec](https://github.com/paulmfischer/MediaSet/commit/7cbf3ecda6f63088638bda3e85e842f4b6aef832))
* **readme:** add release and license badges [AI-assisted] ([#456](https://github.com/paulmfischer/MediaSet/issues/456)) ([5b41d10](https://github.com/paulmfischer/MediaSet/commit/5b41d109b33d298f8bd85bd2faed4ae137303da0))
* reference backend and frontend instruction files [AI-assisted] ([#446](https://github.com/paulmfischer/MediaSet/issues/446)) ([c59661f](https://github.com/paulmfischer/MediaSet/commit/c59661f4069a42ce98059c738117e2d051e063de))
* updating readme, setup docs, and dev docs closes [#454](https://github.com/paulmfischer/MediaSet/issues/454) ([#458](https://github.com/paulmfischer/MediaSet/issues/458)) ([0411d5f](https://github.com/paulmfischer/MediaSet/commit/0411d5f87d013febf13eafe3f6c5850ead30a269))


### Refactoring

* **tracing:** implement W3C standard distributed tracing across UI and API ([#450](https://github.com/paulmfischer/MediaSet/issues/450)) ([c672d05](https://github.com/paulmfischer/MediaSet/commit/c672d0522d0283bdf10248cc9f4b89b432e276c0)), closes [#437](https://github.com/paulmfischer/MediaSet/issues/437)


### Miscellaneous

* **docs:** add commit signing requirement to instructions ([#447](https://github.com/paulmfischer/MediaSet/issues/447)) ([021280c](https://github.com/paulmfischer/MediaSet/commit/021280c3ad4783558729103868be2788026b4f5b))

## [2.4.0](https://github.com/paulmfischer/MediaSet/compare/v2.3.0...v2.4.0) (2026-01-26)


### Features

* **config:** add runtime env config with separate server/client API URLs ([#438](https://github.com/paulmfischer/MediaSet/issues/438)) ([885d3cf](https://github.com/paulmfischer/MediaSet/commit/885d3cf5ad9f5e571a54fd7c9b81bd1a0d3d474b))

## [2.3.0](https://github.com/paulmfischer/MediaSet/compare/v2.2.0...v2.3.0) (2026-01-26)


### Features

* **ui:** improve error screen with better layout and actions ([#436](https://github.com/paulmfischer/MediaSet/issues/436)) ([a01fe3b](https://github.com/paulmfischer/MediaSet/commit/a01fe3bb9c84fa98b6ed2328571fd5a93972a392))


### Bug Fixes

* **api:** handle image save failures gracefully closes [#401](https://github.com/paulmfischer/MediaSet/issues/401) ([#435](https://github.com/paulmfischer/MediaSet/issues/435)) ([85caa47](https://github.com/paulmfischer/MediaSet/commit/85caa4764c8a6b1568a33ce9e5eb49d29f61867b))
* **api:** resolve NETSDK1064 OpenAPI package error in Podman dev setup ([#431](https://github.com/paulmfischer/MediaSet/issues/431)) ([b32e132](https://github.com/paulmfischer/MediaSet/commit/b32e132fa89ce6cbb92fe8e26e62e28be81978b3))
* **dev-env:** stabilize podman builds ([#427](https://github.com/paulmfischer/MediaSet/issues/427)) ([0a8d9ec](https://github.com/paulmfischer/MediaSet/commit/0a8d9ec7a1c9993900b2029e856551a840b59044))
* **ui:** close delete dialog after submission closes [#399](https://github.com/paulmfischer/MediaSet/issues/399) ([#430](https://github.com/paulmfischer/MediaSet/issues/430)) ([38decda](https://github.com/paulmfischer/MediaSet/commit/38decdae3c11b3358222632faefa9e3f37dc4705))
* **ui:** form fields not repopulating on subsequent barcode lookups  ([#432](https://github.com/paulmfischer/MediaSet/issues/432)) ([a03f5fb](https://github.com/paulmfischer/MediaSet/commit/a03f5fb166b9f6897859c912cd6e5cc770b11daf))

## [2.2.0](https://github.com/paulmfischer/MediaSet/compare/v2.1.0...v2.2.0) (2026-01-23)


### Features

* **deps:** update frontend dependencies to latest versions ([#425](https://github.com/paulmfischer/MediaSet/issues/425)) ([bfa05df](https://github.com/paulmfischer/MediaSet/commit/bfa05dffd1854269b2397ee4cb3948e461c37040))

## [2.1.0](https://github.com/paulmfischer/MediaSet/compare/v2.0.4...v2.1.0) (2026-01-22)


### Features

* **api:** update to .NET 10 ([#423](https://github.com/paulmfischer/MediaSet/issues/423)) ([e0a6aab](https://github.com/paulmfischer/MediaSet/commit/e0a6aab07286b039044586224c4ef4c76a3ae08d))

## [2.0.4](https://github.com/paulmfischer/MediaSet/compare/v2.0.3...v2.0.4) (2026-01-21)


### Bug Fixes

* **build:** change rollForward to latestFeature for cross-feature-band compatibility ([#419](https://github.com/paulmfischer/MediaSet/issues/419)) ([1d5d678](https://github.com/paulmfischer/MediaSet/commit/1d5d67881af7b8595673279d70a501a15c98c96b))

## [2.0.3](https://github.com/paulmfischer/MediaSet/compare/v2.0.2...v2.0.3) (2026-01-21)


### Miscellaneous

* trigger release after malformed msg ([#417](https://github.com/paulmfischer/MediaSet/issues/417)) ([e60af2b](https://github.com/paulmfischer/MediaSet/commit/e60af2bb879cbd30e483b421d0dc0b7dcd3c6ca5))

## [2.0.2](https://github.com/paulmfischer/MediaSet/compare/v2.0.1...v2.0.2) (2026-01-21)


### Bug Fixes

* **build:** align .NET SDK to 9.0.112 across all environments ([#414](https://github.com/paulmfischer/MediaSet/issues/414)) ([93b0e8b](https://github.com/paulmfischer/MediaSet/commit/93b0e8b072b546e2e0794bac3f0c6100de1dc6a9))

## [2.0.1](https://github.com/paulmfischer/MediaSet/compare/v2.0.0...v2.0.1) (2026-01-20)


### Bug Fixes

* **ui:** fixing search and isbn lookup submits closes [#402](https://github.com/paulmfischer/MediaSet/issues/402) and [#403](https://github.com/paulmfischer/MediaSet/issues/403) ([#409](https://github.com/paulmfischer/MediaSet/issues/409)) ([e93cbcf](https://github.com/paulmfischer/MediaSet/commit/e93cbcf3d27450eb093dc645393620ac0f2cf43f))


### Documentation

* **docker:** update example docker-compose files with complete configurations ([#396](https://github.com/paulmfischer/MediaSet/issues/396)) ([612a3a8](https://github.com/paulmfischer/MediaSet/commit/612a3a840bb3669e94d567d71f5af55e02eb4cbf))

## [2.0.0](https://github.com/paulmfischer/MediaSet/compare/v1.0.9...v2.0.0) (2025-11-19)


### ⚠ BREAKING CHANGES

* **license:** License change to AGPLv3 affects all usage and distribution. release-as: 2.0.0

### Features

* **api:** add Id and fix FileName on Image data closes [#391](https://github.com/paulmfischer/MediaSet/issues/391) ([#393](https://github.com/paulmfischer/MediaSet/issues/393)) ([affadd6](https://github.com/paulmfischer/MediaSet/commit/affadd61f81de6bd713987c83944d47a236a0f80))
* **api:** implement ability to save an image for an entity closes [#357](https://github.com/paulmfischer/MediaSet/issues/357), [#358](https://github.com/paulmfischer/MediaSet/issues/358), [#359](https://github.com/paulmfischer/MediaSet/issues/359), [#360](https://github.com/paulmfischer/MediaSet/issues/360), [#361](https://github.com/paulmfischer/MediaSet/issues/361), [#361](https://github.com/paulmfischer/MediaSet/issues/361) ([#379](https://github.com/paulmfischer/MediaSet/issues/379)) ([4831495](https://github.com/paulmfischer/MediaSet/commit/48314955a33c22fc4acaea4ff4d7e2f028c709a7))
* **api:** strip EXIF data from uploaded and downloaded images closes [#389](https://github.com/paulmfischer/MediaSet/issues/389) ([#395](https://github.com/paulmfischer/MediaSet/issues/395)) ([e88dec5](https://github.com/paulmfischer/MediaSet/commit/e88dec5974b5505eda2f807f72b0076bc258ca28))
* **frontend:** add ability to upload an image to media through the UI ([#380](https://github.com/paulmfischer/MediaSet/issues/380)) ([2a4d343](https://github.com/paulmfischer/MediaSet/commit/2a4d343faccbbe80b571e3774ae18beac50105f8))
* **lookup:** add imageUrl to barcode lookup closes [#369](https://github.com/paulmfischer/MediaSet/issues/369) ([#388](https://github.com/paulmfischer/MediaSet/issues/388)) ([91f42a1](https://github.com/paulmfischer/MediaSet/commit/91f42a150b752de74159f6a44696966d8f20701e))
* **models:** add Image model and CoverImage property closes [#356](https://github.com/paulmfischer/MediaSet/issues/356) [AI-assisted] ([#377](https://github.com/paulmfischer/MediaSet/issues/377)) ([4e18996](https://github.com/paulmfischer/MediaSet/commit/4e189965afdbc9b097a713329d2d2ff642ab3536))
* **ui:** display cover images on list view closes [#390](https://github.com/paulmfischer/MediaSet/issues/390) ([#394](https://github.com/paulmfischer/MediaSet/issues/394)) ([5d8935b](https://github.com/paulmfischer/MediaSet/commit/5d8935bb1c67a0a19cdf472ab72fe75fc4e4e73b))
* **ui:** display images on entity details closes [#367](https://github.com/paulmfischer/MediaSet/issues/367) ([#383](https://github.com/paulmfischer/MediaSet/issues/383)) ([44e810c](https://github.com/paulmfischer/MediaSet/commit/44e810c9503d5d65a4ffbef019305e55d6e37881))


### Bug Fixes

* **api,ui:** resolve image editing and deletion bugs ([#384](https://github.com/paulmfischer/MediaSet/issues/384)) ([3edfa5d](https://github.com/paulmfischer/MediaSet/commit/3edfa5dcad659fe89745fac8fa840a5728de6c08))
* **api:** improve EntityApi multipart form deserialization ([#381](https://github.com/paulmfischer/MediaSet/issues/381)) ([b2bf386](https://github.com/paulmfischer/MediaSet/commit/b2bf386968ae077b9f8ba7f164e1572531b8fd6d))
* **test:** eliminate warnings as part of test project closes [#373](https://github.com/paulmfischer/MediaSet/issues/373) ([#386](https://github.com/paulmfischer/MediaSet/issues/386)) ([e0c93f7](https://github.com/paulmfischer/MediaSet/commit/e0c93f7f9f51ea0428b67ff2a3861b9a4ebd98d3))


### Documentation

* add image storage implementation plan [AI-assisted] refs [#151](https://github.com/paulmfischer/MediaSet/issues/151) ([#375](https://github.com/paulmfischer/MediaSet/issues/375)) ([9255039](https://github.com/paulmfischer/MediaSet/commit/92550397fd150014bcaad5560daa5bb9a520869a))
* update documentation for image storage and API endpoints ([#392](https://github.com/paulmfischer/MediaSet/issues/392)) ([14e536e](https://github.com/paulmfischer/MediaSet/commit/14e536ee17020bd06ea1a0bbeaa66dd53ddee52f))


### Miscellaneous

* **license:** switch to GNU AGPLv3 and update project metadata ([#385](https://github.com/paulmfischer/MediaSet/issues/385)) ([b4995a7](https://github.com/paulmfischer/MediaSet/commit/b4995a76a1108fed302efdf7ab27db07f9e420d2))

## [1.0.9](https://github.com/paulmfischer/MediaSet/compare/v1.0.8...v1.0.9) (2025-11-13)


### Bug Fixes

* **ui:** centralize page-level spinner for all form operations closes [#297](https://github.com/paulmfischer/MediaSet/issues/297) ([#353](https://github.com/paulmfischer/MediaSet/issues/353)) ([7730266](https://github.com/paulmfischer/MediaSet/commit/7730266a3f6a58d703097c98abd34d17238c8eae))
* **ui:** fix mouse selection in dropdown inputs closes [#352](https://github.com/paulmfischer/MediaSet/issues/352) ([#354](https://github.com/paulmfischer/MediaSet/issues/354)) ([6ac6a90](https://github.com/paulmfischer/MediaSet/commit/6ac6a9006f77967631a714228f630c095d7c04af))


### Miscellaneous

* **docs:** consolidate and reorganize copilot instructions ([#350](https://github.com/paulmfischer/MediaSet/issues/350)) ([bfe3dfa](https://github.com/paulmfischer/MediaSet/commit/bfe3dfad32838dfe768f343880e1d57e44dfe419))

## [1.0.8](https://github.com/paulmfischer/MediaSet/compare/v1.0.7...v1.0.8) (2025-11-12)


### Documentation

* add testing documentation and coverage reporting setup closes [#321](https://github.com/paulmfischer/MediaSet/issues/321) ([#348](https://github.com/paulmfischer/MediaSet/issues/348)) ([56cb614](https://github.com/paulmfischer/MediaSet/commit/56cb61427f9294c5c9f6fd8d7628650bb9edebae))
* add UI testing infrastructure implementation plan [AI-assisted] refs [#249](https://github.com/paulmfischer/MediaSet/issues/249) ([#323](https://github.com/paulmfischer/MediaSet/issues/323)) ([a8a5be8](https://github.com/paulmfischer/MediaSet/commit/a8a5be80816e1a6ff39a7bbc47674e9ce4e9bb8e))


### Tests

* **ui:** add badge.tsx component tests closes [#312](https://github.com/paulmfischer/MediaSet/issues/312) [AI-assisted] ([#339](https://github.com/paulmfischer/MediaSet/issues/339)) ([6bd1688](https://github.com/paulmfischer/MediaSet/commit/6bd16884b78e60d8b7a977b826594420c35c5eea))
* **ui:** Add book-form.tsx component tests closes [#305](https://github.com/paulmfischer/MediaSet/issues/305) ([#331](https://github.com/paulmfischer/MediaSet/issues/331)) ([146e5d2](https://github.com/paulmfischer/MediaSet/commit/146e5d2163bd8f8fc40d29c2d5520ab03d9d174c))
* **ui:** add delete-dialog.tsx component tests closes [#311](https://github.com/paulmfischer/MediaSet/issues/311) ([#338](https://github.com/paulmfischer/MediaSet/issues/338)) ([37d9919](https://github.com/paulmfischer/MediaSet/commit/37d991918a9e33be35f8e89289f3886d43e86fd2))
* **ui:** add entity add/create route tests ([#345](https://github.com/paulmfischer/MediaSet/issues/345)) ([2757c76](https://github.com/paulmfischer/MediaSet/commit/2757c76d46c9f072269bc5080557d6ae65e27152))
* **ui:** Add entity delete route tests closes [#320](https://github.com/paulmfischer/MediaSet/issues/320) ([#347](https://github.com/paulmfischer/MediaSet/issues/347)) ([d86a60e](https://github.com/paulmfischer/MediaSet/commit/d86a60ec18f6a40d746d70bd1d8ec41e5b6fbd5c))
* **ui:** Add entity edit route tests closes [#319](https://github.com/paulmfischer/MediaSet/issues/319) ([#346](https://github.com/paulmfischer/MediaSet/issues/346)) ([f75ce32](https://github.com/paulmfischer/MediaSet/commit/f75ce32928466fd752f94079ab056b75cf2a0c7b))
* **ui:** Add entity list routes tests ([#344](https://github.com/paulmfischer/MediaSet/issues/344)) ([8a19891](https://github.com/paulmfischer/MediaSet/commit/8a1989173b1a5a09ed9e215bbee88e60befc8865)), closes [#317](https://github.com/paulmfischer/MediaSet/issues/317)
* **ui:** Add entity-data.ts tests closes [#302](https://github.com/paulmfischer/MediaSet/issues/302) ([#328](https://github.com/paulmfischer/MediaSet/issues/328)) ([fb865e1](https://github.com/paulmfischer/MediaSet/commit/fb865e1eb1a2e816c0cf69a3f3020b5325ea0de6))
* **ui:** Add game-form.tsx component tests ([#332](https://github.com/paulmfischer/MediaSet/issues/332)) ([151a485](https://github.com/paulmfischer/MediaSet/commit/151a48529a0f38454be3f6abe5fe3ecb783c6b3d))
* **ui:** Add helpers.ts unit tests closes [#300](https://github.com/paulmfischer/MediaSet/issues/300) ([#326](https://github.com/paulmfischer/MediaSet/issues/326)) ([379d6bf](https://github.com/paulmfischer/MediaSet/commit/379d6bf9ebe3917666de771c3445a5f5ad3c8cdc))
* **ui:** Add home page (_index) route tests closes [#316](https://github.com/paulmfischer/MediaSet/issues/316) ([#343](https://github.com/paulmfischer/MediaSet/issues/343)) ([ff9897a](https://github.com/paulmfischer/MediaSet/commit/ff9897a30986512bd3cf64535b1644c9013e926f))
* **ui:** add metadata-data.ts tests closes [#303](https://github.com/paulmfischer/MediaSet/issues/303) ([#329](https://github.com/paulmfischer/MediaSet/issues/329)) ([14ea837](https://github.com/paulmfischer/MediaSet/commit/14ea83765f55ebc28265e38829aa2284a3873ef2))
* **ui:** add models.ts type validation tests closes [#301](https://github.com/paulmfischer/MediaSet/issues/301)  ([#327](https://github.com/paulmfischer/MediaSet/issues/327)) ([5cbc45b](https://github.com/paulmfischer/MediaSet/commit/5cbc45ba9110970d1bf848da91f28f35aed976db))
* **ui:** Add movie-form.tsx component tests closes [#307](https://github.com/paulmfischer/MediaSet/issues/307) ([#333](https://github.com/paulmfischer/MediaSet/issues/333)) ([abb89bc](https://github.com/paulmfischer/MediaSet/commit/abb89bce26e1ef8cb5fe896b0899927c36e57f9f))
* **ui:** add multiselect-input.tsx component tests closes [#310](https://github.com/paulmfischer/MediaSet/issues/310)  ([#337](https://github.com/paulmfischer/MediaSet/issues/337)) ([4ff4f50](https://github.com/paulmfischer/MediaSet/commit/4ff4f508ef0590fc71aeaa7e1a204a54df199cc9))
* **ui:** Add music-form.tsx component tests closes [#308](https://github.com/paulmfischer/MediaSet/issues/308) ([#335](https://github.com/paulmfischer/MediaSet/issues/335)) ([49ce7b9](https://github.com/paulmfischer/MediaSet/commit/49ce7b98368a3709b96d12ac7b4fa2a299ed8fb2))
* **ui:** add pending-navigation.tsx component tests closes [#315](https://github.com/paulmfischer/MediaSet/issues/315) ([#342](https://github.com/paulmfischer/MediaSet/issues/342)) ([4b244f5](https://github.com/paulmfischer/MediaSet/commit/4b244f5a991d1069c6f2eebec3f1ece24c76698c))
* **ui:** Add singleselect-input.tsx component tests closes [#309](https://github.com/paulmfischer/MediaSet/issues/309) ([#336](https://github.com/paulmfischer/MediaSet/issues/336)) ([9f0edd1](https://github.com/paulmfischer/MediaSet/commit/9f0edd1ee99806ac4f1e74e0776f9453daa8daed))
* **ui:** Add StatCard.tsx component tests closes [#314](https://github.com/paulmfischer/MediaSet/issues/314) ([#341](https://github.com/paulmfischer/MediaSet/issues/341)) ([c99961f](https://github.com/paulmfischer/MediaSet/commit/c99961f3388b9084f95b98baa7dfe0d65c9304cc))
* **ui:** add stats-data.ts tests closes [#304](https://github.com/paulmfischer/MediaSet/issues/304) ([#330](https://github.com/paulmfischer/MediaSet/issues/330)) ([d1b5b48](https://github.com/paulmfischer/MediaSet/commit/d1b5b48e4af9d18d6a883a766b67b12863789e19))
* **ui:** Consolidate spinner component tests closes [#313](https://github.com/paulmfischer/MediaSet/issues/313) ([#340](https://github.com/paulmfischer/MediaSet/issues/340)) ([7c31c3c](https://github.com/paulmfischer/MediaSet/commit/7c31c3ca14986861f132dda632332a0ba15a4c51))
* **ui:** setup Vitest, React Testing Library, and test utilities [AI-assisted] ([#325](https://github.com/paulmfischer/MediaSet/issues/325)) ([1e4ca6c](https://github.com/paulmfischer/MediaSet/commit/1e4ca6c81b584537d25244254aeb41fba7c1951a)), closes [#298](https://github.com/paulmfischer/MediaSet/issues/298)


### Performance

* **test:** optimizing some test runs closes [#249](https://github.com/paulmfischer/MediaSet/issues/249) ([#349](https://github.com/paulmfischer/MediaSet/issues/349)) ([9fd5464](https://github.com/paulmfischer/MediaSet/commit/9fd5464d4cf850504a67d73eb5b6cbcf5ed9fa96))

## [1.0.7](https://github.com/paulmfischer/MediaSet/compare/v1.0.6...v1.0.7) (2025-11-09)


### Bug Fixes

* **docker:** copy .git directory and pass MinVerVersion to MSBuild closes [#281](https://github.com/paulmfischer/MediaSet/issues/281) ([#295](https://github.com/paulmfischer/MediaSet/issues/295)) ([30302df](https://github.com/paulmfischer/MediaSet/commit/30302df38597b91dc70cd1886a742829c506c1ce))

## [1.0.6](https://github.com/paulmfischer/MediaSet/compare/v1.0.5...v1.0.6) (2025-11-09)


### Bug Fixes

* **docker:** use environment variable for MinVer version refs [#281](https://github.com/paulmfischer/MediaSet/issues/281)  ([#293](https://github.com/paulmfischer/MediaSet/issues/293)) ([8ef63e5](https://github.com/paulmfischer/MediaSet/commit/8ef63e50dffc3bbc8e86c0f15733a1d1954a9322))

## [1.0.5](https://github.com/paulmfischer/MediaSet/compare/v1.0.4...v1.0.5) (2025-11-09)


### Bug Fixes

* **docker:** simplify minver version handling refs [#281](https://github.com/paulmfischer/MediaSet/issues/281) ([#291](https://github.com/paulmfischer/MediaSet/issues/291)) ([76bbe31](https://github.com/paulmfischer/MediaSet/commit/76bbe31959cd26d41b743043682dc4b9a62215fe))

## [1.0.4](https://github.com/paulmfischer/MediaSet/compare/v1.0.3...v1.0.4) (2025-11-09)


### Bug Fixes

* **docker:** wrap shell conditional in /bin/sh for compatibility refs [#281](https://github.com/paulmfischer/MediaSet/issues/281) ([#289](https://github.com/paulmfischer/MediaSet/issues/289)) ([0639934](https://github.com/paulmfischer/MediaSet/commit/0639934ff1d83130b21835bb7801298a64341dee))

## [1.0.3](https://github.com/paulmfischer/MediaSet/compare/v1.0.2...v1.0.3) (2025-11-09)


### Bug Fixes

* **docker:** pass version via build arg instead of copying .git refs [#281](https://github.com/paulmfischer/MediaSet/issues/281) ([#287](https://github.com/paulmfischer/MediaSet/issues/287)) ([83f0ed1](https://github.com/paulmfischer/MediaSet/commit/83f0ed1151970c7ee2c059095397bc6a583bfe05))

## [1.0.2](https://github.com/paulmfischer/MediaSet/compare/v1.0.1...v1.0.2) (2025-11-09)


### Bug Fixes

* **docker:** include .git directory for MinVer versioning in production builds closes [#281](https://github.com/paulmfischer/MediaSet/issues/281) ([#285](https://github.com/paulmfischer/MediaSet/issues/285)) ([fa88290](https://github.com/paulmfischer/MediaSet/commit/fa88290edf91b85af204f568c4e7354e0bd1983d))

## [1.0.1](https://github.com/paulmfischer/MediaSet/compare/v1.0.0...v1.0.1) (2025-11-09)


### Bug Fixes

* **versioning:** configure displaying version correctly in api/ui closes [#281](https://github.com/paulmfischer/MediaSet/issues/281) ([#284](https://github.com/paulmfischer/MediaSet/issues/284)) ([cede81b](https://github.com/paulmfischer/MediaSet/commit/cede81bd352dee5ebe53e2ed5fc99c49bb0de9e6))


### Miscellaneous

* restructure copilot instructions to follow VS Code standards [AI-assisted] ([#282](https://github.com/paulmfischer/MediaSet/issues/282)) ([5322e34](https://github.com/paulmfischer/MediaSet/commit/5322e3488e263eaed9eb58b1195d1074937d8e4f))

## [1.0.0](https://github.com/paulmfischer/MediaSet/compare/v0.5.0...v1.0.0) (2025-11-08)


### Miscellaneous

* **docs:** move and update markdown references [AI-assisted] closes [#276](https://github.com/paulmfischer/MediaSet/issues/276) ([#278](https://github.com/paulmfischer/MediaSet/issues/278)) ([c7940bf](https://github.com/paulmfischer/MediaSet/commit/c7940bf931e5ce532e0e8fce8cff85e2eee6b2a2))

## [0.5.0](https://github.com/paulmfischer/MediaSet/compare/v0.4.3...v0.5.0) (2025-11-08)


### Features

* **ui:** improve entity list mobile layout [AI-assisted] closes [#253](https://github.com/paulmfischer/MediaSet/issues/253) ([#271](https://github.com/paulmfischer/MediaSet/issues/271)) ([0bf9077](https://github.com/paulmfischer/MediaSet/commit/0bf90775c6d019f4869ce6092c0f6e84644fece8))
* **ui:** replace browser confirm with HTML dialog [AI-assisted] closes [#267](https://github.com/paulmfischer/MediaSet/issues/267) ([#269](https://github.com/paulmfischer/MediaSet/issues/269)) ([8c6da56](https://github.com/paulmfischer/MediaSet/commit/8c6da566fe5ff32d0f48e97fb054fdca5ff09a08))


### Bug Fixes

* **api:** correct HttpClient configuration pattern closes [#204](https://github.com/paulmfischer/MediaSet/issues/204) ([#275](https://github.com/paulmfischer/MediaSet/issues/275)) ([da8f27a](https://github.com/paulmfischer/MediaSet/commit/da8f27adefa0b1d44f3bb846f5605df2d41aa15c))
* **api:** fix game clean title to remove extra info closes [#272](https://github.com/paulmfischer/MediaSet/issues/272) ([#273](https://github.com/paulmfischer/MediaSet/issues/273)) ([b5fb20c](https://github.com/paulmfischer/MediaSet/commit/b5fb20c5773595467107c72bf36153a77d6c8a23))
* **ui:** close select dropdown on Tab/blur [AI-assisted] closes [#268](https://github.com/paulmfischer/MediaSet/issues/268) ([#270](https://github.com/paulmfischer/MediaSet/issues/270)) ([7c5d10a](https://github.com/paulmfischer/MediaSet/commit/7c5d10aa567f746cfec035f01433fc2b5055788e))


### Miscellaneous

* **chatmodes:** add expert remix chatmode [AI-assisted] closes [#255](https://github.com/paulmfischer/MediaSet/issues/255) ([#264](https://github.com/paulmfischer/MediaSet/issues/264)) ([7faace3](https://github.com/paulmfischer/MediaSet/commit/7faace3722864a7e9d673b9aa617be6ff6a4e6a8))


### Code Style

* **ui:** adjust button colors to fit theme [AI-assisted] closes [#252](https://github.com/paulmfischer/MediaSet/issues/252) ([#266](https://github.com/paulmfischer/MediaSet/issues/266)) ([9a9ef91](https://github.com/paulmfischer/MediaSet/commit/9a9ef91e37997576cd186b208becb9ce4beb0a1a))

## [0.4.3](https://github.com/paulmfischer/MediaSet/compare/v0.4.2...v0.4.3) (2025-11-06)


### Bug Fixes

* **ci:** use GitHub App for verified release-please commits refs [#261](https://github.com/paulmfischer/MediaSet/issues/261) ([#262](https://github.com/paulmfischer/MediaSet/issues/262)) ([992af7a](https://github.com/paulmfischer/MediaSet/commit/992af7aa83559e864670c6a4f14bfddbfce01d79))

## [0.4.2](https://github.com/paulmfischer/MediaSet/compare/v0.4.1...v0.4.2) (2025-11-06)


### Bug Fixes

* **ci:** use tags trigger only for docker workflows refs [#245](https://github.com/paulmfischer/MediaSet/issues/245) ([#259](https://github.com/paulmfischer/MediaSet/issues/259)) ([6f8ada6](https://github.com/paulmfischer/MediaSet/commit/6f8ada61d76d2d67c69982fe7df4d9ba26421b6e))

## [0.4.1](https://github.com/paulmfischer/MediaSet/compare/v0.4.0...v0.4.1) (2025-11-06)


### Bug Fixes

* **ci:** use PAT and semver tags to trigger docker workflows refs [#245](https://github.com/paulmfischer/MediaSet/issues/245) ([#257](https://github.com/paulmfischer/MediaSet/issues/257)) ([19cc7c3](https://github.com/paulmfischer/MediaSet/commit/19cc7c3367412151b82f918d02d50158c13a6ffd))

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
