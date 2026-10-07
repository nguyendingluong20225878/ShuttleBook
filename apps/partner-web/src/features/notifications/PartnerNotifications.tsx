import { useCallback, useEffect, useRef, useState } from 'react';
import type { PartnerRequest } from '../bookings/types';

type Notice = { id: string; title: string; body: string; readAt?: string | null; bookingId?: string | null; action?: string | null };
type NoticesEnvelope = { data: Notice[]; unreadCount?: number; nextCursor?: string | null };
export function usePartnerNotifications(request: PartnerRequest) {
  const latest = useRef(request); latest.current = request;
  const [notices, setNotices] = useState<Notice[]>([]);
  const [unread, setUnread] = useState(0);
  const [cursor, setCursor] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const current = useRef<AbortController | null>(null);
  const mounted = useRef(false);
  const refresh = useCallback(async (before?: string) => {
    if (current.current) return;
    const controller = new AbortController(); current.current = controller;
    setLoading(true); setError('');
    try {
      const result = await latest.current<NoticesEnvelope>(`/me/notifications/${before ? `?before=${encodeURIComponent(before)}` : ''}`,
        'GET', undefined, undefined, { signal: controller.signal, envelope: true });
      if (!mounted.current || controller.signal.aborted) return;
      setNotices(previous => before ? [...previous, ...result.data.filter(item => !previous.some(old => old.id === item.id))] : result.data);
      setUnread(result.unreadCount ?? result.data.filter(item => !item.readAt).length);
      setCursor(result.nextCursor ?? null);
    } catch { if (mounted.current && !controller.signal.aborted) setError('Không tải được thông báo. Vui lòng thử lại.'); }
    finally { if (current.current === controller) { current.current = null; if (mounted.current) setLoading(false); } }
  }, []);
  useEffect(() => {
    mounted.current = true; void refresh();
    const update = () => { if (document.visibilityState === 'visible') void refresh(); };
    const timer = window.setInterval(update, 5000);
    window.addEventListener('focus', update); document.addEventListener('visibilitychange', update);
    return () => { mounted.current = false; current.current?.abort(); current.current = null;
      window.clearInterval(timer); window.removeEventListener('focus', update); document.removeEventListener('visibilitychange', update); };
  }, [refresh]);
  async function read(id: string) {
    try {
      await latest.current(`/me/notifications/${id}/read`, 'POST');
      if (!mounted.current) return;
      setNotices(previous => previous.map(item => item.id === id ? { ...item, readAt: new Date().toISOString() } : item));
      current.current?.abort(); current.current = null; await refresh();
    } catch { if (mounted.current) setError('Không đánh dấu được thông báo. Vui lòng thử lại.'); }
  }
  return { notices, unread, cursor, loading, error, refresh, read };
}
export function PartnerNotifications({ model, openBooking }: { model: ReturnType<typeof usePartnerNotifications>;
  openBooking: (id: string) => void }) {
  const [reading, setReading] = useState<string | null>(null);
  return <section aria-label="Thông báo của bạn">
    <button type="button" disabled={model.loading} onClick={() => void model.refresh()}>Tải lại thông báo</button>
    {model.loading && <p role="status">Đang tải thông báo…</p>}
    {model.error && <p className="feedback error" role="alert">{model.error}</p>}
    {!model.loading && !model.error && model.notices.length === 0 && <div className="panel empty-state"><h3>Chưa có thông báo</h3><p>Phản hồi hồ sơ và thông báo đơn đặt sân sẽ hiển thị tại đây.</p></div>}
    <ul className="notice-list">{model.notices.map(notice => <li className={notice.readAt ? '' : 'unread'} key={notice.id}>
      <strong>{notice.title}</strong><p>{notice.body}</p><div className="booking-actions">
      {notice.action === 'OPERATOR_BOOKING' && notice.bookingId && /^[0-9a-f-]{36}$/i.test(notice.bookingId) &&
        <button type="button" onClick={() => { openBooking(notice.bookingId!); if (!notice.readAt) void model.read(notice.id); }}>Xem đơn đặt sân</button>}
      {!notice.readAt ? <button type="button" disabled={reading === notice.id} onClick={async () => {
        setReading(notice.id); await model.read(notice.id); setReading(null);
      }}>Đã đọc</button> : <span className="status-badge">Đã đọc</span>}</div>
    </li>)}</ul>
    {model.cursor && <button type="button" disabled={model.loading} onClick={() => void model.refresh(model.cursor!)}>Thông báo trước đó</button>}
  </section>;
}
