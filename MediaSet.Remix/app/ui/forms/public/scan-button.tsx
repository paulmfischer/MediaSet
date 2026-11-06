import { clientEntry, on, ref, type Handle } from 'remix/component';

import { OPEN_SCANNER_EVENT, type OpenScannerDetail } from './barcode-scanner-dialog.tsx';

export interface ScanButtonProps {
  /** The `name` and `id` of the identifier input that takes the scanned code. */
  field: 'isbn' | 'barcode';
  /** Names the identifier in the label, for example "ISBN" or "barcode". */
  identifierLabel: string;
}

const MOBILE_USER_AGENT = /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i;

/**
 * Scanning is for phones and tablets, as on the old UI: a touch device that is either narrow or
 * reports a mobile user agent. It also needs a camera API to open the scanner.
 */
function canScan(): boolean {
  if (!navigator.mediaDevices?.getUserMedia) return false;
  const hasTouch = 'ontouchstart' in window || navigator.maxTouchPoints > 0;
  const narrowScreen = window.matchMedia('(max-width: 768px)').matches;
  return hasTouch && (narrowScreen || MOBILE_USER_AGENT.test(navigator.userAgent));
}

// Asks the form's barcode scanner dialog to open. Without JavaScript, on a device that can't scan
// (a desktop, or no camera API) it renders nothing, so the form is the plain one.
export const ScanButton = clientEntry(import.meta.url, function ScanButton(handle: Handle<ScanButtonProps>) {
  let supported = false;
  let button: HTMLButtonElement | undefined;

  const check = () => {
    const next = canScan();
    if (next === supported) return;
    supported = next;
    handle.update();
  };

  handle.queueTask(() => {
    check();
    // A window resized across the breakpoint (or a rotated tablet) changes whether scanning is offered.
    window.addEventListener('resize', check, { signal: handle.signal });
  });

  return () => {
    if (!supported) return null;
    const { field, identifierLabel } = handle.props;
    return (
      <button
        type="button"
        class="secondary"
        aria-label={`Scan ${identifierLabel}`}
        data-scan-field={field}
        mix={[
          ref((node) => {
            button = node as HTMLButtonElement;
          }),
          on('click', () => {
            button?.form?.dispatchEvent(new CustomEvent<OpenScannerDetail>(OPEN_SCANNER_EVENT, { detail: { field } }));
          }),
        ]}
      >
        Scan
      </button>
    );
  };
});
