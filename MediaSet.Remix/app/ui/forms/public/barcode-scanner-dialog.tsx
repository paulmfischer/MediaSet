import { clientEntry, on, ref, type Handle } from 'remix/component';

import { createDecoder } from './barcode-decoder.ts';

/** The event the scan button sends on the form to open the scanner for one identifier field. */
export const OPEN_SCANNER_EVENT = 'mediaset:open-barcode-scanner';

export interface OpenScannerDetail {
  field: string;
}

export interface BarcodeScannerDialogProps {
  /** The `name` and `id` of the identifier input that takes the scanned code. */
  field: 'isbn' | 'barcode';
  /** Names the identifier in the heading, for example "ISBN" or "barcode". */
  identifierLabel: string;
}

const SCAN_INTERVAL_MS = 150;

const MESSAGES = {
  denied: 'Camera access was denied. Allow camera access for this site in your browser settings, then try again.',
  notFound: 'No camera was found on this device. Enter the code by hand instead.',
  unsupported: 'Camera access is not available here. It needs a secure (HTTPS) connection and a supported browser.',
  failed: 'The camera could not be started. Close any other app using it and try again.',
} as const;

function errorMessage(error: unknown): string {
  const name = error instanceof Error ? error.name : '';
  if (name === 'NotAllowedError' || name === 'SecurityError') return MESSAGES.denied;
  if (name === 'NotFoundError' || name === 'OverconstrainedError') return MESSAGES.notFound;
  return MESSAGES.failed;
}

// A modal dialog with a live camera preview, closed until the scan button asks for it (a DOM event on
// the surrounding form, since an island cannot take callbacks). It reads barcodes from the video
// until one is found and always releases the camera: on Cancel, Escape, a detected code and when the
// island is removed. A detected code goes into the identifier input and the form is submitted through
// the field's existing lookup button, so the server lookup flow runs unchanged.
export const BarcodeScannerDialog = clientEntry(
  import.meta.url,
  function BarcodeScannerDialog(handle: Handle<BarcodeScannerDialogProps>) {
    let dialog: HTMLDialogElement | undefined;
    let video: HTMLVideoElement | undefined;
    let stream: MediaStream | undefined;
    let timer: ReturnType<typeof setTimeout> | undefined;
    let open = false;
    let error: string | undefined;
    // Ends a scan session; a later session gets a new one, so a late async step never touches it.
    let session = { stopped: true };

    // Cancels the frame loop and stops every track. Safe to call more than once.
    const release = () => {
      session.stopped = true;
      clearTimeout(timer);
      timer = undefined;
      stream?.getTracks().forEach((track) => track.stop());
      stream = undefined;
      if (video) video.srcObject = null;
    };
    handle.signal.addEventListener('abort', release);

    const fail = (current: typeof session, message: string) => {
      if (current.stopped) return;
      release();
      error = message;
      handle.update();
    };

    const form = () => dialog?.closest('form') ?? undefined;

    const finish = (code: string) => {
      const owner = form();
      const { field } = handle.props;
      const input = owner?.elements.namedItem(field);
      release();
      dialog?.close();
      if (!owner || !(input instanceof HTMLInputElement)) return;
      input.value = code;
      const lookup = Array.from(owner.querySelectorAll<HTMLButtonElement>('button[name="intent"]')).find(
        (candidate) => candidate.value === `lookup:${field}`
      );
      if (lookup) owner.requestSubmit(lookup);
    };

    const start = async () => {
      const current = { stopped: false };
      session = current;
      if (!navigator.mediaDevices?.getUserMedia) return fail(current, MESSAGES.unsupported);
      let acquired: MediaStream;
      try {
        acquired = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' }, audio: false });
      } catch (caught) {
        return fail(current, errorMessage(caught));
      }
      if (current.stopped) {
        // Closed while the permission prompt was open: do not keep the camera.
        acquired.getTracks().forEach((track) => track.stop());
        return;
      }
      stream = acquired;
      try {
        if (!video) throw new Error('no video element');
        video.srcObject = acquired;
        await video.play();
        const decode = await createDecoder();
        const scan = async () => {
          if (current.stopped || !video) return;
          let code: string | null = null;
          try {
            code = await decode(video);
          } catch {
            // The decoder gave up (the native one failed repeatedly and the fallback would not load).
            return fail(current, MESSAGES.failed);
          }
          if (current.stopped) return;
          if (code) return finish(code);
          timer = setTimeout(scan, SCAN_INTERVAL_MS);
        };
        void scan();
      } catch {
        fail(current, MESSAGES.failed);
      }
    };

    const openScanner = (event: Event) => {
      const { field } = handle.props;
      if (open || (event as CustomEvent<OpenScannerDetail>).detail?.field !== field) return;
      open = true;
      error = undefined;
      handle.update();
      handle.queueTask(() => {
        if (dialog && !dialog.open) dialog.showModal();
        void start();
      });
    };

    // Runs once the dialog is in the page: listen on the form for the scan button's request.
    handle.queueTask(() => {
      form()?.addEventListener(OPEN_SCANNER_EVENT, openScanner, { signal: handle.signal });
    });

    const closed = () => {
      // Not open, or the dialog is open again: a late close event belongs to an earlier session.
      if (!open || dialog?.open) return;
      release();
      open = false;
      handle.update();
      // Return focus to the scan button. After a detected code the form is submitted and the lookup
      // re-renders it, so this only matters for Cancel, Escape and the backdrop; it is harmless otherwise.
      const { field } = handle.props;
      form()?.querySelector<HTMLElement>(`[data-scan-field="${field}"]`)?.focus();
    };

    return () => {
      const { identifierLabel } = handle.props;
      return (
        <dialog
          aria-labelledby={`scan-${handle.props.field}-title`}
          class="w-full max-w-lg rounded-md border border-gray-600 bg-gray-900 p-0 text-white backdrop:bg-black/60"
          mix={[
            ref((node) => {
              dialog = node as HTMLDialogElement;
            }),
            // Close fires for Cancel, Escape and a detected code alike.
            on('close', closed),
            // The content wrapper carries the padding, so only a backdrop click targets the dialog itself.
            on('click', (event) => {
              if (event.target === dialog) dialog?.close();
            }),
          ]}
        >
          <div class="flex flex-col gap-3 p-4">
            <h2 id={`scan-${handle.props.field}-title`} class="text-xl">
              Scan {identifierLabel}
            </h2>
            {error ? (
              <p role="alert" class="rounded-md border border-red-500/50 bg-red-500/10 p-3 text-red-300">
                {error}
              </p>
            ) : null}
            <video
              class={error ? 'hidden' : 'aspect-video w-full rounded-md bg-black object-cover'}
              aria-label="Camera preview"
              muted
              playsInline
              mix={ref((node) => {
                video = node as HTMLVideoElement;
                // The attributes cover browsers that read them before the properties.
                video.muted = true;
                video.setAttribute('playsinline', '');
              })}
            />
            {error ? null : (
              <p class="text-sm text-gray-400">
                Point the camera at the barcode on the back of the item and hold it steady.
              </p>
            )}
            <div class="flex justify-end">
              <button
                type="button"
                class="secondary"
                mix={on('click', () => {
                  dialog?.close();
                  // The dialog's own close event is asynchronous; release the camera right away.
                  closed();
                })}
              >
                Cancel
              </button>
            </div>
          </div>
        </dialog>
      );
    };
  }
);
