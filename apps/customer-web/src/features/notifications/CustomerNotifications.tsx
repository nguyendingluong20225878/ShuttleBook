import { createContext, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { useCustomerSession } from '../auth/CustomerSession';
import { useVisiblePolling } from '../../hooks/useVisiblePolling';
import { data, failure } from '../bookings/bookingApi';
import { navigate } from '../../routes/navigation';
import { CustomerShell } from '../../components/CustomerShell';

type Notification = { id: string; title: string; body: string; readAt: string | null; createdAt?: string;
  bookingId?: string | null; action?: string | null };
type Page = { data: Notification[]; unreadCount: number; nextCursor: string | null };
type Notifications = { items: Notification[]; unreadCount: number; nextCursor: string | null; error: string; loading: boolean;
  refresh: () => void; markRead: (id: string) => Promise<string | null> };
const Context = createContext<Notifications | null>(null);
async function readPage(response: Response): Promise<Page> {
  const body = await response.json(); if (!response.ok) throw new Error(body.code ?? 'REQUEST_FAILED');
  return { data: body.data ?? [], unreadCount: body.unreadCount ?? 0, nextCursor: body.nextCursor ?? null };
}
function bookingLink(item: Notification) {
  return item.action === 'CUSTOMER_BOOKING' && item.bookingId && /^[a-f0-9-]{36}$/.test(item.bookingId)
    ? `/bookings/${item.bookingId}` : null;
}
export function CustomerNotificationsProvider({ children }: { children: ReactNode }) {
  const { session, request } = useCustomerSession(); const [items, setItems] = useState<Notification[]>([]);
  const [unreadCount, setUnreadCount] = useState(0); const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [error, setError] = useState(''); const [loading, setLoading] = useState(true);
  const reading = useRef(new Set<string>()); const mounted = useRef(true);
  const currentSession = useRef(session); currentSession.current = session;
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  useEffect(() => { if (!session) { setItems([]); setUnreadCount(0); setNextCursor(null); setError(''); setLoading(true); } }, [Boolean(session)]);
  const refresh = useVisiblePolling(Boolean(session), 'customer-notifications',
    signal => request('/api/v1/me/notifications', { signal }).then(readPage),
    result => { setItems(result.data); setUnreadCount(result.unreadCount); setNextCursor(result.nextCursor); setLoading(false); setError(''); },
    reason => { setError(failure(reason)); setLoading(false); });
  async function markRead(id: string) {
    if (reading.current.has(id)) return null; reading.current.add(id);
    const actorSession = currentSession.current;
    try {
      const result = await request(`/api/v1/me/notifications/${id}/read`, { method: 'POST' }).then(data<{ id: string; readAt: string }>);
      if (mounted.current && currentSession.current === actorSession) { setItems(previous => previous.map(item => item.id === id ? { ...item, readAt: result.readAt } : item)); refresh(); }
      return result.readAt;
    } catch (reason) { if (mounted.current && currentSession.current === actorSession) setError(failure(reason)); return null; }
    finally { reading.current.delete(id); }
  }
  return <Context.Provider value={{ items, unreadCount, nextCursor, error, loading, refresh, markRead }}>{children}</Context.Provider>;
}
function useNotifications() { const value = useContext(Context); if (!value) throw new Error('Missing CustomerNotificationsProvider'); return value; }
export function CustomerNotificationsLink() {
  const { unreadCount } = useNotifications();
  return <a href="/me/notifications">Thông báo{unreadCount > 0 && <span className="notification-badge" aria-label={`${unreadCount} thông báo chưa đọc`}>{unreadCount > 99 ? '99+' : unreadCount}</span>}</a>;
}
export function CustomerNotificationsPage() {
  const { session, request } = useCustomerSession(); const notifications = useNotifications();
  const [older, setOlder] = useState<Notification[]>([]); const [cursor, setCursor] = useState<string | null>(null);
  const [paging, setPaging] = useState(false); const [pageError, setPageError] = useState(''); const abort = useRef<AbortController | null>(null);
  useEffect(() => { if (!session) navigate(`/login?returnTo=${encodeURIComponent('/me/notifications')}`, true); }, [Boolean(session)]);
  useEffect(() => () => abort.current?.abort(), []);
  async function more() {
    const before = older.length ? cursor : notifications.nextCursor;
    if (!before || paging) return;
    const controller = new AbortController(); abort.current = controller; setPaging(true); setPageError('');
    try {
      const result = await request(`/api/v1/me/notifications?before=${encodeURIComponent(before)}`, { signal: controller.signal }).then(readPage);
      if (!controller.signal.aborted) { setOlder(previous => [...previous, ...result.data]); setCursor(result.nextCursor); }
    } catch (reason) { if (!controller.signal.aborted) setPageError(failure(reason)); }
    finally { if (!controller.signal.aborted) setPaging(false); }
  }
  async function read(id: string) { const readAt = await notifications.markRead(id); if (readAt) setOlder(previous => previous.map(item => item.id === id ? { ...item, readAt } : item)); }
  const items = [...notifications.items, ...older].filter((item, index, all) => all.findIndex(other => other.id === item.id) === index);
  return <CustomerShell className="customer-inbox">
    <section className="booking-panel notification-panel"><div className="notification-page-heading"><div><p className="eyebrow">Cập nhật đặt sân</p><h1>Thông báo</h1><p>{notifications.unreadCount} thông báo chưa đọc</p></div>
      <button type="button" onClick={notifications.refresh}>Làm mới thông báo</button></div>
      {(notifications.error || pageError) && <p className="inline-feedback" role="alert">{pageError || notifications.error}</p>}
      {notifications.loading && <p className="loading-label" role="status">Đang tải thông báo…</p>}
      {!notifications.loading && !items.length && !notifications.error && <div className="empty-state"><h2>Bạn đã theo dõi mọi cập nhật</h2><p>Chưa có thông báo.</p><a className="secondary-link" href="/venues">Tìm sân &amp; xem lịch</a></div>}
      <ul className="customer-notifications">{items.map(item => <li key={item.id} className={item.readAt ? '' : 'is-unread'}>
        <div className="notification-item-heading"><h2>{item.title}</h2><span className={`notification-read-state ${item.readAt ? 'is-read' : ''}`}>{item.readAt ? 'Đã đọc' : 'Chưa đọc'}</span></div>
        <p>{item.body}</p>{item.createdAt && <time dateTime={item.createdAt}>{new Date(item.createdAt).toLocaleString('vi-VN')}</time>}
        <div className="notification-item-actions">{bookingLink(item) && <a href={bookingLink(item)!} onClick={() => { void read(item.id); }}>Xem đơn đặt sân</a>}
          {!item.readAt && <button type="button" onClick={() => { void read(item.id); }}>Đánh dấu đã đọc</button>}</div>
      </li>)}</ul>
      {(older.length ? cursor : notifications.nextCursor) && <button type="button" disabled={paging} onClick={() => { void more(); }}>{paging ? 'Đang tải…' : 'Xem thêm thông báo'}</button>}
    </section></CustomerShell>;
}
