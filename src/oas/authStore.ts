/**
 * Signed-in identity, backed by the real OAS auth endpoints (spec §8.2).
 * Console sign-in hits `POST /api/oas/auth/login` (email + password, checked
 * server-side — no shared demo password). Shop-floor sign-in hits
 * `POST /api/oas/shopfloor/login` with the polymorphic `{mode:"pin"}` /
 * `{mode:"qr"}` body. Both persist the JWT pair via `oas/api/client`.
 */

import { useSyncExternalStore } from 'react';
import { apiFetch, clearSession, getSession, setSession, setUnauthorizedHandler, ApiError } from './api/client';
import { pushToast } from '@/shared/lib/toast';

/** Mirrors the backend's `oas_app_role` enum (spec §8.0) — the only 3 roles that exist server-side. */
export type OasRole = 'admin' | 'supervisor' | 'operator';
export type Workspace = 'web' | 'mobile';

export interface AuthUser {
  id: string;
  email: string;
  displayName: string | null;
  role: OasRole;
  isActive: boolean;
  scopeSiteId: string | null;
  scopeZoneId: string | null;
  scopeLineId: string | null;
  /** Which shell this session signed into — web console or mobile shop floor. */
  workspace: Workspace;
}

interface AuthUserDto {
  id: string;
  email: string;
  displayName: string | null;
  role: string;
  workspace: string;
  isActive: boolean;
  scopeSiteId: string | null;
  scopeZoneId: string | null;
  scopeLineId: string | null;
}

interface AuthResponseDto {
  success: boolean;
  message?: string;
  accessToken?: string;
  refreshToken?: string;
  expiresAt?: string;
  user?: AuthUserDto;
}

const USER_KEY = 'oas.auth.user.v1';

function readUser(): AuthUser | null {
  try {
    const raw = localStorage.getItem(USER_KEY);
    return raw ? (JSON.parse(raw) as AuthUser) : null;
  } catch {
    return null;
  }
}

let current: AuthUser | null = typeof window === 'undefined' ? null : (getSession() ? readUser() : null);
const listeners = new Set<() => void>();

function commit(next: AuthUser | null) {
  current = next;
  try {
    if (next) localStorage.setItem(USER_KEY, JSON.stringify(next));
    else localStorage.removeItem(USER_KEY);
  } catch {
    /* private mode — keep the in-memory copy */
  }
  listeners.forEach((l) => l());
}

function subscribe(l: () => void) {
  listeners.add(l);
  return () => listeners.delete(l);
}

// A hard 401 (post-refresh-attempt) means the token is dead — drop the
// locally cached identity too, so `useAuth()` reactively kicks the app back
// to the login screen instead of showing stale data that can no longer load.
setUnauthorizedHandler(() => {
  commit(null);
  pushToast('auth.sessionExpired');
});

export function getAuth(): AuthUser | null {
  return current;
}

// A page reload trusts the cached identity in localStorage instantly (so
// the UI doesn't flash a login screen), but that cache could be stale — the
// account may have been deactivated, its role/scope changed, or the token
// revoked server-side since it was written. Validate once per load against
// `GET /auth/me`, the same pattern `ensureSessionValid` uses for the
// operator's post session.
let authChecked = false;
function ensureAuthValid() {
  if (authChecked || !current) return;
  authChecked = true;
  void apiFetch<AuthUserDto>('/auth/me')
    .then((dto) => commit(toAuthUser(dto, dto.workspace as Workspace)))
    .catch(() => {
      commit(null);
      pushToast('auth.sessionExpired');
    });
}

export function useAuth(): AuthUser | null {
  ensureAuthValid();
  return useSyncExternalStore(subscribe, () => current, () => null);
}

function toAuthUser(dto: AuthUserDto, workspace: Workspace): AuthUser {
  return {
    id: dto.id,
    email: dto.email,
    displayName: dto.displayName,
    role: dto.role as OasRole,
    isActive: dto.isActive,
    scopeSiteId: dto.scopeSiteId,
    scopeZoneId: dto.scopeZoneId,
    scopeLineId: dto.scopeLineId,
    workspace,
  };
}

