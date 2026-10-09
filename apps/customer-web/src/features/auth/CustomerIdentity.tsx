import { FormEvent, useEffect, useRef, useState } from 'react';
import { useCustomerSession } from './CustomerSession';
import { navigate as go, safeReturnTo } from '../../routes/navigation';
import { CustomerShell } from '../../components/CustomerShell';

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
  const { session, completeLogin, login, message: sessionMessage } = useCustomerSession();
  const [message, setMessage] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const feedback = useRef<HTMLParagraphElement>(null);

  useEffect(() => { if (message && !submitting) feedback.current?.focus(); }, [message, submitting]);
  useEffect(() => {
    if (session && new URLSearchParams(location.search).has('returnTo')) go(safeReturnTo(), true);
  }, [session]);

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
      const response = await login({ contactType, contact, password });
      const next = await completeLogin(response);
      setPassword('');
      if (next) {
        setMessage('Đăng nhập thành công.');
        go(safeReturnTo(), true);
      }
      else if (response.ok) setMessage('Tài khoản này không phải tài khoản khách đang hoạt động.');
      else if (response.status === 429) setMessage(retryMessage(response));
      else setMessage('Thông tin đăng nhập không hợp lệ.');
    } catch (error) { setMessage(error instanceof Error && error.message === 'SESSION_CHANGED'
      ? 'Phiên đăng nhập đã thay đổi ở cửa sổ khác. Hãy thử lại.' : 'Không thể kết nối máy chủ. Vui lòng thử lại.'); }
    finally { setSubmitting(false); }
  };

  return <CustomerShell className="customer-identity" layout="account">
    <div className="identity-layout">
    <section className="identity-intro"><p className="eyebrow">Lịch chơi của bạn</p><h1>ShuttleBook</h1>
      <p>Tìm cơ sở, chọn sân và khung giờ phù hợp. Theo dõi đơn và xác nhận thanh toán trong cùng một nơi.</p>
      <a className="primary-link" href="/venues">Tìm sân gần bạn và xem lịch trống</a>
      <ol className="identity-steps" aria-label="Các bước đặt sân"><li>Tìm cơ sở và xem lịch trống</li>
        <li>Chọn khung giờ, xem báo giá</li><li>Chuyển khoản và chờ chủ sân xác nhận</li></ol>
    </section>
    <section className="identity-card">
    {sessionMessage && !session && <p role="status">{sessionMessage}</p>}
    {session ? <section>
      <h2>Xin chào khách hàng</h2>
      <p>Bạn đã đăng nhập. Chọn cơ sở để đặt sân hoặc xem trạng thái các đơn hiện có.</p>
      <div className="identity-actions"><a className="primary-link" href="/venues">Tìm sân &amp; xem lịch</a>
        <a className="secondary-link" href="/me/bookings">Xem đơn của tôi</a><a className="secondary-link" href="/me/notifications">Xem thông báo</a></div>
    </section> : <>
      <nav className="identity-tabs" aria-label="Tài khoản">
        <button type="button" onClick={() => navigate('register')} disabled={page === 'register'}>Đăng ký</button>
        <button type="button" onClick={() => navigate('login')} disabled={page === 'login'}>Đăng nhập</button>
      </nav>
      {page === 'register' && <form className="identity-form" onSubmit={submitRegistration} aria-busy={submitting}>
        <h2>Tạo tài khoản khách</h2>
        <p className="form-note">Tạo tài khoản để tiếp tục đặt sân và theo dõi thanh toán.</p>
        <label>Phương thức liên hệ<select value={contactType} onChange={event => setContactType(event.target.value as ContactType)}><option value="email">Email</option><option value="phone">Số điện thoại</option></select></label>
        <label>{contactType === 'email' ? 'Email' : 'Số điện thoại E.164'}<input required type={contactType === 'email' ? 'email' : 'tel'} autoComplete={contactType === 'email' ? 'email' : 'tel'} value={contact} onChange={event => setContact(event.target.value)} placeholder={contactType === 'email' ? 'ban@example.com' : '+84901234567'} /></label>
        <label>Mật khẩu<input required type="password" autoComplete="new-password" minLength={12} aria-describedby="customer-password-help" value={password} onChange={event => setPassword(event.target.value)} /></label>
        <label>Nhập lại mật khẩu<input required type="password" autoComplete="new-password" minLength={12} aria-invalid={message === 'Mật khẩu nhập lại chưa khớp.' ? true : undefined} value={confirmPassword} onChange={event => setConfirmPassword(event.target.value)} /></label>
        <p id="customer-password-help" className="form-note">Tối thiểu 12 ký tự, gồm ít nhất 3 nhóm: chữ thường, chữ hoa, số, ký tự đặc biệt.</p>
        <button disabled={submitting} type="submit">{submitting ? 'Đang đăng ký…' : 'Đăng ký'}</button>
      </form>}
      {page === 'verify' && <form className="identity-form" onSubmit={submitVerification} aria-busy={submitting}>
        <h2>Xác minh tài khoản khách</h2>
        <p>Nhập mã 6 chữ số đã gửi đến liên hệ bạn vừa đăng ký.</p>
        {import.meta.env.DEV && <p className="form-note">Môi trường local: xem mã tại <a href="http://localhost:8025" target="_blank" rel="noreferrer">Mailpit</a>; mã không được gửi đến hộp thư hoặc số điện thoại thật.</p>}
        <label>Mã xác minh 6 chữ số<input required disabled={!contact} inputMode="numeric" autoComplete="one-time-code" pattern="[0-9]{6}" maxLength={6} value={code} onChange={event => setCode(event.target.value.replace(/\D/g, ''))} /></label>
        <button disabled={submitting || !contact} type="submit">{submitting ? 'Đang xử lý…' : 'Xác minh'}</button>
        <button className="secondary-button" disabled={submitting || !contact} type="button" onClick={resend}>Gửi lại mã</button>
        {!contact && <p role="status">Không còn thông tin đăng ký trong trang này. <button type="button" onClick={() => navigate('register')}>Quay lại đăng ký</button></p>}
      </form>}
      {page === 'login' && <form className="identity-form" onSubmit={submitLogin} aria-busy={submitting}>
        <h2>Đăng nhập khách hàng</h2>
        <p className="form-note">Đăng nhập để tiếp tục khung giờ đã chọn hoặc xem đơn của bạn.</p>
        <label>Phương thức liên hệ<select value={contactType} onChange={event => setContactType(event.target.value as ContactType)}><option value="email">Email</option><option value="phone">Số điện thoại</option></select></label>
        <label>{contactType === 'email' ? 'Email' : 'Số điện thoại E.164'}<input required type={contactType === 'email' ? 'email' : 'tel'} autoComplete="username" value={contact} onChange={event => setContact(event.target.value)} placeholder={contactType === 'email' ? 'ban@example.com' : '+84901234567'} /></label>
        <label>Mật khẩu<input required type="password" autoComplete="current-password" value={password} onChange={event => setPassword(event.target.value)} /></label>
        <button disabled={submitting} type="submit">{submitting ? 'Đang đăng nhập…' : 'Đăng nhập'}</button>
      </form>}
    </>}
    {message && <p className="identity-feedback" role="status" tabIndex={-1} ref={feedback}>{message}</p>}
    </section></div>
  </CustomerShell>;
}
