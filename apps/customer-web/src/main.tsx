import { FormEvent, StrictMode, useEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';

type ContactType = 'email' | 'phone';
type Page = 'register' | 'verify' | 'login';
type Session = { accessToken: string; refreshToken: string; expiresAt: number };
type AuthPayload = { data?: { tokenType: string; accessToken: string; refreshToken: string; expiresInSeconds: number; user: { accountType: string; status: string } } };
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';

function pageFromPath(): Page {
  return location.pathname === '/login' ? 'login' : location.pathname === '/verify' ? 'verify' : 'register';
}

function pendingContact(): { contactType: ContactType; contact: string } | null {
  const value = history.state as { pendingContact?: { contactType?: unknown; contact?: unknown } } | null;
  if (value?.pendingContact && (value.pendingContact.contactType === 'email' || value.pendingContact.contactType === 'phone')
      && typeof value.pendingContact.contact === 'string') {
    return { contactType: value.pendingContact.contactType, contact: value.pendingContact.contact };
  }
  return null;
}

function retryMessage(response: Response): string {
  const retryAfter = Number(response.headers.get('Retry-After'));
  const wait = Number.isFinite(retryAfter) && retryAfter > 0
    ? ` Vui lòng đợi khoảng ${Math.max(1, Math.ceil(retryAfter / 60))} phút rồi thử lại.`
    : ' Vui lòng thử lại sau.';
  return `Bạn đã gửi quá nhiều yêu cầu.${wait}`;
}

async function post(path: string, body: object, bearer?: string): Promise<Response> {
  return fetch(`${apiBaseUrl}/api/v1/auth${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...(bearer ? { Authorization: `Bearer ${bearer}` } : {}) },
    body: JSON.stringify(body),
  });
}

async function parseSession(response: Response): Promise<Session | null> {
  if (!response.ok) return null;
  const data = (await response.json() as AuthPayload).data;
  if (!data || data.tokenType !== 'Bearer' || data.user?.accountType !== 'CUSTOMER' || data.user?.status !== 'ACTIVE' ||
      !data.accessToken || !data.refreshToken || !Number.isFinite(data.expiresInSeconds) || data.expiresInSeconds <= 0) return null;
  return { accessToken: data.accessToken, refreshToken: data.refreshToken, expiresAt: Date.now() + data.expiresInSeconds * 1000 };
}

function CustomerIdentity() {
  const [page, setPage] = useState<Page>(pageFromPath);
  const [contactType, setContactType] = useState<ContactType>(() => pendingContact()?.contactType ?? 'email');
  const [contact, setContact] = useState(() => pendingContact()?.contact ?? '');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [code, setCode] = useState('');
  const [session, setSession] = useState<Session | null>(null);
  const [message, setMessage] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const refreshInFlight = useRef<Promise<void> | null>(null);
  const sessionGeneration = useRef(0);

  useEffect(() => {
    const onPopState = () => {
      const pending = pendingContact();
      setPage(pageFromPath());
      if (pending) { setContactType(pending.contactType); setContact(pending.contact); }
      setMessage('');
    };
    addEventListener('popstate', onPopState);
    return () => removeEventListener('popstate', onPopState);
  }, []);

  const navigate = (next: Page, state: object | null = null) => {
    history.pushState(state, '', next === 'register' ? '/' : `/${next}`);
    setPage(next); setMessage('');
  };

  useEffect(() => {
    if (!session) return;
    const timer = window.setTimeout(() => {
      if (refreshInFlight.current) return;
      const generation = sessionGeneration.current;
      refreshInFlight.current = (async () => {
        try {
          const next = await parseSession(await post('/refresh', { refreshToken: session.refreshToken }));
          if (!next) throw new Error('Refresh rejected');
          if (generation === sessionGeneration.current) setSession(next);
        } catch {
          if (generation === sessionGeneration.current) {
            sessionGeneration.current++;
            setSession(null); navigate('login');
            setMessage('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
          }
        } finally { refreshInFlight.current = null; }
      })();
    }, Math.max(0, session.expiresAt - Date.now() - 30_000));
    return () => window.clearTimeout(timer);
  }, [session]);

  const submitRegistration = async (event: FormEvent) => {
    event.preventDefault();
    if (password !== confirmPassword) { setMessage('Mật khẩu nhập lại chưa khớp.'); return; }
    setSubmitting(true); setMessage('');
    try {
      const response = await post('/register', { contactType, contact, password });
      if (response.status === 202) {
        setPassword(''); setConfirmPassword(''); setCode('');
        navigate('verify', { pendingContact: { contactType, contact } });
        setMessage('Nếu thông tin hợp lệ, mã xác minh đã được gửi.');
      } else {
        const failure = (await response.json() as { code?: string }).code;
        setMessage(response.status === 429 ? retryMessage(response) : failure === 'IDENTITY_DELIVERY_UNAVAILABLE'
          ? 'Dịch vụ gửi mã đang bận. Mã chưa được gửi. Vui lòng thử gửi lại sau.'
          : 'Thông tin chưa hợp lệ. Kiểm tra email/số điện thoại và mật khẩu (từ 12 ký tự, gồm ít nhất 3 nhóm ký tự).');
      }
    } catch { setMessage('Không thể kết nối máy chủ. Vui lòng thử lại.'); }
    finally { setSubmitting(false); }
  };

  const submitVerification = async (event: FormEvent) => {
    event.preventDefault();
    if (!contact) { setMessage('Không còn thông tin đăng ký trong trang này. Hãy quay lại đăng ký để nhận mã mới.'); return; }
    setSubmitting(true); setMessage('');
    try {
      const response = await post('/verify-contact', { contactType, contact, code });
      if (response.ok) {
        setCode(''); navigate('login');
        setMessage('Xác minh thành công. Hãy đăng nhập để tiếp tục.');
      } else if (response.status === 429) setMessage(retryMessage(response));
      else setMessage('Mã xác minh không hợp lệ hoặc đã hết hạn.');
    } catch { setMessage('Không thể kết nối máy chủ. Vui lòng thử lại.'); }
    finally { setSubmitting(false); }
  };

  const resend = async () => {
    if (!contact) { setMessage('Không còn thông tin đăng ký trong trang này. Hãy quay lại đăng ký để nhận mã mới.'); return; }
    setSubmitting(true); setMessage('');
    try {
      const response = await post('/verification-resend', { contactType, contact });
      setMessage(response.status === 202
        ? 'Nếu liên hệ đang chờ xác minh, mã mới đã được gửi. Hãy kiểm tra Mailpit và cả thư mục spam.'
        : response.status === 429 ? retryMessage(response)
          : response.status === 503 ? 'Mailpit chưa nhận được yêu cầu gửi mã. Kiểm tra dịch vụ rồi thử lại.'
            : 'Chưa thể gửi lại mã. Vui lòng thử lại sau.');
    } catch { setMessage('Không thể kết nối máy chủ. Vui lòng thử lại.'); }
    finally { setSubmitting(false); }
  };

  const submitLogin = async (event: FormEvent) => {
    event.preventDefault(); setSubmitting(true); setMessage('');
    try {
      const response = await post('/login', { contactType, contact, password });
      const next = await parseSession(response);
      setPassword('');
      if (next) {
        sessionGeneration.current++;
        history.replaceState(null, '', '/login');
        setSession(next); setMessage('Đăng nhập thành công.');
      }
      else if (response.ok) setMessage('Tài khoản này không phải tài khoản khách đang hoạt động.');
      else if (response.status === 429) setMessage(retryMessage(response));
      else setMessage('Thông tin đăng nhập không hợp lệ.');
    } catch { setMessage('Không thể kết nối máy chủ. Vui lòng thử lại.'); }
    finally { setSubmitting(false); }
  };

  const logout = async () => {
    if (!session) return;
    const current = session;
    sessionGeneration.current++;
    setSession(null); setMessage('Đã đăng xuất.');
    try {
      const response = await post('/logout', { refreshToken: current.refreshToken }, current.accessToken);
      if (!response.ok) setMessage('Đã rời phiên trên trình duyệt. Vui lòng đăng nhập lại sau khi kiểm tra kết nối.');
    } catch { setMessage('Đã rời phiên trên trình duyệt. Vui lòng đăng nhập lại sau khi kiểm tra kết nối.'); }
  };

  return <main style={{ fontFamily: 'system-ui, sans-serif', maxWidth: 480, margin: '3rem auto', padding: '0 1rem' }}>
    <h1>ShuttleBook</h1>
    {session ? <section>
      <h2>Xin chào khách hàng</h2>
      <p>Phiên đăng nhập đang hoạt động. Bạn có thể tiếp tục sử dụng ShuttleBook.</p>
      <button type="button" onClick={logout}>Đăng xuất</button>
    </section> : <>
      <nav aria-label="Tài khoản">
        <button type="button" onClick={() => navigate('register')} disabled={page === 'register'}>Đăng ký</button>{' '}
        <button type="button" onClick={() => navigate('login')} disabled={page === 'login'}>Đăng nhập</button>
      </nav>
      {page === 'register' && <form onSubmit={submitRegistration}>
        <h2>Tạo tài khoản khách</h2>
        <label>Phương thức liên hệ<br /><select value={contactType} onChange={event => setContactType(event.target.value as ContactType)}><option value="email">Email</option><option value="phone">Số điện thoại</option></select></label><br /><br />
        <label>{contactType === 'email' ? 'Email' : 'Số điện thoại E.164'}<br /><input required value={contact} onChange={event => setContact(event.target.value)} placeholder={contactType === 'email' ? 'ban@example.com' : '+84901234567'} /></label><br /><br />
        <label>Mật khẩu<br /><input required type="password" minLength={12} value={password} onChange={event => setPassword(event.target.value)} /></label>
        <br /><br /><label>Nhập lại mật khẩu<br /><input required type="password" minLength={12} value={confirmPassword} onChange={event => setConfirmPassword(event.target.value)} /></label>
        <p><small>Tối thiểu 12 ký tự, gồm ít nhất 3 nhóm: chữ thường, chữ hoa, số, ký tự đặc biệt.</small></p>
        <button disabled={submitting} type="submit">Đăng ký</button>
      </form>}
      {page === 'verify' && <form onSubmit={submitVerification}>
        <h2>Xác minh tài khoản khách</h2>
        <p>Nhập mã 6 chữ số đã gửi đến liên hệ bạn vừa đăng ký.</p>
        {import.meta.env.DEV && <p><small>Môi trường local: xem mã tại <a href="http://localhost:8025" target="_blank" rel="noreferrer">Mailpit</a>; mã không được gửi đến hộp thư hoặc số điện thoại thật.</small></p>}
        <label>Mã xác minh 6 chữ số<br /><input required disabled={!contact} inputMode="numeric" pattern="[0-9]{6}" maxLength={6} value={code} onChange={event => setCode(event.target.value.replace(/\D/g, ''))} /></label><br /><br />
        <button disabled={submitting || !contact} type="submit">Xác minh</button>{' '}
        <button disabled={submitting || !contact} type="button" onClick={resend}>Gửi lại mã</button>
        {!contact && <p role="status">Không còn thông tin đăng ký trong trang này. <button type="button" onClick={() => navigate('register')}>Quay lại đăng ký</button></p>}
      </form>}
      {page === 'login' && <form onSubmit={submitLogin}>
        <h2>Đăng nhập khách hàng</h2>
        <label>Phương thức liên hệ<br /><select value={contactType} onChange={event => setContactType(event.target.value as ContactType)}><option value="email">Email</option><option value="phone">Số điện thoại</option></select></label><br /><br />
        <label>{contactType === 'email' ? 'Email' : 'Số điện thoại E.164'}<br /><input required value={contact} onChange={event => setContact(event.target.value)} placeholder={contactType === 'email' ? 'ban@example.com' : '+84901234567'} /></label><br /><br />
        <label>Mật khẩu<br /><input required type="password" value={password} onChange={event => setPassword(event.target.value)} /></label><br /><br />
        <button disabled={submitting} type="submit">Đăng nhập</button>
      </form>}
    </>}
    {message && <p role="status">{message}</p>}
  </main>;
}

createRoot(document.getElementById('root')!).render(<StrictMode><CustomerIdentity /></StrictMode>);
