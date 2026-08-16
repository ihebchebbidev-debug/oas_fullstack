/**
 * Barcode / QR scanning bridge.
 *
 * - Native (Capacitor / Android): uses the ML-Kit backed `@capacitor/barcode-scanner`
 *   plugin, which opens the real device camera in a full-screen scanner activity.
 * - Web: uses the native `BarcodeDetector` when available, otherwise falls back to
 *   `jsQR` decoding of video frames drawn on a canvas.
 */
import { Capacitor } from '@capacitor/core';

export const isNativeApp = Capacitor.isNativePlatform();

/** Opens the native full-screen scanner. Returns the raw value, or null if cancelled. */
export async function scanWithNativeCamera(): Promise<string | null> {
  const { CapacitorBarcodeScanner, CapacitorBarcodeScannerTypeHint } = await import(
    '@capacitor/barcode-scanner'
  );
  const res = await CapacitorBarcodeScanner.scanBarcode({
    hint: CapacitorBarcodeScannerTypeHint.ALL,
    scanInstructions: undefined,
    cameraDirection: 1, // back camera
  } as Parameters<typeof CapacitorBarcodeScanner.scanBarcode>[0]);
  const value = (res as { ScanResult?: string }).ScanResult ?? '';
  return value ? value : null;
}

type BarcodeDetectorCtor = new (opts: { formats: string[] }) => {
  detect: (source: CanvasImageSource) => Promise<{ rawValue: string }[]>;
};

export interface WebScanHandle {
  stop: () => void;
}

/**
 * Starts a continuous web camera scan into `video`, calling `onResult` once.
 * Throws if the camera cannot be opened (permission denied / no device).
 */
export async function startWebScan(
  video: HTMLVideoElement,
  onResult: (value: string) => void,
): Promise<WebScanHandle> {
  if (!navigator.mediaDevices?.getUserMedia) throw new Error('no-camera');

  const stream = await navigator.mediaDevices.getUserMedia({
    video: { facingMode: { ideal: 'environment' } },
    audio: false,
  });

  let stopped = false;
  let timer = 0;
  video.srcObject = stream;
  video.setAttribute('playsinline', 'true');
  await video.play();

  const stop = () => {
    stopped = true;
    window.clearTimeout(timer);
    stream.getTracks().forEach((t) => t.stop());
    video.srcObject = null;
  };

  const Detector = (window as unknown as { BarcodeDetector?: BarcodeDetectorCtor }).BarcodeDetector;
  const detector = Detector ? new Detector({ formats: ['qr_code', 'code_128', 'ean_13'] }) : null;

  const canvas = document.createElement('canvas');
  const ctx = canvas.getContext('2d', { willReadFrequently: true });
  let jsQR: typeof import('jsqr').default | null = null;
  if (!detector) jsQR = (await import('jsqr')).default;

  const tick = async () => {
    if (stopped) return;
    try {
      if (video.readyState >= 2 && video.videoWidth) {
        if (detector) {
          const hits = await detector.detect(video);
          if (hits[0]?.rawValue) {
            stop();
            onResult(hits[0].rawValue);
            return;
          }
        } else if (jsQR && ctx) {
          canvas.width = video.videoWidth;
          canvas.height = video.videoHeight;
          ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
          const img = ctx.getImageData(0, 0, canvas.width, canvas.height);
          const hit = jsQR(img.data, img.width, img.height, { inversionAttempts: 'dontInvert' });
          if (hit?.data) {
            stop();
            onResult(hit.data);
            return;
          }
        }
      }
    } catch {
      /* frame not ready */
    }
    timer = window.setTimeout(() => void tick(), 200);
  };

  void tick();
  return { stop };
}
