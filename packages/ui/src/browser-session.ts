import { useCallback, useEffect, useRef, useState } from 'react';

export type BrowserSession = { accessToken: string; expiresAt: number; idleExpiresAt: number; refreshExpiresAt: number };
type Portal = 'customer' | 'partner';
export async function readBrowserSession(response: Response, portal: Portal): Promise<BrowserSession | null> {
  if (response.status === 401 || response.status === 403) return null;
  if (!response.ok) throw new Error('Không thể khôi phục phiên. Vui lòng thử lại.');
  const data = (await response.json()).data;
  const expected = portal === 'customer' ? 'CUSTOMER' : 'VENUE_OPERATOR';
  if (data?.tokenType !== 'Bearer' || data.user?.accountType !== expected ||
      !(portal === 'customer' ? ['ACTIVE'] : ['ACTIVE', 'PENDING_ONBOARDING']).includes(data.user?.status) ||
      typeof data.accessToken !== 'string' || !data.accessToken || !Number.isFinite(data.expiresInSeconds) || data.expiresInSeconds <= 0) return null;
  const idleExpiresAt = Date.parse(data.idleExpiresAt), refreshExpiresAt = Date.parse(data.refreshExpiresAt);
  if (!Number.isFinite(idleExpiresAt) || !Number.isFinite(refreshExpiresAt)) return null;
  return { accessToken: data.accessToken, expiresAt: Date.now() + data.expiresInSeconds * 1000, idleExpiresAt, refreshExpiresAt };
}

