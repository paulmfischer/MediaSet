# Barcode Scanning (Mobile + Browser) Implementation

Status: Draft

Purpose
-------
This document describes a practical, web-first implementation approach to add barcode scanning capability when adding or editing items in MediaSet. The goal is to let users scan barcodes using phone cameras (or desktop webcams) in the browser without a native mobile app, with robust fallbacks and good UX.

Recommendation (short)
----------------------
- Implement a client-side scanner component in the Remix frontend that uses the native `BarcodeDetector` when present, falls back to a JavaScript decoder (e.g., `html5-qrcode` or `@zxing/library`) fed from `getUserMedia`, and offers a file-input (`accept="image/*" capture="environment"`) fallback for iOS Safari and devices without camera access.
- Offer the scanner as a modal/overlay accessible from add/edit item forms. Decode on-device and send only the barcode string to existing lookup endpoints.

Why this approach
------------------
- Maximizes compatibility across Android/iOS/desktop.
- Uses fast native APIs where available for better performance and lower CPU.
- Keeps user data local (privacy) and avoids sending images to the server.

Options considered
------------------
- Web-only (recommended): `getUserMedia` + `BarcodeDetector` + JS fallback + file-input fallback.
- PWA: same code, installed PWA improves UX (home-screen, standalone window) and sometimes permission behavior.
- Deep-link to external scanner app: fragile and platform-dependent — not recommended.
- Native mobile app wrapper: highest fidelity but high cost — out of scope.

Browser / Platform Capabilities
------------------------------
- Camera: `navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } })` — supported by modern browsers on secure contexts.
- Native barcode detection: `BarcodeDetector` API (Chromium-based browsers, Android WebView). Not available on iOS Safari.
- File-capture fallback: `<input type="file" accept="image/*" capture="environment">` — reliable on iOS for capturing photos.
- Torch control: supported via MediaStreamTrack capabilities (`track.getCapabilities()` and `track.applyConstraints(...)`) on devices that expose it.

Firefox-specific notes
----------------------
- `BarcodeDetector` is generally not available in Firefox as of current stable releases; plan to always use a JS fallback there.
- `navigator.mediaDevices.getUserMedia` is supported in Firefox Desktop and Android Firefox (secure context required). On iOS Firefox, browsers use WebKit under the hood and therefore inherit Safari's camera support limitations.
- Torch support in Firefox is limited; use feature detection (`track.getCapabilities().torch`) and offer UI fallbacks when unavailable.


Library choices & tradeoffs
---------------------------
- Native `BarcodeDetector`: fastest, low CPU; use when available.
- `html5-qrcode`: easy to plug-in, actively maintained, built for streaming camera input.
- `@zxing/library` (ZXing JS): robust format support, widely used.
- `QuaggaJS`: older; okay for 1D barcodes but less maintained.
- Commercial (Scandit, Dynamsoft): best-in-class accuracy + price; consider only if high accuracy is required.

Supported formats
-----------------
Prioritize EAN-13, UPC-A, UPC-E, EAN-8, Code 128, Code 39. Configure both native and JS decoders to prefer these.

UX flows
---------
- Trigger: `Scan` button/icon beside the barcode input on add/edit forms.
- Modal scanner overlay with:
  - Live camera preview.
  - Visual guide/target box and hint text.
  - Torch toggle (if available).
  - File upload button (fallback for iOS or permission-denied cases).
  - Manual entry field and Cancel button.
- On detection: show a small confirmation/preview with the scanned value and run lookup (client -> backend) to show match preview; allow Accept / Retry.

Frontend Implementation (detailed)
---------------------------------
1. Add a client-only React component `BarcodeScanner` in the Remix app. Responsibilities:
   - Request camera stream with `facingMode: 'environment'`.
   - Detect `BarcodeDetector` support; if available, use it.
   - If not, initialize fallback (`html5-qrcode` or ZXing) to decode frames.
   - Provide `onDetected(barcode: string)` callback to parent form.
   - Provide file-input fallback: `<input type="file" accept="image/*" capture="environment">` and a decoding path for image files.
   - Support torch toggling when the active `MediaStreamTrack` exposes `torch` capability.
   - Release camera and resources on close.

