// Barcode decoding for the scanner dialog: the browser's native BarcodeDetector when it has one,
// otherwise the @zxing/library decoder, which is only downloaded the first time it is needed.

// The product barcodes a book, movie, game or album carries.
const NATIVE_FORMATS = ['ean_13', 'upc_a', 'upc_e', 'ean_8', 'code_128'];

interface NativeDetectedBarcode {
  rawValue: string;
}

interface NativeBarcodeDetector {
  detect(source: HTMLVideoElement): Promise<NativeDetectedBarcode[]>;
}

interface NativeBarcodeDetectorConstructor {
  new (options?: { formats?: string[] }): NativeBarcodeDetector;
  getSupportedFormats?: () => Promise<string[]>;
}

/** Reads one barcode from the current video frame; resolves to null when there is none in view. */
export type DecodeFrame = (video: HTMLVideoElement) => Promise<string | null>;

async function createNativeDecoder(): Promise<DecodeFrame | null> {
  const Detector = (globalThis as { BarcodeDetector?: NativeBarcodeDetectorConstructor }).BarcodeDetector;
  if (!Detector) return null;
  let formats = NATIVE_FORMATS;
  try {
    const supported = await Detector.getSupportedFormats?.();
    if (supported) formats = NATIVE_FORMATS.filter((format) => supported.includes(format));
  } catch {
    // Keep the full list; construction below decides whether the detector really works.
  }
  if (formats.length === 0) return null;
  try {
    const detector = new Detector({ formats });
    return async (video) => (await detector.detect(video))[0]?.rawValue ?? null;
  } catch {
    return null;
  }
}

async function createZxingDecoder(): Promise<DecodeFrame> {
  // Dynamic import: the decoder is a separate download, fetched on the first scan that needs it.
  const zxing = await import('@zxing/library');
  const hints = new Map<unknown, unknown>([
    [
      zxing.DecodeHintType.POSSIBLE_FORMATS,
      [
        zxing.BarcodeFormat.EAN_13,
        zxing.BarcodeFormat.UPC_A,
        zxing.BarcodeFormat.UPC_E,
        zxing.BarcodeFormat.EAN_8,
        zxing.BarcodeFormat.CODE_128,
      ],
    ],
    [zxing.DecodeHintType.TRY_HARDER, true],
  ]);
  const reader = new zxing.MultiFormatReader();
  const canvas = document.createElement('canvas');
  const context = canvas.getContext('2d', { willReadFrequently: true });
  return async (video) => {
    if (!context || video.videoWidth === 0) return null;
    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;
    context.drawImage(video, 0, 0, canvas.width, canvas.height);
    const bitmap = new zxing.BinaryBitmap(
      new zxing.HybridBinarizer(new zxing.HTMLCanvasElementLuminanceSource(canvas))
    );
    try {
      return reader.decode(bitmap, hints as Map<import('@zxing/library').DecodeHintType, unknown>).getText();
    } catch {
      // NotFoundException (and a checksum or format error) just mean no readable barcode in this frame.
      return null;
    }
  };
}

export async function createDecoder(): Promise<DecodeFrame> {
  return (await createNativeDecoder()) ?? (await createZxingDecoder());
}