/** Console roles — an `operator` account never receives a console token (spec §8.0, enforced server-side too). */
const CONSOLE_ROLES: OasRole[] = ['admin', 'supervisor'];

export type WebSignInError = 'unknown' | 'inactive' | 'noAccess' | 'badPassword' | 'network';

/** Console sign-in (`POST /api/oas/auth/login`) — real credentials, gated server-side. */
export async function signInConsole(email: string, password: string): Promise<AuthUser | WebSignInError> {
  let result: AuthResponseDto;
  try {
    result = await apiFetch<AuthResponseDto>('/auth/login', { method: 'POST', auth: false, body: { email, password } });
  } catch (e) {
    if (e instanceof ApiError && (e.status === 401 || e.status === 400)) return 'unknown';
    return 'network';
  }
  if (!result.success || !result.accessToken || !result.user) return 'badPassword';
  if (!result.user.isActive) return 'inactive';
  if (!CONSOLE_ROLES.includes(result.user.role as OasRole)) return 'noAccess';
  setSession({ accessToken: result.accessToken, refreshToken: result.refreshToken ?? '', expiresAt: result.expiresAt ?? null });
  const user = toAuthUser(result.user, 'web');
  commit(user);
  return user;
}

export type SetupError = 'exists' | 'network';

/**
 * One-time bootstrap (`POST /api/oas/setup`) — the server itself refuses
 * (`Success: false`) if an admin already exists for this tenant, so this is
 * safe to attempt blindly; it can never overwrite or conflict with a real
 * account. Used by the `/test` diagnostics page to fully self-provision
 * when there is truly no one to sign in as yet (a fresh tenant) — on any
 * tenant that already has a real admin, this is a guaranteed no-op.
 */
export async function setupFirstAdmin(email: string, password: string, displayName: string): Promise<AuthUser | SetupError> {
  let result: AuthResponseDto;
  try {
    result = await apiFetch<AuthResponseDto>('/setup', { method: 'POST', auth: false, body: { email, password, displayName } });
  } catch {
    return 'network';
  }
  if (!result.success || !result.accessToken || !result.user) return 'exists';
  setSession({ accessToken: result.accessToken, refreshToken: result.refreshToken ?? '', expiresAt: result.expiresAt ?? null });
  const user = toAuthUser(result.user, 'web');
  commit(user);
  return user;
}

export type ShopFloorSignInError = 'invalid' | 'inactive' | 'network';

async function shopFloorLogin(body: { mode: 'pin'; badge: string; pin: string } | { mode: 'qr'; token: string }): Promise<AuthUser | ShopFloorSignInError> {
  let result: AuthResponseDto;
  try {
    result = await apiFetch<AuthResponseDto>('/shopfloor/login', { method: 'POST', auth: false, body });
  } catch (e) {
    if (e instanceof ApiError && (e.status === 401 || e.status === 400)) return 'invalid';
    return 'network';
  }
  if (!result.success || !result.accessToken || !result.user) return 'invalid';
  if (!result.user.isActive) return 'inactive';
  setSession({ accessToken: result.accessToken, refreshToken: result.refreshToken ?? '', expiresAt: result.expiresAt ?? null });
  const user = toAuthUser(result.user, 'mobile');
  commit(user);
  return user;
}

/** Mobile PIN sign-in (BL-014) — matricule + one-shot PIN, checked server-side. */
export function signInWithPin(badge: string, pin: string): Promise<AuthUser | ShopFloorSignInError> {
  return shopFloorLogin({ mode: 'pin', badge, pin });
}

/** Mobile QR/badge sign-in — the raw string decoded from the camera. */
export function signInWithBadgeToken(token: string): Promise<AuthUser | ShopFloorSignInError> {
  return shopFloorLogin({ mode: 'qr', token });
}

export async function signOut(): Promise<void> {
  try {
    await apiFetch('/auth/logout', { method: 'POST' });
  } catch {
    /* best-effort — a dead/expired token shouldn't block clearing local state */
  }
  clearSession();
  commit(null);
}