2. Integrate the component into add/edit pages:
   - Add a `Scan` button next to barcode input that opens the scanner modal.
   - When `onDetected` is called, populate the input and optionally auto-trigger a lookup preview.

3. Implementation details & snippets (conceptual):
- Native `BarcodeDetector` usage
  ```js
  if ('BarcodeDetector' in window) {
    const detector = new BarcodeDetector({ formats: ['ean_13','upc_a','code_128'] });
    const barcodes = await detector.detect(videoElement);
    if (barcodes.length) onDetected(barcodes[0].rawValue);
  }
  ```

- File input fallback
  ```html
  <input type="file" accept="image/*" capture="environment" />
  ```

- Torch toggle
  ```js
  const [track] = stream.getVideoTracks();
  const caps = track.getCapabilities();
  if (caps.torch) await track.applyConstraints({ advanced: [{ torch: true }] });
  ```

4. Performance tuning
   - Downscale canvas frames before decoding to reduce CPU.
   - Limit decode frequency (e.g., 10 fps) instead of every animation frame.

Backend integration
-------------------
- Keep decoding client-side. Send only the barcode string to the existing lookup endpoints.
- If a lookup endpoint is missing, add `GET /api/lookup?barcode=...` (read-only) that returns matched item metadata.
- Server responsibilities:
  - Validate format and check-digit where applicable.
  - Rate-limit anonymous lookups (per-IP) to prevent abuse.
  - Do not accept arbitrary image uploads unless needed => if you must, store temporarily with strong retention policy and authenticated access.

Security, privacy, and permissions
---------------------------------
- Require HTTPS for camera access.
- Only request camera when the user explicitly taps `Scan`.
- Do not persist camera frames or uploaded images unless user explicitly chooses to upload; prefer client-side decode.
- Sanitize barcode strings server-side; treat them as untrusted input.

Compatibility & edge cases
-------------------------
- Android Chrome: recommended UX; `BarcodeDetector` often available.
- iOS Safari: rely on `getUserMedia` where supported, but use file-input capture fallback for best compatibility.
- Desktop: support webcams; show fallback to manual entry when no camera exists.
- Permission denied: show file-input and manual input with clear instructions.

- Firefox (Desktop & Android): assume no native `BarcodeDetector` and run the JS fallback (`html5-qrcode`/`@zxing/library`) against the `getUserMedia` stream. Test closely in Firefox as behavior and permission prompts differ slightly from Chromium. For Firefox on iOS, treat it like Safari (WebKit) and prefer file-input fallback.


Testing recommendations
-----------------------
- Devices: multiple Android phones (Chrome), multiple iPhones (Safari), desktop with webcam.
- Test barcode types: EAN/UPC variants, Code128/39.
- Test low light, rotated barcodes, multiple barcodes in frame.
- Add automated UI tests for the component where possible (mock streams).

Deliverables / Files to add
--------------------------
- `ImplementationDetails/BARCODE_SCANNING_IMPLEMENTATION.md` (this document).
- Frontend (suggested):
  - `MediaSet.Remix/app/components/BarcodeScanner.tsx` — scanner component (client-only).
  - Integrate button/modal into add/edit routes: `MediaSet.Remix/app/routes/items/new.tsx` and edit route.
  - Optionally add `html5-qrcode` or `@zxing/library` to `MediaSet.Remix/package.json`.
- Backend (if missing):
  - `MediaSet.Api/Controllers/LookupController.cs` — `GET /api/lookup?barcode=`.

Estimated effort
----------------
- POC (component + modal + native + basic fallback): 4–8 hours.
- Full integration, UX polish, torch support, file fallback and testing: 8–12 hours.
- Cross-device QA and accessibility fixes: 4–8 hours.

Risks & mitigations
-------------------
- iOS limitations: mitigate with file-input fallback; mark as known limitation in UI.
- Browser fragmentation: feature detect and provide graceful fallbacks.
- Privacy concerns: decode on-device and minimize server uploads.

Next steps
----------
1. Confirm you want a POC implementation added to the Remix app.
2. If yes: I will create a feature branch, add `BarcodeScanner.tsx`, wire it into add/edit pages, update `package.json` (if needed), run the dev server and test on Chrome.

Contact
-------
For questions or to request specific UI mockups or stricter format support, reply and I will update the document.
