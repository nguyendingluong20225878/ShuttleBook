import { FormEvent, StrictMode, useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { PartnerOnboarding } from './PartnerOnboarding';
import { PartnerIcon } from './components/PartnerIcon';
import { useBrowserSession } from '@shuttlebook/ui/browser-session';
import '@shuttlebook/ui/tokens.css';
import '@shuttlebook/ui/primitives.css';
import './assets/partner.css';

type ContactType = 'email' | 'phone';
type View = 'register' | 'verify' | 'login' | 'session';
type ApiError = { code?: string };

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';

async function post(path: string, body: object): Promise<Response> {
  return fetch(`${apiBaseUrl}/api/v1${path}`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
  });
}

async function errorCode(response: Response): Promise<string | undefined> {
  try { return (await response.json() as ApiError).code; } catch { return undefined; }
}

function retryMessage(response: Response): string {
  const retryAfter = Number(response.headers.get('Retry-After'));
  return Number.isFinite(retryAfter) && retryAfter > 0
    ? `Bạn đã thử quá nhiều lần. Vui lòng đợi khoảng ${Math.max(1, Math.ceil(retryAfter / 60))} phút rồi thử lại.`
    : 'Bạn đã thử quá nhiều lần. Vui lòng thử lại sau.';
}

function PartnerIdentity() {
  const [view, setView] = useState<View>('register');
  const [contactType, setContactType] = useState<ContactType>('email');
  const [contact, setContact] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [code, setCode] = useState('');
  const auth = useBrowserSession('partner', apiBaseUrl);
  const { session } = auth;
  const [message, setMessage] = useState('');
  const [submitting, setSubmitting] = useState(false);
  useEffect(() => {
    if (session && view !== 'session') setView('session');
    else if (!session && view === 'session') { setView('login'); setMessage(''); }
  }, [session, view]);

  const register = async (event: FormEvent) => {
    event.preventDefault();
    if (password !== confirmPassword) { setMessage('Mật khẩu nhập lại chưa khớp.'); return; }
    setSubmitting(true); setMessage('');
    try {
      const response = await post('/partner-auth/register', { contactType, contact, password });
      if (response.status === 202) {
        setPassword(''); setConfirmPassword(''); setView('verify');
        setMessage('Nếu thông tin hợp lệ, mã xác minh đã được gửi.');
      } else {
        const error = await errorCode(response);
        setMessage(error === 'RATE_LIMITED' ? retryMessage(response)
          : error === 'IDENTITY_DELIVERY_UNAVAILABLE' ? 'Dịch vụ gửi mã đang bận. Vui lòng thử lại sau.'
            : 'Thông tin chưa hợp lệ. Kiểm tra liên hệ và mật khẩu.');
      }
    } catch { setMessage('Không thể kết nối máy chủ. Vui lòng thử lại.'); }
    finally { setSubmitting(false); }
  };

  const verify = async (event: FormEvent) => {
    event.preventDefault(); setSubmitting(true); setMessage('');
    try {
      const response = await post('/partner-auth/verify', { contactType, contact, code });
      if (response.ok) {
        setCode(''); setView('login');
        setMessage('Đã xác minh liên hệ. Hãy đăng nhập để xem trạng thái tài khoản chủ sân.');
      } else {
        const error = await errorCode(response);
        setMessage(error === 'RATE_LIMITED' ? retryMessage(response)
          : 'Mã xác minh không hợp lệ hoặc đã hết hạn.');
      }
    } catch { setMessage('Không thể kết nối máy chủ. Vui lòng thử lại.'); }
    finally { setSubmitting(false); }
  };

  const resend = async () => {
    setSubmitting(true); setMessage('');
    try {
      const response = await post('/partner-auth/verification-resend', { contactType, contact });
      const error = response.status === 202 ? undefined : await errorCode(response);
      setMessage(response.status === 202 ? 'Nếu liên hệ đang chờ xác minh, mã mới đã được gửi. Hãy kiểm tra hộp thư hoặc tin nhắn.'
        : error === 'RATE_LIMITED' ? retryMessage(response)
          : error === 'IDENTITY_DELIVERY_UNAVAILABLE' ? 'Dịch vụ gửi mã đang bận. Vui lòng thử lại sau.'
            : 'Chưa thể gửi lại mã. Vui lòng thử lại sau.');
    } catch { setMessage('Không thể kết nối máy chủ. Vui lòng thử lại.'); }
    finally { setSubmitting(false); }
  };

  const login = async (event: FormEvent) => {
    event.preventDefault(); setSubmitting(true); setMessage('');
    try {
      const response = await auth.login({ contactType, contact, password });
      if (!response.ok) {
        const error = await errorCode(response);
        setMessage(error === 'RATE_LIMITED' ? retryMessage(response)
          : 'Đăng nhập không thành công. Kiểm tra thông tin hoặc xác minh tài khoản.');
        return;
      }
      const next = await auth.completeLogin(response);
      if (!next) {
        setMessage('Tài khoản này không thuộc cổng chủ sân.');
        return;
      }
      setPassword(''); setView('session');
      setMessage('Đăng nhập thành công.');
    } catch (error) { setMessage(error instanceof Error && error.message === 'SESSION_CHANGED'
      ? 'Phiên đăng nhập đã thay đổi ở cửa sổ khác. Hãy thử lại.' : 'Không thể kết nối máy chủ. Vui lòng thử lại.'); }
    finally { setSubmitting(false); }
  };

  const logout = async () => {
    setSubmitting(true); setMessage('');
    try {
      await auth.logout(); setMessage('Đã đăng xuất.');
    } catch { setMessage('Phiên đã đóng trên trình duyệt. Không thể kết nối máy chủ để đăng xuất.'); }
    finally { setView('login'); setSubmitting(false); }
  };

  if (auth.restoring) return <main className="auth-page" role="status">Đang khôi phục phiên đăng nhập…</main>;
  if (auth.restoreError) return <main className="auth-page"><p role="alert">{auth.restoreError}</p>
    <button type="button" onClick={() => void auth.retryRestore()}>Thử lại kết nối</button></main>;
  if (session) return <PartnerOnboarding sendRequest={auth.request}
    onLogout={() => void logout()} logoutBusy={submitting} />;

  return <main className="auth-page">
    <section className="auth-story">
      <a className="brand" href="/"><span className="brand-mark"><PartnerIcon name="courts" /></span>ShuttleBook</a>
      <p className="eyebrow">DÀNH CHO CHỦ SÂN</p>
      <h1>Chào chủ sân.</h1>
      <p>Một không gian để quản lý cơ sở, thiết lập lịch chơi và chuẩn bị đón khách.</p>
      <ul className="auth-highlights">
        <li><PartnerIcon name="venues" />Quản lý cơ sở và từng sân</li>
        <li><PartnerIcon name="schedule" />Lịch hoạt động và bảng giá linh hoạt</li>
        <li><PartnerIcon name="payments" />QR nhận tiền do bạn cung cấp</li>
      </ul>
    </section>
    <section className="auth-panel"><div>
    <nav aria-label="Tài khoản chủ sân">
      {view !== 'register' && view !== 'session' && <button type="button" onClick={() => { setView('register'); setMessage(''); }}>Đăng ký</button>}{' '}
      {view !== 'login' && view !== 'session' && <button type="button" onClick={() => { setView('login'); setMessage(''); }}>Đăng nhập</button>}
    </nav>

    {view === 'register' && <form onSubmit={register}>
      <h2>Tạo tài khoản chủ sân</h2>
      <label>Phương thức liên hệ<br /><select value={contactType} onChange={event => setContactType(event.target.value as ContactType)}>
        <option value="email">Email</option><option value="phone">Số điện thoại</option>
      </select></label><br /><br />
      <label>{contactType === 'email' ? 'Email' : 'Số điện thoại E.164'}<br />
        <input required type={contactType === 'email' ? 'email' : 'tel'} value={contact}
          onChange={event => setContact(event.target.value)} placeholder={contactType === 'email' ? 'ban@example.com' : '+84901234567'} />
      </label><br /><br />
      <label>Mật khẩu<br /><input required type="password" minLength={12} value={password}
        onChange={event => setPassword(event.target.value)} /></label><br /><br />
      <label>Nhập lại mật khẩu<br /><input required type="password" value={confirmPassword}
        onChange={event => setConfirmPassword(event.target.value)} /></label>
      <p><small>Tối thiểu 12 ký tự, gồm ít nhất 3 nhóm: chữ thường, chữ hoa, số, ký tự đặc biệt.</small></p>
      <button disabled={submitting} type="submit">{submitting ? 'Đang gửi…' : 'Đăng ký'}</button>
    </form>}

    {view === 'verify' && <form onSubmit={verify}>
      <h2>Xác minh liên hệ</h2>
      <p>Nếu tài khoản đủ điều kiện, mã được gửi đến {contactType === 'email' ? 'email' : 'số điện thoại'} đã cung cấp.</p>
      {import.meta.env.DEV && <p><small>Môi trường local: xem mã tại <a href="http://localhost:8025" target="_blank" rel="noreferrer">Mailpit</a>; mã không được gửi đến hộp thư hoặc số điện thoại thật.</small></p>}
      <label>Mã xác minh 6 chữ số<br /><input required inputMode="numeric" pattern="[0-9]{6}" maxLength={6}
        value={code} onChange={event => setCode(event.target.value.replace(/\D/g, ''))} /></label><br /><br />
      <button disabled={submitting} type="submit">{submitting ? 'Đang kiểm tra…' : 'Xác minh'}</button>{' '}
      <button disabled={submitting} type="button" onClick={resend}>Gửi lại mã</button>
    </form>}

    {view === 'login' && <form onSubmit={login}>
      <h2>Đăng nhập chủ sân</h2>
      <label>Phương thức liên hệ<br /><select value={contactType} onChange={event => setContactType(event.target.value as ContactType)}>
        <option value="email">Email</option><option value="phone">Số điện thoại</option>
      </select></label><br /><br />
      <label>{contactType === 'email' ? 'Email' : 'Số điện thoại E.164'}<br />
        <input required type={contactType === 'email' ? 'email' : 'tel'} value={contact}
          onChange={event => setContact(event.target.value)} /></label><br /><br />
      <label>Mật khẩu<br /><input required type="password" value={password}
        onChange={event => setPassword(event.target.value)} /></label><br /><br />
      <button disabled={submitting} type="submit">{submitting ? 'Đang đăng nhập…' : 'Đăng nhập'}</button>
    </form>}

    {(message || auth.message) && <p className="feedback" role="status">{message || auth.message}</p>}
    </div></section>
  </main>;
}

createRoot(document.getElementById('root')!).render(<StrictMode><PartnerIdentity /></StrictMode>);
