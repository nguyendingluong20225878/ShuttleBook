import { FormEvent, useEffect, useState } from 'react';
import { parseSession, useCustomerSession } from './CustomerSession';
import { navigate as go, safeReturnTo } from '../../routes/navigation';

type ContactType = 'email' | 'phone';
type Page = 'register' | 'verify' | 'login';
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

export function CustomerIdentity() {
  const [page, setPage] = useState<Page>(pageFromPath);
  const [contactType, setContactType] = useState<ContactType>(() => pendingContact()?.contactType ?? 'email');
  const [contact, setContact] = useState(() => pendingContact()?.contact ?? '');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [code, setCode] = useState('');
  const { session, setSession, logout: sharedLogout } = useCustomerSession();
  const [message, setMessage] = useState('');
  const [submitting, setSubmitting] = useState(false);

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
    go(`${next === 'register' ? '/' : `/${next}`}${location.search}`, false, state);
    setPage(next); setMessage('');
  };

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
        ? 'Nếu liên hệ đang chờ xác minh, mã mới đã được gửi. Hãy kiểm tra hộp thư hoặc tin nhắn.'
        : response.status === 429 ? retryMessage(response)
          : response.status === 503 ? 'Dịch vụ gửi mã đang bận. Vui lòng thử lại sau.'
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
        setSession(next); setMessage('Đăng nhập thành công.');
        go(safeReturnTo(), true);
      }
      else if (response.ok) setMessage('Tài khoản này không phải tài khoản khách đang hoạt động.');
      else if (response.status === 429) setMessage(retryMessage(response));
      else setMessage('Thông tin đăng nhập không hợp lệ.');
    } catch { setMessage('Không thể kết nối máy chủ. Vui lòng thử lại.'); }
    finally { setSubmitting(false); }
  };

  const logout = async () => {
    if (!session) return;
    setMessage('Đã đăng xuất.');
    try {
      await sharedLogout();
    } catch { setMessage('Đã rời phiên trên trình duyệt. Vui lòng đăng nhập lại sau khi kiểm tra kết nối.'); }
  };

  return <main style={{ fontFamily: 'system-ui, sans-serif', maxWidth: 480, margin: '3rem auto', padding: '0 1rem' }}>
    <h1>ShuttleBook</h1>
    <p><a href="/venues">Tìm sân gần bạn và xem lịch trống</a></p>
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
