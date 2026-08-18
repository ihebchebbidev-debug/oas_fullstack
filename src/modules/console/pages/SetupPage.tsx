import { useState } from 'react';
import { Loader2, Copy, Check, LogOut } from 'lucide-react';

import { cn } from '@/lib/utils';
import { getAuth, setupFirstAdmin, signInConsole, signOut, useAuth, type SetupError, type WebSignInError } from '@/oas/authStore';
import { operatorsApi } from '@/oas/api/referentials';
import { ApiError } from '@/oas/api/client';

/**
 * Hidden ops utility — never linked from any nav (same treatment as /test).
 * Not an open account-creation door: bootstrapping the first admin goes
 * through POST /api/oas/setup, which the backend already refuses once any
 * admin exists (spec decision, see authStore.setupFirstAdmin) — this page
 * doesn't change that. Creating additional users past bootstrap requires
 * actually being signed in as that admin (or an existing one via the
 * sign-in tab), through the same authenticated POST /operators endpoint
 * the real Users console screen uses — just without the extra navigation.
 */

type Created = { displayName: string; email: string; employeeCode: string; pin?: string; password?: string; pinFailed?: boolean };

function normalizeEmployeeCode(raw: string): string {
  const digits = raw.replace(/\D/g, '');
  return digits ? `EMP-${digits}` : raw.trim();
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="text-xs font-medium uppercase tracking-[0.04em] text-muted-foreground">{label}</span>
      <div className="mt-1">{children}</div>
    </label>
  );
}

const inputCls = 'h-10 w-full rounded-md border border-input bg-card px-3 text-sm text-foreground focus:border-ring focus:outline-none';

export default function SetupPage() {
  const auth = useAuth();
  const [tab, setTab] = useState<'bootstrap' | 'signin'>('bootstrap');

  if (!auth) return <AuthGate tab={tab} setTab={setTab} />;
  if (auth.role !== 'admin' && auth.role !== 'supervisor') {
    return (
      <Shell>
        <p className="text-sm text-destructive">Signed in as {auth.email}, role "{auth.role}" — this tool needs admin or supervisor.</p>
      </Shell>
    );
  }
  return <ManagePanel />;
}

function Shell({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen items-start justify-center bg-muted/30 p-6">
      <div className="w-full max-w-lg space-y-4 pt-10">
        <h1 className="text-lg font-semibold">OAS setup</h1>
        {children}
      </div>
    </div>
  );
}

function AuthGate({ tab, setTab }: { tab: 'bootstrap' | 'signin'; setTab: (t: 'bootstrap' | 'signin') => void }) {
  return (
    <Shell>
      <div className="flex gap-1 rounded-md bg-muted p-1 text-sm">
        <button
          type="button"
          onClick={() => setTab('bootstrap')}
          className={cn('flex-1 rounded px-3 py-1.5', tab === 'bootstrap' ? 'bg-card shadow-sm' : 'text-muted-foreground')}
        >
          Create first admin
        </button>
        <button
          type="button"
          onClick={() => setTab('signin')}
          className={cn('flex-1 rounded px-3 py-1.5', tab === 'signin' ? 'bg-card shadow-sm' : 'text-muted-foreground')}
        >
          Sign in
        </button>
      </div>
      {tab === 'bootstrap' ? <BootstrapForm onExists={() => setTab('signin')} /> : <SignInForm />}
    </Shell>
  );
}

function BootstrapForm({ onExists }: { onExists: () => void }) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const canSubmit = email.trim().length >= 3 && password.length >= 8 && displayName.trim().length > 0 && !loading;

  const submit = async () => {
    setLoading(true);
    const result: Awaited<ReturnType<typeof setupFirstAdmin>> = await setupFirstAdmin(email.trim(), password, displayName.trim());
    setLoading(false);
    if (typeof result === 'string') {
      const messages: Record<SetupError, string> = {
        exists: 'This tenant already has an admin — use "Sign in" instead.',
        network: 'Could not reach the server. Try again.',
      };
      setError(messages[result]);
      if (result === 'exists') onExists();
      return;
    }
    setError(null);
  };

  return (
    <form
      onSubmit={(e) => { e.preventDefault(); if (canSubmit) void submit(); }}
      className="space-y-3 rounded-xl border border-border bg-card p-5"
    >
      <p className="text-xs text-muted-foreground">
        Only works once — the server refuses if this tenant already has an admin. Safe to leave this tab open; it can't create a second one.
      </p>
      <Field label="Display name">
        <input className={inputCls} value={displayName} onChange={(e) => setDisplayName(e.target.value)} placeholder="Jane Doe" />
      </Field>
      <Field label="Email">
        <input className={inputCls} type="email" autoComplete="username" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="admin@company.com" />
      </Field>
      <Field label="Password (min 8 chars)">
        <input className={inputCls} type="password" autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} />
      </Field>
      {error && <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive">{error}</p>}
      <button
        type="submit"
        disabled={!canSubmit}
        className={cn('h-10 w-full rounded-md text-sm font-semibold', canSubmit ? 'bg-primary text-primary-foreground hover:bg-primary/90' : 'cursor-not-allowed bg-muted text-muted-foreground')}
      >
        {loading ? <Loader2 className="mx-auto h-4 w-4 animate-spin" /> : 'Create admin & sign in'}
      </button>
    </form>
  );
}

