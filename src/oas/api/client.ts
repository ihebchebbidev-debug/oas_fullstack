/**
 * Thin fetch wrapper for `/api/oas/*` (spec §3, §8.0). Replaces the
 * localStorage-backed stores' synchronous reads with real HTTP calls against
 * the OAS backend (`Backend/Modules/OAS/*`). Handles the console/shopfloor
 * JWT (second, independent scheme — spec §3.3), silent refresh on 401, and
 * a uniform error shape so every store can `catch (e) { if (e instanceof
 * ApiError) ... }`.
 *
 * Base URL: hardcoded to the production API host — the OAS backend is
 * served cross-origin from the SPA, not same-origin, so this is always an
 * absolute URL (the backend's CORS policy already allows any origin —
 * `Backend/Program.cs`'s "AllowFrontend" policy).
 */

/** Exported so `events.ts`'s SSE connection (a raw `EventSource`, not routed through `apiFetch`) builds against the same host instead of duplicating the literal. */
export const OAS_API_BASE = 'https://api.flowentra.app/api/oas';

/**
 * Every `/api/oas/*` request must carry `X-Tenant: <slug>oas` — the backend
 * (`OasTenantMiddleware`) routes purely off that header to pick the
 * tenant's dedicated database (spec §1.2 bis): `devoas.<domain>` →
 * `X-Tenant: devoas` → `TENANT_DEVOAS_DATABASE_URL`, and so on per client.
 * Derived once from the page's own hostname (first label — the deployed
 * subdomain IS the slug) so the same bundle works unmodified on every
 * tenant's subdomain; falls back to the dev tenant on localhost/IP hosts
 * where no such subdomain exists.
 */
function deriveOasSlug(): string {
  if (typeof window === 'undefined') return 'devoas';
  const host = window.location.hostname;
  if (host === 'localhost' || host === '127.0.0.1' || /^[\d.]+$/.test(host)) return 'devoas';
  return host.split('.')[0] || 'devoas';
}

export const OAS_TENANT_SLUG = deriveOasSlug();

export interface OasSession {
  accessToken: string;
  refreshToken: string;
  expiresAt: string | null;
}

const SESSION_KEY = 'oas.session.v1';

function readSession(): OasSession | null {
  try {
    const raw = localStorage.getItem(SESSION_KEY);
    return raw ? (JSON.parse(raw) as OasSession) : null;
  } catch {
    return null;
  }
}

let session: OasSession | null = typeof window === 'undefined' ? null : readSession();

function writeSession(next: OasSession | null) {
  session = next;
  try {
    if (next) localStorage.setItem(SESSION_KEY, JSON.stringify(next));
    else localStorage.removeItem(SESSION_KEY);
  } catch {
    /* private mode — keep the in-memory copy */
  }
}

/** authStore is the single subscribable source of truth for "am I signed in" — this module only owns the token. */
export function setSession(next: OasSession) {
  writeSession(next);
}

export function clearSession() {
  writeSession(null);
}

export function getSession(): OasSession | null {
  return session;
}

let unauthorizedHandler: (() => void) | null = null;
/** Registered by authStore so a hard (post-refresh) 401 signs the user out reactively, without client.ts importing authStore (would cycle). */
export function setUnauthorizedHandler(fn: () => void) {
  unauthorizedHandler = fn;
}

export class ApiError extends Error {
  status: number;
  body: unknown;
  constructor(status: number, message: string, body?: unknown) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.body = body;
  }
}

function buildUrl(path: string, query?: Record<string, string | number | boolean | undefined | null>): string {
  const url = new URL(`${OAS_API_BASE}${path}`, window.location.origin);
  if (query) {
    for (const [k, v] of Object.entries(query)) {
      if (v !== undefined && v !== null && v !== '') url.searchParams.set(k, String(v));
    }
  }
  // NOT `url.pathname + url.search` — the API is cross-origin (a different
  // host than the SPA), so the origin must be kept or every request would
  // silently resolve back against the page's own origin instead of the API.
  return url.toString();
}

let refreshPromise: Promise<boolean> | null = null;

async function tryRefresh(): Promise<boolean> {
  const refreshToken = session?.refreshToken;
  if (!refreshToken) return false;
  if (!refreshPromise) {
    refreshPromise = fetch(`${OAS_API_BASE}/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-Tenant': OAS_TENANT_SLUG },
      body: JSON.stringify({ refreshToken }),
    })
      .then(async (res) => {
        if (!res.ok) return false;
        const data = await res.json().catch(() => null) as { accessToken?: string; refreshToken?: string; expiresAt?: string } | null;
        if (!data?.accessToken) return false;
        setSession({ accessToken: data.accessToken, refreshToken: data.refreshToken ?? refreshToken, expiresAt: data.expiresAt ?? null });
        return true;
      })
      .catch(() => false)
      .finally(() => {
        refreshPromise = null;
      });
  }
  return refreshPromise;
}

async function extractErrorMessage(res: Response): Promise<{ message: string; body: unknown }> {
  let body: unknown = null;
  try {
    body = await res.json();
  } catch {
    /* no JSON body */
  }
  const b = body as { title?: string; message?: string; error?: string; detail?: string } | null;
  const message = b?.title ?? b?.message ?? b?.error ?? b?.detail ?? `HTTP ${res.status}`;
  return { message, body };
}

export interface ApiFetchOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH';
  body?: unknown;
  query?: Record<string, string | number | boolean | undefined | null>;
  /** Set false for the two anonymous auth endpoints (login/setup). Defaults true. */
  auth?: boolean;
}

export async function apiFetch<T>(path: string, opts: ApiFetchOptions = {}): Promise<T> {
  const { method = 'GET', body, query, auth = true } = opts;
  const url = buildUrl(path, query);

  const doFetch = (token: string | null) => {
    const headers: Record<string, string> = { 'X-Tenant': OAS_TENANT_SLUG };
    if (body !== undefined) headers['Content-Type'] = 'application/json';
    if (auth && token) headers['Authorization'] = `Bearer ${token}`;
    return fetch(url, { method, headers, body: body !== undefined ? JSON.stringify(body) : undefined });
  };

  let res = await doFetch(session?.accessToken ?? null);

  if (res.status === 401 && auth && session?.refreshToken) {
    const refreshed = await tryRefresh();
    if (refreshed) res = await doFetch(session?.accessToken ?? null);
  }

  if (res.status === 401 && auth) {
    clearSession();
    unauthorizedHandler?.();
  }

  if (!res.ok) {
    const { message, body: errBody } = await extractErrorMessage(res);
    throw new ApiError(res.status, message, errBody);
  }

  if (res.status === 204) return undefined as T;
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}
