import React, { useEffect, useRef, useState } from 'react';

type Props = {
  open: boolean;
  onClose: () => void;
  onDetected: (value: string) => void;
};

export default function BarcodeScanner({ open, onClose, onDetected }: Props) {
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const [error, setError] = useState<string | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const [scannerType, setScannerType] = useState<'native' | 'zxing' | null>(null);
  const codeReaderRef = useRef<{
    reset: () => void;
    decodeFromVideoDevice: (...args: unknown[]) => Promise<unknown>;
  } | null>(null);
  const detectionLoopRef = useRef<number | null>(null);

  useEffect(() => {
    if (!open) {
      // Ensure camera is released when modal closes
      if (streamRef.current) {
        console.log('[BarcodeScanner] Closing - stopping camera stream');
        streamRef.current.getTracks().forEach((t) => t.stop());
        streamRef.current = null;
      }
      if (codeReaderRef.current) {
        try {
          codeReaderRef.current.reset();
        } catch (e) {
          console.debug('[BarcodeScanner] Error resetting ZXing on close:', e);
        }
        codeReaderRef.current = null;
      }
      if (detectionLoopRef.current) {
        cancelAnimationFrame(detectionLoopRef.current);
        detectionLoopRef.current = null;
      }
      return;
    }

    let active = true;
    let detectedAlready = false;

    async function start() {
      setError(null);
      setScannerType(null);

      // Check for secure context (required for getUserMedia)
      if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
        setError('Camera access requires HTTPS or localhost. Current page is not secure.');
        console.error('[BarcodeScanner] getUserMedia not available - insecure context');
        return;
      }

      try {
        // Request higher resolution for better barcode detection
        const mediaStream = await navigator.mediaDevices.getUserMedia({
          video: {
            facingMode: 'environment',
            width: { ideal: 1920, min: 640 },
            height: { ideal: 1080, min: 480 },
          },
        });

        if (!active) {
          mediaStream.getTracks().forEach((t) => t.stop());
          return;
        }

        streamRef.current = mediaStream;
        if (videoRef.current) {
          videoRef.current.srcObject = mediaStream;
        }

        console.log('[BarcodeScanner] Camera stream acquired', {
          width: mediaStream.getVideoTracks()[0].getSettings().width,
          height: mediaStream.getVideoTracks()[0].getSettings().height,
        });

        // Try native BarcodeDetector first (Chrome/Edge)
        if ('BarcodeDetector' in window && 'ImageCapture' in window) {
          try {
            // @ts-expect-error BarcodeDetector is not in all TypeScript lib definitions
            const detector = new (
              window as {
                BarcodeDetector: new (opts: { formats: string[] }) => {
                  detect: (img: unknown) => Promise<{ rawValue: string }[]>;
                };
              }
            ).BarcodeDetector({
              formats: ['ean_13', 'upc_a', 'code_128', 'code_39', 'ean_8', 'upc_e'],
            });
            const track = mediaStream.getVideoTracks()[0];
            // @ts-expect-error ImageCapture is not in all TypeScript lib definitions
            const imageCapture = new (
              window as { ImageCapture: new (track: MediaStreamTrack) => { grabFrame: () => Promise<ImageBitmap> } }
            ).ImageCapture(track);

            setScannerType('native');
            console.log('[BarcodeScanner] Using native BarcodeDetector');

            const loop = async () => {
              if (!active || detectedAlready) return;

              try {
                const bitmap = await imageCapture.grabFrame();
                const barcodes = await detector.detect(bitmap);

                if (barcodes && barcodes.length > 0 && !detectedAlready) {
                  detectedAlready = true;
                  console.log('[BarcodeScanner] Detected:', barcodes[0].rawValue);
                  onDetected(barcodes[0].rawValue);
                  return; // Stop loop after detection
                }

                if (active && !detectedAlready) {
                  detectionLoopRef.current = requestAnimationFrame(loop);
                }
              } catch (err) {
                console.error('[BarcodeScanner] Native detection error:', err);
                // If native fails, fall back to ZXing
                if (active && !detectedAlready) {
                  startJSFallback();
                }
              }
            };

            detectionLoopRef.current = requestAnimationFrame(loop);
            return; // Successfully started native detector
          } catch (err) {
            console.warn('[BarcodeScanner] Native BarcodeDetector failed:', err);
            // Fall through to ZXing fallback
          }
        }

        // Fallback to ZXing for Firefox and other browsers
        await startJSFallback();
      } catch (err: unknown) {
        console.error('[BarcodeScanner] Camera access error:', err);
        setError((err as { message?: string })?.message ?? String(err));
      }
    }

    async function startJSFallback() {
      if (!active || detectedAlready) return;

      try {
        console.log('[BarcodeScanner] Starting ZXing fallback');
        setScannerType('zxing');

        const { BrowserMultiFormatReader, DecodeHintType, BarcodeFormat } = await import('@zxing/library');

        // Configure hints for better detection
        const hints = new Map();
        hints.set(DecodeHintType.POSSIBLE_FORMATS, [
          BarcodeFormat.EAN_13,
          BarcodeFormat.EAN_8,
          BarcodeFormat.UPC_A,
          BarcodeFormat.UPC_E,
          BarcodeFormat.CODE_128,
          BarcodeFormat.CODE_39,
        ]);
        hints.set(DecodeHintType.TRY_HARDER, true);
        hints.set(DecodeHintType.ASSUME_GS1, true); // Better EAN/UPC detection

        const codeReader = new BrowserMultiFormatReader(hints);
        codeReaderRef.current = codeReader;

        const videoElem = videoRef.current;
        if (!videoElem) {
          throw new Error('Video element not found');
        }

        // Wait for video to be ready
        if (videoElem.readyState < 2) {
          console.log('[BarcodeScanner] Waiting for video to be ready...');
          await new Promise((resolve) => {
            videoElem.addEventListener('loadeddata', resolve, { once: true });
          });
        }

        console.log('[BarcodeScanner] Video ready, starting ZXing decode', {
          videoWidth: videoElem.videoWidth,
          videoHeight: videoElem.videoHeight,
          readyState: videoElem.readyState,
        });
        await codeReader.decodeFromVideoDevice(
          null, // Use default device
          videoElem,
          (result) => {
            if (result && !detectedAlready) {
              detectedAlready = true;
              const text = result.getText();
              console.log('[BarcodeScanner] ZXing detected:', text);
              onDetected(text);
              // Stop decoding after successful detection
              if (codeReaderRef.current) {
                try {
                  codeReaderRef.current.reset();
                } catch (e) {
                  console.debug('[BarcodeScanner] Error stopping after detection:', e);
                }
              }
            }
            // Ignore common "no barcode found" errors - these are expected during scanning
            // NotFoundException, No MultiFormat Readers, etc. are normal until a barcode is detected
          }
        );

        console.log('[BarcodeScanner] ZXing decoder initialized and scanning');
      } catch (err: unknown) {
        console.error('[BarcodeScanner] ZXing initialization failed:', err);
        setError(`Scanner error: ${(err as { message?: string })?.message ?? String(err)}`);
      }
    }

    start();

    return () => {
      console.log('[BarcodeScanner] Cleanup - stopping all resources');
      active = false;
      detectedAlready = true;

      // Cancel animation frame loop
      if (detectionLoopRef.current) {
        cancelAnimationFrame(detectionLoopRef.current);
        detectionLoopRef.current = null;
      }

      // Stop ZXing reader
      if (codeReaderRef.current) {
        try {
          codeReaderRef.current.reset();
        } catch (e) {
          console.debug('[BarcodeScanner] Error resetting ZXing:', e);
        }
        codeReaderRef.current = null;
      }

      // Stop media stream
      if (streamRef.current) {
        console.log('[BarcodeScanner] Stopping camera tracks');
        streamRef.current.getTracks().forEach((t) => {
          t.stop();
          console.log('[BarcodeScanner] Stopped track:', t.kind, t.label);
        });
        streamRef.current = null;
      }
    };
  }, [open, onDetected]);

  const handleFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setError(null);
    console.log('[BarcodeScanner] Processing uploaded file:', file.name, file.type, file.size);
    const url = URL.createObjectURL(file);

    try {
      // Try native BarcodeDetector first
      if ('BarcodeDetector' in window) {
        try {
          // @ts-expect-error BarcodeDetector is not in all TypeScript lib definitions
          const detector = new (
            window as {
              BarcodeDetector: new (opts: { formats: string[] }) => {
                detect: (img: unknown) => Promise<{ rawValue: string }[]>;
              };
            }
          ).BarcodeDetector({
            formats: ['ean_13', 'upc_a', 'code_128', 'code_39', 'ean_8', 'upc_e'],
          });

          const img = await createImageBitmap(file);
          const barcodes = await detector.detect(img);

          if (barcodes && barcodes.length > 0) {
            console.log('[BarcodeScanner] File scan (native):', barcodes[0].rawValue);
            onDetected(barcodes[0].rawValue);
            URL.revokeObjectURL(url);
            return;
          }
        } catch (err) {
          console.warn('[BarcodeScanner] Native file scan failed:', err);
          // Fall through to ZXing
        }
      }

      // Fallback to ZXing with enhanced configuration
      console.log('[BarcodeScanner] Using ZXing for file scan');
      const { BrowserMultiFormatReader, DecodeHintType, BarcodeFormat } = await import('@zxing/library');

      // Configure hints for better image detection
      const hints = new Map();
      hints.set(DecodeHintType.POSSIBLE_FORMATS, [
        BarcodeFormat.EAN_13,
        BarcodeFormat.EAN_8,
        BarcodeFormat.UPC_A,
        BarcodeFormat.UPC_E,
        BarcodeFormat.CODE_128,
        BarcodeFormat.CODE_39,
      ]);
      hints.set(DecodeHintType.TRY_HARDER, true);
      hints.set(DecodeHintType.ASSUME_GS1, true);

      const reader = new BrowserMultiFormatReader(hints);
      const result = await reader.decodeFromImageUrl(url);

      if (result) {
        const text = result.getText();
        console.log('[BarcodeScanner] File scan (ZXing):', text);
        onDetected(text);
      } else {
        setError('No barcode detected. Try taking a clearer photo with better lighting.');
      }
    } catch (err: unknown) {
      console.error('[BarcodeScanner] File scan error:', err);
      // Provide user-friendly error messages
      const errMsg = (err as { message?: string })?.message;
      if (errMsg?.includes('No MultiFormat Readers')) {
        setError(
          'Could not detect barcode. Please ensure:\n• Barcode is clearly visible and in focus\n• Good lighting without glare\n• Barcode fills most of the frame'
        );
      } else if (errMsg?.includes('NotFoundException')) {
        setError('No barcode found in image. Try taking a closer, clearer photo.');
      } else {
        setError(`Could not read barcode: ${errMsg ?? String(err)}`);
      }
    } finally {
      URL.revokeObjectURL(url);
    }
  };

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/75">
      <div className="bg-gray-900 rounded-md p-4 w-full max-w-xl mx-4">
        <div className="flex items-center justify-between mb-2">
          <div className="flex items-center gap-2">
            <h3 className="text-lg font-semibold">Scan Barcode</h3>
            {scannerType && (
              <span className="text-xs px-2 py-1 rounded bg-blue-500/20 text-blue-300">
                {scannerType === 'native' ? 'Hardware' : 'Software'}
              </span>
            )}
          </div>
          <button
            onClick={onClose}
            className="px-3 py-1 text-sm rounded bg-gray-700 hover:bg-gray-600 text-white"
            type="button"
          >
            Close
          </button>
        </div>

        <div className="relative w-full h-64 bg-black rounded-md overflow-hidden flex items-center justify-center">
          <video ref={videoRef} autoPlay playsInline muted className="w-full h-full object-cover" />

          {/* Scanning guide overlay */}
          {!error && (
            <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
              <div className="w-48 h-32 border-2 border-blue-400 rounded-lg shadow-lg">
                <div className="absolute -top-6 left-1/2 transform -translate-x-1/2 text-xs text-blue-300 bg-black/50 px-2 py-1 rounded">
                  Align barcode here
                </div>
              </div>
            </div>
          )}

          {error && (
            <div className="absolute inset-0 flex items-center justify-center bg-black/75">
              <div className="text-center px-4 max-w-sm">
                <p className="text-red-300 mb-2 whitespace-pre-line">{error}</p>
                <p className="text-xs text-gray-400">Try the file upload option below</p>
              </div>
            </div>
          )}
        </div>

        <div className="mt-3 flex flex-col gap-2">
          <label className="flex items-center justify-center gap-2 cursor-pointer px-4 py-2 bg-gray-700 hover:bg-gray-600 rounded text-sm text-white">
            <input type="file" accept="image/*" capture="environment" onChange={handleFile} className="hidden" />
            <span>📷 Upload Photo</span>
          </label>

          <div className="text-xs text-center text-gray-400">
            {scannerType === 'zxing' && (
              <div>
                <p>Software decoder active - Hold barcode steady</p>
                <p className="mt-1">Distance: 4-8 inches • Good lighting required</p>
              </div>
            )}
            {scannerType === 'native' && 'Hardware decoder (Chrome/Edge)'}
            {!scannerType && 'Initializing scanner...'}
          </div>
        </div>
      </div>
    </div>
  );
}
