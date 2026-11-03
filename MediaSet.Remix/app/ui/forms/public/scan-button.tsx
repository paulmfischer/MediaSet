import { clientEntry, on, ref, type Handle } from 'remix/component';

import { OPEN_SCANNER_EVENT, type OpenScannerDetail } from './barcode-scanner-dialog.tsx';

export interface ScanButtonProps {
  /** The `name` and `id` of the identifier input that takes the scanned code. */
  field: 'isbn' | 'barcode';
  /** Names the identifier in the label, for example "ISBN" or "barcode". */
  identifierLabel: string;
}

// Asks the form's barcode scanner dialog to open. Without JavaScript or camera support (no
// getUserMedia) it renders nothing, so the form is the plain one.
export const ScanButton = clientEntry(import.meta.url, function ScanButton(handle: Handle<ScanButtonProps>) {
  let supported = false;
  let button: HTMLButtonElement | undefined;

  handle.queueTask(() => {
    supported = Boolean(navigator.mediaDevices?.getUserMedia);
    if (supported) handle.update();
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
