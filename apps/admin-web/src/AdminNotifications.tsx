import { useEffect, useRef, useState } from 'react';

type Notice = { id: string; title: string; body: string; createdAt: string; readAt: string | null;
  bookingId?: string | null; action?: string | null };
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';

export function AdminNotifications({ accessToken }: { accessToken: string }) {
  const [items, setItems] = useState<Notice[]>([]);
  const [unread, setUnread] = useState(0);
  const [next, setNext] = useState<string | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const mounted = useRef(true);

  useEffect(() => {
    mounted.current = true;
    void load();
    const focus = () => { if (document.visibilityState === 'visible') void load(); };
    window.addEventListener('focus', focus);
    return () => { mounted.current = false; controller.current?.abort(); window.removeEventListener('focus', focus); };
  }, [accessToken]);

  async function load(cursor?: string) {
    controller.current?.abort();
    const active = new AbortController(); controller.current = active;
    setBusy(true);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/me/notifications${cursor ? `?before=${encodeURIComponent(cursor)}` : ''}`,
        { headers: { Authorization: `Bearer ${accessToken}` }, signal: active.signal });
      if (!response.ok) throw new Error('Không thể tải thông báo. Vui lòng thử lại.');
      const payload = await response.json() as { data: Notice[]; unreadCount?: number; nextCursor?: string | null };
      if (!mounted.current || active.signal.aborted) return;
      setItems(previous => cursor ? [...previous, ...payload.data.filter(item => !previous.some(old => old.id === item.id))] : payload.data);
      setUnread(payload.unreadCount ?? payload.data.filter(item => !item.readAt).length);
      setNext(payload.nextCursor ?? null); setError('');
    } catch (reason) {
      if (!active.signal.aborted && mounted.current) setError(reason instanceof Error ? reason.message : 'Không thể tải thông báo.');
    } finally { if (!active.signal.aborted && mounted.current) setBusy(false); }
  }

  async function markRead(id: string) {
    const current = controller.current;
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/me/notifications/${encodeURIComponent(id)}/read`,
        { method: 'POST', headers: { Authorization: `Bearer ${accessToken}` } });
      if (!response.ok) throw new Error('Chưa đánh dấu được thông báo. Vui lòng thử lại.');
      if (mounted.current && controller.current === current) await load();
    } catch (reason) { if (mounted.current) setError(reason instanceof Error ? reason.message : 'Không thể cập nhật thông báo.'); }
  }

  return <section className="admin-notifications" aria-labelledby="admin-notices-title">
    <header><h2 id="admin-notices-title">Thông báo <span>{unread} chưa đọc</span></h2>
      <button type="button" onClick={() => void load()} disabled={busy}>Làm mới thông báo</button></header>
    <p>Cảnh báo đối chiếu chỉ nhắc vận hành; khung giờ vẫn được giữ và chưa xác nhận thanh toán.</p>
    {error && <p role="alert">{error}</p>}
    {busy && !items.length ? <p role="status">Đang tải thông báo…</p> : !items.length ? <p>Chưa có thông báo.</p> :
      <ul>{items.map(item => <li key={item.id} className={item.readAt ? '' : 'unread'}>
        <strong>{item.title}</strong><p>{item.body}</p>
        <time dateTime={item.createdAt}>{new Date(item.createdAt).toLocaleString('vi-VN')}</time>
        {item.action === 'ADMIN_PAYMENT_ALERT' && <small>Liên hệ chủ sân để xử lý đối chiếu. Cổng Admin không xác nhận tiền thay chủ sân.</small>}
        {!item.readAt && <button type="button" onClick={() => void markRead(item.id)}>Đánh dấu đã đọc</button>}
      </li>)}</ul>}
    {next && <button type="button" disabled={busy} onClick={() => void load(next)}>Xem thêm thông báo</button>}
  </section>;
}
