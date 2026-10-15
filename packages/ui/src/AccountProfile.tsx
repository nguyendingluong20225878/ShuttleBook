import { useEffect, useState, type FormEvent } from 'react';

type Profile = { id: string; displayName: string | null; contactType: 'email' | 'phone'; contact: string };
type Props = { request: (path: string, options?: RequestInit) => Promise<Response>; onSaved?: (name: string) => void;
  variant?: 'default' | 'customer' };

export function AccountProfile({ request, onSaved, variant = 'default' }: Props) {
  const [profile, setProfile] = useState<Profile | null>(null);
  const [name, setName] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [saved, setSaved] = useState(false);
  useEffect(() => {
    const controller = new AbortController();
    setProfile(null); setError('');
    void request('/api/v1/auth/profile', { signal: controller.signal }).then(async response => {
      if (!response.ok) throw new Error('Chưa tải được hồ sơ cá nhân.');
      return (await response.json() as { data: Profile }).data;
    }).then(value => { if (!controller.signal.aborted) { setProfile(value); setName(value.displayName ?? ''); } })
      .catch(reason => { if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : 'Chưa tải được hồ sơ.'); });
    return () => controller.abort();
  }, [request]);
  async function save(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError(''); setSaved(false);
    try {
      const response = await request('/api/v1/auth/profile', { method: 'PUT',
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ displayName: name.trim() }) });
      if (!response.ok) throw new Error(response.status === 400 ? 'Tên cần từ 2 đến 80 ký tự.' : 'Chưa lưu được hồ sơ cá nhân.');
      const value = (await response.json() as { data: Profile }).data;
      setProfile(value); setName(value.displayName ?? ''); setSaved(true); onSaved?.(value.displayName ?? '');
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Chưa lưu được hồ sơ.'); }
    finally { setBusy(false); }
  }
  return <section className={`panel account-profile ${variant === 'customer' ? 'account-profile-customer' : ''}`} aria-labelledby="account-profile-title">
    {variant === 'customer' ? <header className="account-profile-heading">
      <span className="account-profile-avatar" aria-hidden="true">{profile?.displayName?.trim().charAt(0).toLocaleUpperCase('vi-VN') || 'KH'}</span>
      <div><p className="account-profile-kicker">HỒ SƠ KHÁCH ĐẶT SÂN</p><h2 id="account-profile-title">Thông tin cá nhân</h2>
        <p>Quản lý tên hiển thị và xem liên hệ đăng nhập của bạn.</p></div>
    </header> : <h2 id="account-profile-title">Thông tin cá nhân</h2>}
    {!profile && !error && <p role="status">Đang tải thông tin cá nhân…</p>}
    {error && <p role="alert">{error}</p>}
    {profile && <form onSubmit={event => void save(event)}>
      <label>Tên hiển thị <input value={name} minLength={2} maxLength={80} required autoComplete="name"
        onChange={event => { setName(event.target.value); setSaved(false); }} /></label>
      <label>{profile.contactType === 'email' ? 'Email đăng nhập' : 'Số điện thoại đăng nhập'}
        <input value={profile.contact} readOnly aria-readonly="true" /></label>
      <p className="account-profile-help">Liên hệ đăng nhập đã được xác minh và hiện chỉ có thể xem.</p>
      <button type="submit" disabled={busy || name.trim().length < 2}>{busy ? 'Đang lưu…' : 'Lưu thông tin'}</button>
      {saved && <p className="account-profile-saved" role="status">Đã lưu thông tin cá nhân.</p>}
    </form>}
  </section>;
}
