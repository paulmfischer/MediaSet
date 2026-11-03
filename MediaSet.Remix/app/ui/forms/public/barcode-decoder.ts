import type { DecodeHintType } from '@zxing/library';

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

// Frames are scaled down to this width before the JavaScript decoder reads them: it is CPU-bound.
const ZXING_MAX_WIDTH = 960;
// Consecutive failed native detections before switching to the JavaScript decoder.
const NATIVE_FAILURE_LIMIT = 5;

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
  let detector: NativeBarcodeDetector;
  try {
    detector = new Detector({ formats });
  } catch {
    return null;
  }
  // Some browsers expose a detector that throws on every frame (no platform support behind it). After
  // repeated failures hand over to the JavaScript decoder; if that cannot load, the error reaches the
  // caller, which shows a message instead of scanning forever.
  let failures = 0;
  let fallback: DecodeFrame | undefined;
  return async (video) => {
    if (fallback) return fallback(video);
    try {
      const code = (await detector.detect(video))[0]?.rawValue ?? null;
      failures = 0;
      return code;
    } catch {
      failures += 1;
      if (failures < NATIVE_FAILURE_LIMIT) return null;
      fallback = await createZxingDecoder();
      return fallback(video);
    }
  };
}

async function createZxingDecoder(): Promise<DecodeFrame> {
  // Dynamic import: the decoder is a separate download, fetched on the first scan that needs it.
  const zxing = await import('@zxing/library');
  const hints = new Map<DecodeHintType, unknown>([
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
  let sourceWidth = 0;
  let sourceHeight = 0;
  return async (video) => {
    if (!context || video.videoWidth === 0) return null;
    // Size the canvas only when the video's size changes; resizing clears and reallocates it.
    if (video.videoWidth !== sourceWidth || video.videoHeight !== sourceHeight) {
      sourceWidth = video.videoWidth;
      sourceHeight = video.videoHeight;
      const scale = Math.min(1, ZXING_MAX_WIDTH / sourceWidth);
      canvas.width = Math.round(sourceWidth * scale);
      canvas.height = Math.round(sourceHeight * scale);
    }
    context.drawImage(video, 0, 0, canvas.width, canvas.height);
    const bitmap = new zxing.BinaryBitmap(
      new zxing.HybridBinarizer(new zxing.HTMLCanvasElementLuminanceSource(canvas))
    );
    try {
      return reader.decode(bitmap, hints).getText();
    } catch {
      // NotFoundException (and a checksum or format error) just mean no readable barcode in this frame.
      return null;
    }
  };
}

export async function createDecoder(): Promise<DecodeFrame> {
  return (await createNativeDecoder()) ?? (await createZxingDecoder());
}
