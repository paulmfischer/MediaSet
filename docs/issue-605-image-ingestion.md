# Image ingestion replacement — issue #605

## Implementation rationale

SkiaSharp fits the JPEG/PNG workload and uses the [MIT license](https://github.com/mono/SkiaSharp/blob/v4.153.1/LICENSE.md).
Magick.NET's [Apache-2.0 license](https://github.com/dlemstra/Magick.NET/blob/main/License.txt) was also suitable,
but its additional format support is unnecessary here. The managed ImageSharp dependency required replacement
to remove the existing audit suppressions and avoid its newer licensing constraints.

The exact matching packages are `SkiaSharp` **4.153.1** and
`SkiaSharp.NativeAssets.Linux.NoDependencies` **4.153.1**. Both official NuGet pages identified this as the
newest stable version during verification on October 25, 2026; newer versions were prereleases:
[managed package](https://www.nuget.org/packages/SkiaSharp/4.153.1),
[Linux native package](https://www.nuget.org/packages/SkiaSharp.NativeAssets.Linux.NoDependencies/4.153.1).
The [official release](https://mono.github.io/SkiaSharp/docs/releases/4.153.1.html) disables RAW/DNG decoding.
The downloaded native package contains both `runtimes/linux-x64/native/libSkiaSharp.so` and
`runtimes/linux-arm64/native/libSkiaSharp.so`; the deployment target remains amd64.

Uploads and URL downloads now share one pipeline. Its actual JPEG/PNG format determines MIME type and the
first corresponding configured extension. Valid image bytes with incorrect MIME types or filenames are accepted.
Unidentifiable, unsupported, incomplete, and corrupt data are rejected before storage, including when metadata
stripping is disabled. Complete JPEG EOI and PNG IEND/container bounds are checked because a decoder may
successfully produce scanlines from an incomplete container. Existing URL redirect/SSRF checks and compressed-byte
limits remain in place.

`ImageConfiguration.MaxPixelCount` defaults to 25,000,000, must be positive, and is checked using 64-bit
width-times-height arithmetic before pixel allocation. The normal 32-bit bitmap alone can occupy about 100 MB
at the default limit; this is not a total process-memory limit. Metadata stripping encodes a fresh PNG or a
quality-75 JPEG from the decoded bitmap. PNG alpha is retained. Disabling stripping keeps validated original
bytes unchanged. Existing stored images require no migration.

## Security review

An empty maintainer advisory page was not used as the security gate. Checks also covered the
[GitHub advisory database](https://github.com/advisories/GHSA-j7hp-h8jx-5ppr), native codec upstream notices,
and dependency versions in the downloaded Linux binary:

- The historical libwebp CVE-2023-4863 advisory marks SkiaSharp versions below 2.88.6 as affected;
  the selected 4.153.1 is outside that range. This pipeline rejects WebP before pixel decoding.
- Binary strings identify **libpng 1.6.58**. The [libpng upstream notices](https://www.libpng.org/pub/png/libpng.html)
  identify high-severity CVE-2026-25646 as fixed in 1.6.55, CVE-2026-33416 and CVE-2026-33636 as fixed in
  1.6.56, and the later setter regression CVE-2026-34757 as fixed in 1.6.57. The bundled version exceeds these.
- Binary strings identify **libjpeg-turbo 3.1.4.1**. Its [release notes](https://github.com/libjpeg-turbo/libjpeg-turbo/releases/tag/3.1.4.1)
  include the lossless/scaled-decode buffer-overrun correction from 3.1.4. The subsequently reported
  [CVE-2026-75466 issue](https://github.com/libjpeg-turbo/libjpeg-turbo/issues/911) concerns the TurboJPEG
  PNG file-loading API in 3.2.x, rather than JPEG decoding through Skia's libjpeg interface.
- Skia's [dependency refresh notes](https://mono.github.io/SkiaSharp/docs/releases/4.147.0.html) document
  refreshed libpng, libjpeg-turbo, zlib, libexpat and FreeType dependencies in the v4 line.

No known high/critical advisory affecting the selected JPEG/PNG stack was identified in these checks.
This is a dated assessment; retain regular NuGet and native upstream security monitoring.

## Verification record

The implementation includes real JPEG/PNG fixtures and covers upload/download normalization, configured
format restrictions, pixel counts below/at/above the limit, overflow-sized dimensions, malformed/truncated
payloads, cancellation, metadata removal, PNG alpha, byte preservation and rejected-storage assertions.

- Full backend suite: **771 passed, 1 skipped, 772 total**.
- Focused ingestion/storage suite after blind-review fixes: **61 passed**.
- Release build: **0 warnings, 0 errors**.
- Forced no-cache restore with `NuGetAudit=true` and `NuGetAuditMode=all`: completed without warnings.
- Explicit transitive vulnerability listing additionally reported the pre-existing **moderate**
  [SharpCompress GHSA-6c8g-7p36-r338](https://github.com/advisories/GHSA-6c8g-7p36-r338) in version 0.30.1,
  brought into the production API by `MongoDB.Driver` 3.0.0. This is unrelated to the image-library replacement
  and was not suppressed or upgraded here. Therefore the broader original criterion of no vulnerable packages
  anywhere in the dependency graph is not met; no image-stack high/critical advisory was identified.
  Follow-up: [#633](https://github.com/paulmfischer/MediaSet/issues/633).
- Two independent code reviews completed; the second reported no blocking findings. The first identified an
  exact-byte-limit stream boundary issue and an overly narrow cancellation assertion, both fixed with regressions.
- Existing production Dockerfile build succeeded as `localhost/mediaset-605:verify`.
  The verification image is **1,225,788,231 bytes**, versus **1,144,674,553 bytes** for the already-present
  `mediaset_api` image (81,113,678 bytes larger, about 7.1%). The existing image has different source/build
  provenance and an empty configured user, so this is an observed local size comparison rather than a controlled
  dependency-only measurement.
- The disposable container uses the image's `mediaset` user and binds only `127.0.0.1:16055`, reusing the existing
  local development MongoDB. Minimal provider-free configuration exposed a pre-existing route-registration
  dependency: `LookupStrategyFactory` is registered only when a provider configuration section exists. Supplying
  the non-secret verification-only `OpenLibraryConfiguration__ContactEmail=verification@example.invalid`
  enables registration for the smoke run; no lookup calls are made and background lookup stays disabled.

Disposable non-root production-container HTTP checks passed using the existing development MongoDB. Only the smoke
test's own entities and stored images were removed afterward:

| Flow | Observed time |
| --- | --- |
| Tiny PNG upload and retrieval with wrong MIME/filename | 43.94 ms |
| Tiny JPEG upload and retrieval with wrong MIME/filename | 12.13 ms |
| 1 MP PNG upload and retrieval (6,213 input bytes), three samples | 71.03, 74.89, 74.30 ms |
| 1 MP JPEG upload and retrieval (16,504 input bytes), three samples | 32.19, 30.35, 25.41 ms |
| Public GitHub PNG URL download and retrieval | 239.41 ms |
| Public GitHub JPEG URL download and retrieval | 104.07 ms |
| PNG URL served as `text/plain`, normalized to `image/png`, and retrieved | 268.79 ms |
| JPEG URL served as `text/plain`, normalized to `image/jpeg`, and retrieved | 54.25 ms |

The verification container used a 1,000,000-pixel limit. The 1 MP samples demonstrate equality is accepted;
a 1,002,001-pixel PNG was rejected with no cover persisted. PNG transparency was retained. These timings include
HTTP, MongoDB, storage and retrieval overhead and are illustrative samples, not isolated codec benchmarks.
An additional independent disposable API on loopback port 16056 used a six-pixel limit. Downloading the known
128×128 public GitHub PNG (16,384 pixels) persisted no cover, and its logs explicitly reported
`Image dimensions exceed maximum allowed pixel count of 6`. This verifies URL-path pixel rejection in the production
container as well as the upload-path rejection above.
Container memory snapshots from `podman stats` were **71.33 MB** before representative samples and **84.5 MB**
afterward (13.17 MB increase). These are sampled container memory values, not a peak working-set measurement or a
bound on concurrent requests. Ordinary URL flows used publicly available Pillow fixtures on GitHub. Additional URL
mismatch checks successfully used the [documented httpbin.io base64 endpoint](https://httpbin.io/) with URL-safe base64
for valid 3×2 PNG/JPEG bytes. Both responses declared `text/plain`; persisted metadata, filename extensions, retrieved
MIME types, image signatures and dimensions all matched the actual image formats. Earlier source attempts using
httpbingo (HTTP 402) and httpbin.org (undecodable PNG response or a slash-routing JPEG 404) were replaced by this
successful binary-safe source. There is no remaining container URL MIME-mismatch verification gap.

Production deployment is outside this change.