function SignInForm() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const canSubmit = email.trim().length >= 3 && password.length > 0 && !loading;

  const submit = async () => {
    setLoading(true);
    const result: Awaited<ReturnType<typeof signInConsole>> = await signInConsole(email.trim(), password);
    setLoading(false);
    if (typeof result === 'string') {
      const messages: Record<WebSignInError, string> = {
        unknown: 'No account with that email.',
        badPassword: 'Wrong password.',
        inactive: 'That account is deactivated.',
        noAccess: 'That account is an operator — needs admin or supervisor.',
        network: 'Could not reach the server.',
      };
      setError(messages[result]);
    }
  };

  return (
    <form
      onSubmit={(e) => { e.preventDefault(); if (canSubmit) void submit(); }}
      className="space-y-3 rounded-xl border border-border bg-card p-5"
    >
      <Field label="Email">
        <input className={inputCls} type="email" autoComplete="username" value={email} onChange={(e) => setEmail(e.target.value)} />
      </Field>
      <Field label="Password">
        <input className={inputCls} type="password" autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} />
      </Field>
      {error && <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive">{error}</p>}
      <button
        type="submit"
        disabled={!canSubmit}
        className={cn('h-10 w-full rounded-md text-sm font-semibold', canSubmit ? 'bg-primary text-primary-foreground hover:bg-primary/90' : 'cursor-not-allowed bg-muted text-muted-foreground')}
      >
        {loading ? <Loader2 className="mx-auto h-4 w-4 animate-spin" /> : 'Sign in'}
      </button>
    </form>
  );
}

