import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ScanLine, Keyboard, CameraOff, Check, Users, Camera } from 'lucide-react';

import { useT } from '@/i18n/I18nProvider';
import { useHierarchyState } from '@/oas/hierarchyStore';
import { dismissOpenConflict, openSession, scanCode, useOpenConflict } from '@/modules/auth/store/session';
import { getAuth } from '@/oas/authStore';
import { publishedOperator, useAssignments } from '@/oas/assignmentStore';
import { isNativeApp, scanWithNativeCamera, startWebScan, type WebScanHandle } from '@/lib/scanner';

export default function ScanPage() {
  const navigate = useNavigate();
  const t = useT();
  const { posts } = useHierarchyState();
  const videoRef = useRef<HTMLVideoElement>(null);
  const [camera, setCamera] = useState<'idle' | 'live' | 'unavailable' | 'native'>(
    isNativeApp ? 'native' : 'idle',
  );
  const [manual, setManual] = useState(false);
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pendingCode, setPendingCode] = useState<string | null>(null);
  /** BL-029 — scan a neighbouring post to declare a stop there, without session. */
  const [neighbor, setNeighbor] = useState(false);
  const neighborRef = useRef(false);
  const assignments = useAssignments();
  const openConflict = useOpenConflict();

  const acceptRef = useRef<(raw: string) => void>(() => {});

  async function accept(raw: string) {
    let parsed: string | null;
    try {
      parsed = await scanCode(raw);
    } catch {
      setError(t('mobile.scan.scanFailed'));
      return;
    }
    if (!parsed) {
      setError(t('mobile.scan.unknown'));
      return;
    }
    if (neighborRef.current) {
      navigate(`/mobile/neighbor?post=${encodeURIComponent(parsed)}`);
      return;
    }
    const session = await openSession(parsed, publishedOperator(assignments, parsed) ?? getAuth()?.displayName ?? getAuth()?.email ?? undefined);
    if (!session) {
      setPendingCode(parsed);
      return;
    }
    navigate('/mobile/home', { replace: true });
  }
  acceptRef.current = (raw) => void accept(raw);

  async function forceOpen() {
    if (!pendingCode) return;
    await openSession(pendingCode, publishedOperator(assignments, pendingCode) ?? getAuth()?.displayName ?? getAuth()?.email ?? undefined, true);
    setPendingCode(null);
    navigate('/mobile/home', { replace: true });
  }

  /** Native: open the real device camera scanner (ML Kit). */
  const openNativeScanner = useCallback(async () => {
    setError(null);
    try {
      const value = await scanWithNativeCamera();
      if (value) acceptRef.current(value);
    } catch {
      setError(t('mobile.scan.denied'));
    }
  }, [t]);

  useEffect(() => {
    if (isNativeApp) {
      void openNativeScanner();
      return;
    }
    let handle: WebScanHandle | null = null;
    let stopped = false;
    (async () => {
      const el = videoRef.current;
      if (!el) return;
      try {
        handle = await startWebScan(el, (value) => acceptRef.current(value));
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
  }, [openNativeScanner]);

  return (
    <div className="space-y-4">
      <header>
        <h1 className="text-[1.375rem] font-semibold">{t('mobile.scan.title')}</h1>
        <p className="text-sm text-muted-foreground">{t('mobile.scan.subtitle')}</p>
      </header>

      <div data-demo="m-scan" className="relative flex aspect-square w-full items-center justify-center overflow-hidden rounded-xl border border-border bg-muted/60">
        <video
          ref={videoRef}
          muted
          playsInline
          className={`h-full w-full object-cover ${camera === 'live' ? '' : 'hidden'}`}
        />
        {camera !== 'live' && (
          <div className="flex flex-col items-center gap-2 text-muted-foreground">
            {camera === 'unavailable' ? <CameraOff className="h-14 w-14" /> : <ScanLine className="h-14 w-14" />}
            <p className="max-w-[60%] text-balance text-center text-xs">
              {camera === 'unavailable'
                ? t('mobile.scan.noCamera')
                : camera === 'native'
                  ? t('mobile.scan.nativeHint')
                  : t('mobile.scan.starting')}
            </p>
          </div>
        )}
        <div className="pointer-events-none absolute inset-8 rounded-lg border-2 border-dashed border-foreground/50" />
      </div>

      {camera === 'native' && (
        <button
          type="button"
          onClick={() => void openNativeScanner()}
          className="flex min-h-[72px] w-full items-center justify-center gap-2 rounded-xl bg-foreground text-base font-semibold text-background"
        >
          <Camera className="h-5 w-5" /> {t('mobile.scan.openCamera')}
        </button>
      )}


      {error && <p className="text-sm font-medium text-state-technical">{error}</p>}

      {openConflict && pendingCode && (
        <div className="space-y-2 rounded-xl border border-state-material/50 bg-state-material/10 p-3">
          <p className="text-sm font-medium text-state-material">
            {openConflict.reason === 'shift'
              ? t('mobile.scan.shiftConflict', { post: openConflict.previousPost })
              : openConflict.reason === 'postBusy'
                ? t('mobile.scan.postBusy')
                : openConflict.reason === 'userBusy'
                  ? t('mobile.scan.userBusy')
                  : t('mobile.scan.openFailed')}
          </p>
          <div className="flex gap-2">
            {openConflict.reason !== 'error' && (
              <button
                type="button"
                onClick={forceOpen}
                className="min-h-[44px] flex-1 rounded-lg bg-foreground text-sm font-semibold text-background"
              >
                {t('mobile.scan.shiftConflict.continue')}
              </button>
            )}
            <button
              type="button"
              onClick={() => { dismissOpenConflict(); setPendingCode(null); }}
              className={`min-h-[44px] rounded-lg border border-border text-sm font-medium ${openConflict.reason === 'error' ? 'w-full' : 'flex-1'}`}
            >
              {t('common.close')}
            </button>
          </div>
        </div>
      )}

      {/* BL-029 — stop on a neighbouring post: scan only, no session opened. */}
      <button
        type="button"
        aria-pressed={neighbor}
        onClick={() => { const next = !neighbor; setNeighbor(next); neighborRef.current = next; }}
        className={`flex min-h-[56px] w-full items-center justify-center gap-2 rounded-xl border text-sm font-medium ${
          neighbor ? 'border-state-technical bg-state-technical/10 text-state-technical' : 'border-border bg-card'
        }`}
      >
        <Users className="h-4 w-4" />
        {neighbor ? t('mobile.scan.neighborOn') : t('mobile.scan.neighbor')}
      </button>

      {manual ? (
        <form
          className="space-y-3"
          onSubmit={(e) => {
            e.preventDefault();
            accept(code);
          }}
        >
          <input
            value={code}
            onChange={(e) => { setCode(e.target.value); setError(null); }}
            placeholder="PO-104"
            aria-label={t('mobile.scan.manual')}
            className="h-14 w-full rounded-xl border border-border bg-card px-4 font-mono text-lg uppercase"
          />
          <div className="flex flex-wrap gap-2">
            {posts.filter((p) => !p.archived).slice(0, 6).map((p) => (
              <button key={p.id} type="button" onClick={() => accept(p.code)}
                className="min-h-[44px] rounded-lg border border-border bg-card px-3 font-mono text-sm">
                {p.code}
              </button>
            ))}
          </div>
          <button type="submit"
            className="flex min-h-[72px] w-full items-center justify-center gap-2 rounded-xl bg-foreground text-base font-semibold text-background">
            <Check className="h-5 w-5" /> {neighbor ? t('mobile.scan.neighborGo') : t('mobile.scan.openSession')}
          </button>
        </form>
      ) : (
        <button type="button" onClick={() => setManual(true)}
          className="flex min-h-[56px] w-full items-center justify-center gap-2 rounded-xl border border-border bg-card text-sm font-medium">
          <Keyboard className="h-4 w-4" /> {t('mobile.scan.manual')}
        </button>
      )}
    </div>
  );
}
