import { FormEvent, StrictMode, useEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import './admin.css';

type ContactType = 'email' | 'phone';
type Session = { accessToken: string; refreshToken: string; expiresAt: number };
type AuthPayload = { data?: { tokenType?: string; accessToken?: string; refreshToken?: string;
  expiresInSeconds?: number; user?: { accountType?: string; status?: string } } };
type MePayload = { data?: { accountType?: string; status?: string } };
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';

async function post(path: string, body: object, bearer?: string): Promise<Response> {
  return fetch(`${apiBaseUrl}/api/v1/${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...(bearer ? { Authorization: `Bearer ${bearer}` } : {}) },
    body: JSON.stringify(body),
  });
}

async function readSession(response: Response): Promise<Session | null> {
  if (!response.ok) return null;
  const data = (await response.json() as AuthPayload).data;
  if (data?.tokenType !== 'Bearer' || data.user?.accountType !== 'ADMIN' || data.user.status !== 'ACTIVE' ||
      !data.accessToken || !data.refreshToken || !Number.isFinite(data.expiresInSeconds) ||
      (data.expiresInSeconds ?? 0) <= 0) return null;
  return { accessToken: data.accessToken, refreshToken: data.refreshToken,
    expiresAt: Date.now() + data.expiresInSeconds! * 1000 };
}

async function sessionIsCurrent(session: Session): Promise<boolean> {
  const response = await fetch(`${apiBaseUrl}/api/v1/admin-auth/me`, {
    headers: { Authorization: `Bearer ${session.accessToken}` },
  });
  if (!response.ok) return false;
  const data = (await response.json() as MePayload).data;
  return data?.accountType === 'ADMIN' && data.status === 'ACTIVE';
}

function AdminPortal() {
  const [contactType, setContactType] = useState<ContactType>('email');
  const [contact, setContact] = useState('');
  const [password, setPassword] = useState('');
  const [session, setSession] = useState<Session | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const refreshing = useRef(false);
  const sessionGeneration = useRef(0);

  useEffect(() => {
    if (!session) return;
    const generation = sessionGeneration.current;
    let cancelled = false;
    const timer = window.setTimeout(async () => {
      if (refreshing.current || cancelled || generation !== sessionGeneration.current) return;
      refreshing.current = true;
      try {
        const response = await post('auth/refresh', { refreshToken: session.refreshToken });
        const next = await readSession(response);
        if (!next || !await sessionIsCurrent(next)) {
          if (cancelled || generation !== sessionGeneration.current) return;
          setSession(null);
          setMessage('Phiên đăng nhập đã kết thúc. Vui lòng đăng nhập lại.');
        } else if (!cancelled && generation === sessionGeneration.current) setSession(next);
      } catch {
        if (cancelled || generation !== sessionGeneration.current) return;
        setSession(null);
        setMessage('Không thể gia hạn phiên. Vui lòng đăng nhập lại.');
      } finally {
        refreshing.current = false;
      }
    }, Math.max(1000, session.expiresAt - Date.now() - 30_000));
    return () => { cancelled = true; window.clearTimeout(timer); };
  }, [session]);

  async function login(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    const normalized = contact.trim();
    if (!normalized || !password || (contactType === 'email' && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(normalized)) ||
        (contactType === 'phone' && !/^\+[1-9]\d{7,14}$/.test(normalized))) {
      setMessage('Vui lòng kiểm tra thông tin đăng nhập.');
      return;
    }
    setBusy(true);
    setMessage('');
    try {
      const response = await post('admin-auth/login', { contactType, contact: normalized, password });
      setPassword('');
      if (response.status === 429) {
        const seconds = Number(response.headers.get('Retry-After'));
        setMessage(`Bạn đã thử quá nhiều lần. Vui lòng đợi ${Number.isFinite(seconds) && seconds > 0 ? Math.ceil(seconds / 60) : 1} phút.`);
        return;
      }
      const next = await readSession(response);
      if (!next || !await sessionIsCurrent(next)) {
        setMessage('Thông tin đăng nhập không hợp lệ.');
        return;
      }
      sessionGeneration.current++;
      setSession(next);
    } catch {
      setPassword('');
      setMessage('Không thể kết nối. Vui lòng thử lại.');
    } finally {
      setBusy(false);
    }
  }

  async function logout() {
    if (!session || busy) return;
    const outgoing = session;
    sessionGeneration.current++;
    setSession(null);
    setBusy(true);
    try {
      await post('auth/logout', { refreshToken: outgoing.refreshToken }, outgoing.accessToken);
    } catch {
      // The local session is cleared even if the network is unavailable.
    } finally {
      setSession(null);
      setBusy(false);
      setMessage('Đã đăng xuất.');
    }
  }

  return <main className="admin-page">
    <header className="admin-brand">ShuttleBook <span>Quản trị</span></header>
    <section className="admin-card">
      <p className="admin-eyebrow">CỔNG QUẢN TRỊ NỀN TẢNG</p>
      <h1>Quản trị ShuttleBook.</h1>
      {session ? <>
        <p className="admin-description">Bạn đã đăng nhập với tài khoản quản trị.</p>
        <div className="admin-notice"><strong>Chưa có module quản trị.</strong><br />Các chức năng duyệt cơ sở và vận hành sẽ được bổ sung ở các giai đoạn tiếp theo.</div>
        <button type="button" onClick={logout} disabled={busy}>{busy ? 'Đang đăng xuất…' : 'Đăng xuất'}</button>
      </> : <>
        <p className="admin-description">Đăng nhập bằng tài khoản Admin đã được khởi tạo qua quy trình vận hành nội bộ.</p>
        <form onSubmit={login} noValidate>
          <label htmlFor="contact-type">Phương thức liên hệ</label>
          <select id="contact-type" value={contactType} onChange={event => setContactType(event.target.value as ContactType)} disabled={busy}>
            <option value="email">Email</option><option value="phone">Số điện thoại</option>
          </select>
          <label htmlFor="contact">{contactType === 'email' ? 'Email' : 'Số điện thoại E.164'}</label>
          <input id="contact" type={contactType === 'email' ? 'email' : 'tel'} autoComplete="username" value={contact}
            onChange={event => setContact(event.target.value)} disabled={busy} />
          <label htmlFor="password">Mật khẩu</label>
          <input id="password" type="password" autoComplete="current-password" value={password}
            onChange={event => setPassword(event.target.value)} disabled={busy} />
          <button type="submit" disabled={busy}>{busy ? 'Đang đăng nhập…' : 'Đăng nhập'}</button>
        </form>
      </>}
      {message && <p role="status" className="admin-message">{message}</p>}
    </section>
    <footer>ShuttleBook · Cổng quản trị</footer>
  </main>;
}

createRoot(document.getElementById('root')!).render(<StrictMode><AdminPortal /></StrictMode>);
