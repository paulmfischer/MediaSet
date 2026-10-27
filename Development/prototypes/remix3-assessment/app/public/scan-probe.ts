import { MultiFormatReader, BarcodeFormat, DecodeHintType } from '@zxing/library';

const decoder = new MultiFormatReader();
decoder.setHints(new Map([[DecodeHintType.POSSIBLE_FORMATS, [BarcodeFormat.EAN_13, BarcodeFormat.UPC_A]]]));
decoder.reset();
document.documentElement.dataset.decoder = 'loaded-and-reset';
const result = document.querySelector('#scan-result');
if (result)
  result.textContent =
    'ZXing 0.23.0 loaded; EAN/UPC hints and reset succeeded. Camera permission and real code decoding remain device checks.';