// Only the browser's HttpOnly cookie contains the refresh credential. The shared hook
// keeps access tokens in memory and treats user activity separately from polling.
export function useBrowserSession(portal: Portal, base: string) {
  const [session, render] = useState<BrowserSession | null>(null);
  const [restoring, setRestoring] = useState(true);
  const [restoreError, setRestoreError] = useState('');
  const [message, setMessage] = useState('');
  const current = useRef<BrowserSession | null>(null), epoch = useRef(0);
  const inFlight = useRef<Promise<BrowserSession | null> | null>(null);
  const channel = useRef<BroadcastChannel | null>(null);
  const lastSent = useRef(Date.now()), lastUserAction = useRef(0);
  const lastAcknowledgedAction = useRef(0);
  const activityFlight = useRef(false);
  const loginEpochs = useRef(new WeakMap<Response, number>());
  const root = `${base}/api/v1/browser-auth/${portal}`;
  const pendingKey = `shuttlebook_${portal}_logout_pending`;
  const rememberLogout = useCallback((pending: boolean) => {
    try { if (pending) localStorage.setItem(pendingKey, '1'); else localStorage.removeItem(pendingKey); } catch { /* Cookie revocation is authoritative. */ }
  }, [pendingKey]);
  const pendingLogout = useCallback(() => {
    try { return localStorage.getItem(pendingKey) === '1'; } catch { return false; }
  }, [pendingKey]);
  const post = useCallback((action: string, body: object = {}) => fetch(`${root}/${action}`, {
    method: 'POST', credentials: 'include', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
  }), [root]);
  const setSession = useCallback((value: BrowserSession | null) => {
    epoch.current++; current.current = value; render(value); setRestoreError('');
    inFlight.current = null;
    lastSent.current = Date.now(); lastUserAction.current = 0; lastAcknowledgedAction.current = 0;
  }, []);
  const expire = useCallback((notice = 'Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại.') => {
    setSession(null); setMessage(notice);
  }, [setSession]);
  const refresh = useCallback(async (): Promise<BrowserSession | null> => {
    if (inFlight.current) return inFlight.current;
    const generation = epoch.current;
    const pending = (async () => {
      if (pendingLogout()) {
        const loggedOut = await post('logout');
        if (!loggedOut.ok) throw new Error('Chưa thể xác nhận đăng xuất. Vui lòng thử lại khi có mạng.');
        rememberLogout(false); return null;
      }
      const previous = current.current;
      const next = await readBrowserSession(await post('restore'), portal);
      if (generation !== epoch.current) return null;
      current.current = next; render(next);
      if (!next && previous) setMessage('Phiên đăng nhập đã kết thúc. Hãy đăng nhập lại.');
      return next;
    })();
    inFlight.current = pending;
    try { return await pending; } finally { if (inFlight.current === pending) inFlight.current = null; }
  }, [pendingLogout, post, portal, rememberLogout]);
  const retryRestore = useCallback(async () => {
    const generation = epoch.current;
    setRestoring(true); setRestoreError('');
    try { await refresh(); } catch (error) {
      if (generation === epoch.current) setRestoreError(error instanceof Error ? error.message : 'Không thể khôi phục phiên. Vui lòng thử lại.');
    } finally { if (generation === epoch.current) setRestoring(false); }
  }, [refresh]);
  useEffect(() => { void retryRestore(); }, [retryRestore]);
  useEffect(() => {
    const bus = new BroadcastChannel(`shuttlebook-session-${portal}`); channel.current = bus;
    bus.onmessage = event => {
      if (event.data?.type === 'logout') { expire('Đã đăng xuất ở cửa sổ khác.'); setRestoring(false); }
      if (event.data?.type === 'login') { setSession(null); void retryRestore(); }
      if (event.data?.type === 'activity' && Number.isFinite(event.data.deadline) && current.current) {
        const next = { ...current.current, idleExpiresAt: Math.max(current.current.idleExpiresAt, event.data.deadline) };
        current.current = next; render(next);
      }
    };
    return () => { channel.current = null; bus.close(); };
  }, [portal, expire, setSession, retryRestore]);
  const login = useCallback(async (body: object): Promise<Response> => {
    setMessage('');
    // A pending network logout must be completed before replacing the cookie.
    if (pendingLogout()) {
      const result = await post('logout'); if (!result.ok) throw new Error('Chưa thể xác nhận đăng xuất.'); rememberLogout(false);
    }
    const generation = epoch.current;
    const response = await post('login', body);
    if (generation !== epoch.current) {
      // A late login can set a new HttpOnly cookie after another tab logged out.
      // Revoke that cookie before allowing any subsequent restore.
      rememberLogout(true);
      const result = await post('logout');
      if (result.ok) rememberLogout(false);
      throw new Error('SESSION_CHANGED');
    }
    loginEpochs.current.set(response, generation);
    return response;
  }, [pendingLogout, post, rememberLogout]);
  const acceptLogin = useCallback((value: BrowserSession) => {
    setSession(value); setMessage(''); channel.current?.postMessage({ type: 'login' });
  }, [setSession]);
  const completeLogin = useCallback(async (response: Response) => {
    const generation = loginEpochs.current.get(response);
    const next = await readBrowserSession(response, portal);
    if (generation === undefined || generation !== epoch.current) {
      rememberLogout(true);
      const result = await post('logout'); if (result.ok) rememberLogout(false);
      throw new Error('SESSION_CHANGED');
    }
    if (next) acceptLogin(next);
    return next;
  }, [portal, post, rememberLogout, acceptLogin]);
  const logout = useCallback(async () => {
    rememberLogout(true); setSession(null); channel.current?.postMessage({ type: 'logout' });
    try {
      const response = await post('logout');
      if (!response.ok) throw new Error('Máy chủ chưa xác nhận đăng xuất. Vui lòng thử lại.');
      rememberLogout(false); setMessage('Đã đăng xuất.');
    } catch (error) {
      const notice = 'Phiên trên trang đã đóng; máy chủ chưa xác nhận đăng xuất. Kết nối mạng rồi thử lại.';
      setMessage(notice); throw error;
    }
  }, [post, rememberLogout, setSession]);
  useEffect(() => {
    const onAction = (event: Event) => {
      if (!event.isTrusted || !current.current) return;
      if (Date.now() >= Math.min(current.current.idleExpiresAt, current.current.refreshExpiresAt)) { expire(); return; }
      lastUserAction.current = Date.now();
    };
    const tick = async () => {
      const active = current.current; if (!active) return;
      if (Date.now() >= Math.min(active.idleExpiresAt, active.refreshExpiresAt)) { expire(); return; }
      if (lastUserAction.current <= lastAcknowledgedAction.current || Date.now() - lastSent.current < 60_000 || activityFlight.current) return;
      activityFlight.current = true;
      const generation = epoch.current, sentAction = lastUserAction.current;
      try {
        const next = await readBrowserSession(await post('activity'), portal);
        if (generation !== epoch.current) return;
        lastSent.current = Date.now();
        lastAcknowledgedAction.current = sentAction;
        if (!next) { expire(); return; }
        current.current = next; render(next);
        channel.current?.postMessage({ type: 'activity', deadline: next.idleExpiresAt });
      } catch { /* Retry the pending real activity when connectivity returns. */ }
      finally { activityFlight.current = false; }
    };
    // Browser-generated scroll events can also come from layout/scrollIntoView.
    // Wheel/touch input counts as user activity; those automatic scrolls do not.
    for (const name of ['pointerdown', 'keydown', 'wheel', 'touchmove']) window.addEventListener(name, onAction, true);
    const timer = window.setInterval(() => { void tick(); }, 1000);
    return () => {
      clearInterval(timer);
      for (const name of ['pointerdown', 'keydown', 'wheel', 'touchmove']) window.removeEventListener(name, onAction, true);
    };
  }, [expire, post, portal]);
  const request = useCallback(async (path: string, options: RequestInit = {}) => {
    let active = current.current;
    if (active && Date.now() >= Math.min(active.idleExpiresAt, active.refreshExpiresAt)) { expire(); active = null; }
    if (active && active.expiresAt <= Date.now() + 5000) active = await refresh();
    if (!active) throw new Error('SESSION_REQUIRED');
    const generation = epoch.current;
    const call = (token: string) => fetch(`${base}${path}`, { ...options, headers: {
      ...Object.fromEntries(new Headers(options.headers)), Authorization: `Bearer ${token}`,
    } });
    let response = await call(active.accessToken);
    if (generation !== epoch.current) throw new DOMException('Phiên trên trang đã đóng.', 'AbortError');
    if (response.status === 401) {
      active = await refresh(); if (!active || generation !== epoch.current) throw new Error('SESSION_REQUIRED');
      response = await call(active.accessToken);
    }
    if (generation !== epoch.current) throw new DOMException('Phiên trên trang đã đóng.', 'AbortError');
    return response;
  }, [base, expire, refresh]);
  return { session, request, logout, login, completeLogin, refresh, restoring, restoreError, retryRestore, message, expire };
}