function ManagePanel() {
  const auth = getAuth();
  const isAdmin = auth?.role === 'admin';
  const roles: readonly ('operator' | 'supervisor' | 'admin')[] = isAdmin ? ['operator', 'supervisor', 'admin'] : ['operator', 'supervisor'];
  const [role, setRole] = useState<'operator' | 'supervisor' | 'admin'>('operator');
  const [displayName, setDisplayName] = useState('');
  const [employeeCode, setEmployeeCode] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [created, setCreated] = useState<Created[]>([]);

  const isWeb = role === 'supervisor' || role === 'admin';
  const canSubmit =
    displayName.trim().length > 0 &&
    (role !== 'operator' || employeeCode.trim().length > 0) &&
    (!isWeb || password.length >= 8) &&
    !loading;

  const submit = async () => {
    setLoading(true);
    setError(null);
    try {
      const email = `${displayName.trim().toLowerCase().replace(/\s+/g, '.')}.${Date.now().toString(36)}@oas.local`;
      const code = role === 'operator' ? normalizeEmployeeCode(employeeCode) : undefined;
      const dto = await operatorsApi.create({
        email, displayName: displayName.trim(), employeeCode: code,
        role, workspace: role === 'operator' ? 'mobile' : 'web',
        password: isWeb ? password : undefined,
      });
      // The account now exists regardless of what happens next — a failure
      // past this point must still show up below, not get reported as
      // "failed to create" (which it didn't) while silently leaving behind
      // an unusable account nobody knows to go find in the real Users screen.
      let pin: string | undefined;
      let pinFailed = false;
      if (role === 'operator') {
        try {
          const pinResult = await operatorsApi.regeneratePin(dto.id);
          pin = pinResult.pin;
        } catch {
          pinFailed = true;
        }
      }
      setCreated((prev) => [{ displayName: displayName.trim(), email, employeeCode: code ?? '—', pin, pinFailed, password: isWeb ? password : undefined }, ...prev]);
      setDisplayName('');
      setEmployeeCode('');
      setPassword('');
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Failed to create user.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Shell>
      <div className="flex items-center justify-between rounded-md border border-border bg-card px-3 py-2 text-xs text-muted-foreground">
        <span>Signed in as {auth?.email} ({auth?.role})</span>
        <button type="button" onClick={() => void signOut()} className="flex items-center gap-1 text-foreground hover:underline">
          <LogOut className="h-3 w-3" /> Sign out
        </button>
      </div>

      <form
        onSubmit={(e) => { e.preventDefault(); if (canSubmit) void submit(); }}
        className="space-y-3 rounded-xl border border-border bg-card p-5"
      >
        <p className="text-xs text-muted-foreground">
          Creates a real account via the same authenticated endpoint the Users console screen uses. Operators log into the mobile app with a badge + 4-digit PIN (generated for you); supervisors/admins log into the web console with the email + password below — both shown once, right after creation.
        </p>
        <Field label="Role">
          <div className="flex gap-1 rounded-md bg-muted p-1 text-sm">
            {roles.map((r) => (
              <button
                key={r}
                type="button"
                onClick={() => setRole(r)}
                className={cn('flex-1 rounded px-3 py-1.5 capitalize', role === r ? 'bg-card shadow-sm' : 'text-muted-foreground')}
              >
                {r === 'operator' ? 'Operator / technician' : r}
              </button>
            ))}
          </div>
          {!isAdmin && <p className="mt-1 text-[0.6875rem] text-muted-foreground">Creating another admin needs an admin caller — you're signed in as supervisor.</p>}
        </Field>
        <Field label="Display name">
          <input className={inputCls} value={displayName} onChange={(e) => setDisplayName(e.target.value)} placeholder="John Smith" />
        </Field>
        {role === 'operator' && (
          <Field label="Badge / employee number (digits)">
            <input className={inputCls} value={employeeCode} onChange={(e) => setEmployeeCode(e.target.value)} placeholder="999102" inputMode="numeric" />
          </Field>
        )}
        {isWeb && (
          <Field label="Password (min 8 chars) — for web console sign-in">
            <input className={inputCls} type="password" autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} />
          </Field>
        )}
        {error && <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive">{error}</p>}
        <button
          type="submit"
          disabled={!canSubmit}
          className={cn('h-10 w-full rounded-md text-sm font-semibold', canSubmit ? 'bg-primary text-primary-foreground hover:bg-primary/90' : 'cursor-not-allowed bg-muted text-muted-foreground')}
        >
          {loading ? <Loader2 className="mx-auto h-4 w-4 animate-spin" /> : 'Create'}
        </button>
      </form>

      {created.length > 0 && (
        <div className="space-y-2">
          <h2 className="text-xs font-medium uppercase tracking-[0.04em] text-muted-foreground">Created this session</h2>
          {created.map((c, i) => <CreatedCard key={i} c={c} />)}
        </div>
      )}
    </Shell>
  );
}

function CreatedCard({ c }: { c: Created }) {
  const [copied, setCopied] = useState(false);
  const secret = c.pin ?? c.password;
  const secretLabel = c.pin ? 'PIN — shown once, write it down' : 'Password you set — email below is auto-generated, save it too';
  return (
    <div className="rounded-lg border border-border bg-card p-4 text-sm">
      <p className="font-medium">{c.displayName}</p>
      <p className="text-xs text-muted-foreground">{c.pin || c.pinFailed ? c.employeeCode : c.email}</p>
      {c.pinFailed && (
        <p className="mt-2 rounded-md border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-xs text-amber-700 dark:text-amber-400">
          Account created, but PIN generation failed — this operator can't sign in yet. Go to Referentials → Users and use "Regenerate PIN" for {c.employeeCode}.
        </p>
      )}
      {secret && (
        <div className="mt-2 flex items-center justify-between rounded-md bg-muted px-3 py-2">
          <div>
            <p className="text-[0.6875rem] uppercase tracking-wide text-muted-foreground">{secretLabel}</p>
            <p className="font-mono text-lg tracking-widest">{secret}</p>
          </div>
          <button
            type="button"
            onClick={() => { void navigator.clipboard.writeText(secret); setCopied(true); setTimeout(() => setCopied(false), 1500); }}
            className="grid h-8 w-8 place-items-center rounded-md text-muted-foreground hover:bg-background hover:text-foreground"
            aria-label="Copy"
          >
            {copied ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
          </button>
        </div>
      )}
    </div>
  );
}
