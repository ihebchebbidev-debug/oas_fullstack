import { useCallback, useEffect, useRef, useState } from 'react';
import { ScanLine, Camera, CameraOff, Loader2 } from 'lucide-react';
import { useT } from '@/i18n/I18nProvider';
import { signInWithBadgeToken } from '@/oas/authStore';
import { isNativeApp, scanWithNativeCamera, startWebScan, type WebScanHandle } from '@/lib/scanner';

interface Props {
  onSuccess: () => void;
  onFallback: () => void;
}

export function ScanPanel({ onSuccess, onFallback }: Props) {
  const t = useT();
  const videoRef = useRef<HTMLVideoElement>(null);
  const [camera, setCamera] = useState<'idle' | 'live' | 'unavailable' | 'native'>(
    isNativeApp ? 'native' : 'idle',
  );
  const [error, setError] = useState<string | null>(null);
  const [checking, setChecking] = useState(false);

  const handleRef = useRef<(value: string) => void>(() => {});
  handleRef.current = (value: string) => {
    const raw = value.trim().replace(/^badge:/i, '');
    if (!raw || checking) return;
    setChecking(true);
    setError(null);
    void signInWithBadgeToken(raw).then((result) => {
      setChecking(false);
      if (typeof result === 'string') {
        setError(t(result === 'network' ? 'mobile.login.network' : 'mobile.login.badgeUnknown'));
        return;
      }
      onSuccess();
    });
  };

  const openNativeScanner = useCallback(async () => {
    setError(null);
    try {
      const value = await scanWithNativeCamera();
      if (value) handleRef.current(value);
    } catch {
      setError(t('mobile.scan.denied'));
    }
  }, [t]);

  useEffect(() => {
    if (isNativeApp) return;
    let handle: WebScanHandle | null = null;
    let stopped = false;
    (async () => {
      const el = videoRef.current;
      if (!el) return;
      try {
        handle = await startWebScan(el, (value) => handleRef.current(value));
        if (stopped) handle.stop();
        else setCamera('live');
      } catch {
        setCamera('unavailable');
      }
    })();
    return () => {
      stopped = true;
      handle?.stop();
    };
  }, []);

  return (
    <div className="space-y-4">
      <div className="relative flex aspect-square w-full items-center justify-center overflow-hidden rounded-2xl border border-border bg-muted/60">
        <video
          ref={videoRef}
          muted
          playsInline
          className={`h-full w-full object-cover ${camera === 'live' ? '' : 'hidden'}`}
        />
        {camera !== 'live' && (
          <div className="flex flex-col items-center gap-2 text-muted-foreground">
            {camera === 'unavailable' ? <CameraOff className="h-16 w-16" /> : <ScanLine className="h-16 w-16" />}
            <p className="max-w-[70%] text-balance text-center text-xs">
              {camera === 'unavailable'
                ? t('mobile.scan.noCamera')
                : camera === 'native'
                  ? t('mobile.scan.nativeHint')
                  : t('mobile.scan.starting')}
            </p>
          </div>
        )}
        <div className="pointer-events-none absolute inset-8 rounded-xl border-2 border-dashed border-foreground/40" />
      </div>

      <p className="text-center text-sm text-muted-foreground">{t('mobile.login.scanHint')}</p>

      {checking && (
        <p className="flex items-center justify-center gap-2 text-center text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" /> {t('mobile.scan.starting')}
        </p>
      )}

      {error && <p className="text-center text-sm font-medium text-state-technical">{error}</p>}

      {camera === 'native' && (
        <button type="button" onClick={() => void openNativeScanner()} disabled={checking}
          className="flex min-h-[72px] w-full items-center justify-center gap-2 rounded-xl bg-foreground text-base font-semibold text-background disabled:opacity-60">
          <Camera className="h-5 w-5" /> {t('mobile.scan.openCamera')}
        </button>
      )}

      <button type="button" onClick={onFallback}
        className="w-full text-center text-sm font-medium text-muted-foreground underline-offset-2 hover:underline">
        {t('mobile.login.cantScan')}
      </button>
    </div>
  );
}
