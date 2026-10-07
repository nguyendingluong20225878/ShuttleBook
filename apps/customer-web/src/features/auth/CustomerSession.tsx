import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { navigate } from '../../routes/navigation';
import { CustomerNotificationsLink } from '../notifications/CustomerNotifications';

export type Session = { accessToken: string; refreshToken: string; expiresAt: number };
const base = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';
export async function parseSession(response: Response): Promise<Session | null> {
  if (!response.ok) return null;
  const data = (await response.json()).data;
  if (data?.tokenType !== 'Bearer' || data.user?.accountType !== 'CUSTOMER' || data.user?.status !== 'ACTIVE' ||
      !data.accessToken || !data.refreshToken || !Number.isFinite(data.expiresInSeconds) || data.expiresInSeconds <= 0) return null;
  return { accessToken: data.accessToken, refreshToken: data.refreshToken, expiresAt: Date.now() + data.expiresInSeconds * 1000 };
}
type Auth = { session: Session | null; setSession: (value: Session | null) => void;
  request: (path: string, options?: RequestInit) => Promise<Response>; logout: () => Promise<void> };
const Context = createContext<Auth | null>(null);
export function CustomerSessionProvider({ children }: { children: ReactNode }) {
  const [session, render] = useState<Session | null>(null);
  const current = useRef<Session | null>(null); const generation = useRef(0);
  const inFlight = useRef<Promise<Session | null> | null>(null);
  const setSession = useCallback((value: Session | null) => { generation.current++; current.current = value; render(value); }, []);
  const refresh = useCallback(async (): Promise<Session | null> => {
    if (inFlight.current) return inFlight.current;
    const previous = current.current; if (!previous) return null;
    const epoch = generation.current;
    const pending = (async () => {
      try {
        const next = await parseSession(await fetch(`${base}/api/v1/auth/refresh`, { method: 'POST',
          headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ refreshToken: previous.refreshToken }) }));
        if (generation.current !== epoch) return null;
        current.current = next; render(next); return next;
      } catch { if (generation.current === epoch) { current.current = null; render(null); } return null; }
    })();
    inFlight.current = pending;
    try { return await pending; } finally { if (inFlight.current === pending) inFlight.current = null; }
  }, []);
  useEffect(() => {
    if (!session) return;
    const timer = setTimeout(() => { void refresh(); }, Math.max(0, session.expiresAt - Date.now() - 30_000));
    return () => clearTimeout(timer);
  }, [session, refresh]);
  const request = useCallback(async (path: string, options: RequestInit = {}) => {
    let active = current.current;
    if (active && active.expiresAt <= Date.now() + 5000) active = await refresh();
    if (!active) throw new Error('SESSION_REQUIRED');
    const call = (token: string) => fetch(`${base}${path}`, { ...options, headers: { ...Object.fromEntries(new Headers(options.headers)), Authorization: `Bearer ${token}` } });
    let response = await call(active.accessToken);
    if (response.status === 401) { active = await refresh(); if (!active) throw new Error('SESSION_REQUIRED'); response = await call(active.accessToken); }
    return response;
  }, [refresh]);
  const logout = useCallback(async () => {
    const previous = current.current; setSession(null);
    navigate('/login', true);
    if (previous) await fetch(`${base}/api/v1/auth/logout`, { method: 'POST', headers: { 'Content-Type': 'application/json',
      Authorization: `Bearer ${previous.accessToken}` }, body: JSON.stringify({ refreshToken: previous.refreshToken }) });
  }, [setSession]);
  return <Context.Provider value={{ session, setSession, request, logout }}>{children}</Context.Provider>;
}
export function useCustomerSession() { const value = useContext(Context); if (!value) throw new Error('Missing CustomerSessionProvider'); return value; }
export function CustomerNav() {
  const { session, logout } = useCustomerSession();
  return <nav><a href="/venues">Tìm sân</a>{session ? <><a href="/me/bookings">Đơn của tôi</a><CustomerNotificationsLink />
    <button type="button" onClick={() => { void logout().catch(() => undefined); }}>Đăng xuất</button></> : <a href="/login">Đăng nhập</a>}</nav>;
}
