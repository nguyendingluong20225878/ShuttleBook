import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { readBrowserSession, useBrowserSession, type BrowserSession } from '@shuttlebook/ui/browser-session';
import { navigate } from '../../routes/navigation';
import { CustomerNotificationsLink } from '../notifications/CustomerNotifications';

export type Session = BrowserSession;
const base = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';
export const parseSession = (response: Response) => readBrowserSession(response, 'customer');
type Auth = ReturnType<typeof useBrowserSession> & { displayName: string; setDisplayName: (name: string) => void };
const Context = createContext<Auth | null>(null);
export function CustomerSessionProvider({ children }: { children: ReactNode }) {
  const auth = useBrowserSession('customer', base);
  const [displayName, updateDisplayName] = useState('');
  const profileGeneration = useRef(0);
  const setDisplayName = useCallback((name: string) => {
    profileGeneration.current++;
    updateDisplayName(name.trim());
  }, []);
  useEffect(() => {
    const generation = ++profileGeneration.current;
    updateDisplayName('');
    if (!auth.session) return;
    const controller = new AbortController();
    void auth.request('/api/v1/auth/profile', { signal: controller.signal }).then(async response => {
      if (!response.ok) return null;
      return (await response.json() as { data?: { displayName?: string | null } }).data;
    }).then(profile => {
      if (!controller.signal.aborted && generation === profileGeneration.current)
        updateDisplayName(profile?.displayName?.trim() ?? '');
    }).catch(() => {});
    return () => controller.abort();
  }, [auth.session?.accessToken, auth.request]);
  const logout = useCallback(async () => {
    try { await auth.logout(); } finally { navigate('/login', true); }
  }, [auth.logout]);
  return <Context.Provider value={{ ...auth, logout, displayName, setDisplayName }}>
    {auth.restoring ? <main className="auth-page" role="status">Đang khôi phục phiên đăng nhập…</main>
      : auth.restoreError ? <main className="auth-page"><p role="alert">{auth.restoreError}</p>
        <button type="button" onClick={() => void auth.retryRestore()}>Thử lại kết nối</button></main> : children}
  </Context.Provider>;
}
export function useCustomerSession() { const value = useContext(Context); if (!value) throw new Error('Missing CustomerSessionProvider'); return value; }
export function CustomerNav() {
  const { session, logout } = useCustomerSession();
  return <nav><a href="/venues">Tìm sân</a>{session ? <><a href="/me/bookings">Đơn của tôi</a><CustomerNotificationsLink /><a href="/me/profile">Hồ sơ</a>
    <button type="button" onClick={() => { void logout().catch(() => undefined); }}>Đăng xuất</button></> : <a href="/login">Đăng nhập</a>}</nav>;
}
