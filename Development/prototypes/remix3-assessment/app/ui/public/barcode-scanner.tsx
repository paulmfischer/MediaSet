import { clientEntry, on, ref, type Handle } from 'remix/component';

// Spike port of components/scan/barcode-scanner.tsx: camera lifecycle tied to the <video> element's ref signal.
export const BarcodeScanner = clientEntry(import.meta.url, function BarcodeScanner(handle: Handle) {
  let open = false;
  let error: string | null = null;
  let detected: string | null = null;

  return () => (
    <div class="flex flex-col gap-2">
      <button
        type="button"
        mix={on('click', () => {
          open = !open;
          detected = null;
          error = null;
          handle.update();
        })}
      >
        {open ? 'Stop scanning' : 'Start scanning'}
      </button>
      {open && (
        <video
          id="scanner-video"
          class="w-full max-w-md"
          muted
          playsInline
          mix={ref(async (video, signal) => {
            try {
              let { BrowserMultiFormatReader } = await import('@zxing/library');
              let reader = new BrowserMultiFormatReader();
              signal.addEventListener('abort', () => reader.reset());
              await reader.decodeFromVideoDevice(null, video as HTMLVideoElement, (result) => {
                if (result && !signal.aborted) {
                  detected = result.getText();
                  open = false;
                  handle.update();
                }
              });
            } catch (e) {
              if (signal.aborted) return;
              error = e instanceof Error ? e.message : String(e);
              handle.update();
            }
          })}
        />
      )}
      {error && <p role="alert">{error}</p>}
      {detected && <p data-testid="detected">Detected: {detected}</p>}
    </div>
  );
});
