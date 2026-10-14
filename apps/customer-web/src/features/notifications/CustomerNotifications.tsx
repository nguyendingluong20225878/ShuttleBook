import { createContext, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { noticeText } from '@shuttlebook/ui/notice-text';
import { useCustomerSession } from '../auth/CustomerSession';
import { useVisiblePolling } from '../../hooks/useVisiblePolling';
import { data, failure } from '../bookings/bookingApi';
import { navigate } from '../../routes/navigation';
import { CustomerShell } from '../../components/CustomerShell';

type Notification = { id: string; title: string; body: string; readAt: string | null; createdAt?: string;
  bookingId?: string | null; action?: string | null };
type Page = { data: Notification[]; unreadCount: number; nextCursor: string | null };
type Notifications = { items: Notification[]; unreadCount: number; nextCursor: string | null; error: string; loading: boolean;
  refresh: () => void; more: () => Promise<void>; markRead: (id: string) => Promise<string | null> };
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
  const reading = useRef(new Map<string, AbortController>()); const mounted = useRef(true);
  const pages = useRef(1); const mutation = useRef(0); const paging = useRef<AbortController | null>(null);
  const currentSession = useRef(session); currentSession.current = session;
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; paging.current?.abort(); for (const controller of reading.current.values()) controller.abort(); }; }, []);
  useEffect(() => {
    mutation.current++; pages.current = 1; paging.current?.abort();
    for (const controller of reading.current.values()) controller.abort(); reading.current.clear();
    setItems([]); setUnreadCount(0); setNextCursor(null); setError(''); setLoading(true);
  }, [session?.accessToken]);
  function deny(reason: unknown) {
    if (reason instanceof Error && ['UNAUTHORIZED', 'FORBIDDEN', 'NOT_FOUND'].includes(reason.message)) {
      mutation.current++; pages.current = 1; paging.current?.abort();
      setItems([]); setUnreadCount(0); setNextCursor(null);
    }
  }
  const refresh = useVisiblePolling(Boolean(session), `customer-notifications:${session?.accessToken ?? ''}`,
    async signal => {
      const actor = currentSession.current; const epoch = mutation.current;
      let before: string | null = null; let count = 0; const found = new Map<string, Notification>(); const cursors = new Set<string>();
      for (let index = 0; index < pages.current; index++) {
        const result: Page = await request('/api/v1/me/notifications' + (before ? `?before=${encodeURIComponent(before)}` : ''), { signal }).then(readPage);
        if (index === 0) count = result.unreadCount;
        for (const item of result.data) found.set(item.id, item);
        before = result.nextCursor;
        if (!before || cursors.has(before)) break;
        cursors.add(before);
      }
      return { data: [...found.values()], unreadCount: count, nextCursor: before, actor, epoch };
    },
    result => { if (result.actor !== currentSession.current || result.epoch !== mutation.current || reading.current.size || paging.current) return;
      setItems(result.data); setUnreadCount(result.unreadCount); setNextCursor(result.nextCursor); setLoading(false); setError(''); },
    reason => { deny(reason); setError(failure(reason)); setLoading(false); });
  async function more() {
    if (!nextCursor || paging.current || reading.current.size) return;
    const active = new AbortController(); paging.current = active; const actor = currentSession.current; const epoch = ++mutation.current;
    setLoading(true); setError('');
    try {
      const result = await request(`/api/v1/me/notifications?before=${encodeURIComponent(nextCursor)}`, { signal: active.signal }).then(readPage);
      if (!mounted.current || active.signal.aborted || actor !== currentSession.current || epoch !== mutation.current) return;
      pages.current++;
      setItems(previous => [...new Map([...previous, ...result.data].map(item => [item.id, item])).values()]);
      setUnreadCount(result.unreadCount); setNextCursor(result.nextCursor);
    } catch (reason) { if (!active.signal.aborted && mounted.current && actor === currentSession.current && epoch === mutation.current) { deny(reason); setError(failure(reason)); } }
    finally { if (paging.current === active) paging.current = null; if (mounted.current && actor === currentSession.current) setLoading(false); }
  }
  async function markRead(id: string) {
    if (reading.current.has(id)) return null;
    const already = items.find(item => item.id === id); if (already?.readAt) return already.readAt;
    const controller = new AbortController(); reading.current.set(id, controller); mutation.current++; paging.current?.abort();
    const actorSession = currentSession.current;
    try {
      const result = await request(`/api/v1/me/notifications/${id}/read`, { method: 'POST', signal: controller.signal }).then(data<{ id: string; readAt: string; unreadCount?: number }>);
      if (!mounted.current || controller.signal.aborted || currentSession.current !== actorSession) return null;
      setItems(previous => previous.map(item => item.id === id ? { ...item, readAt: result.readAt } : item));
      if (typeof result.unreadCount === 'number') setUnreadCount(result.unreadCount);
      setError(''); refresh(); return result.readAt;
    } catch (reason) { if (mounted.current && !controller.signal.aborted && currentSession.current === actorSession) { deny(reason); setError(failure(reason)); } return null; }
    finally { if (reading.current.get(id) === controller) reading.current.delete(id); }
  }
  return <Context.Provider value={{ items, unreadCount, nextCursor, error, loading, refresh, more, markRead }}>{children}</Context.Provider>;
}
function useNotifications() { const value = useContext(Context); if (!value) throw new Error('Missing CustomerNotificationsProvider'); return value; }
export function CustomerNotificationsLink() {
  const { unreadCount } = useNotifications();
  return <a href="/me/notifications">Thông báo{unreadCount > 0 && <span className="notification-badge" aria-label={`${unreadCount} thông báo chưa đọc`}>{unreadCount > 99 ? '99+' : unreadCount}</span>}</a>;
}
export function CustomerNotificationsPage() {
  const { session } = useCustomerSession(); const notifications = useNotifications();
  const [selected, setSelected] = useState<string | null>(null); const [opening, setOpening] = useState<string | null>(null);
  const mounted = useRef(true);
  useEffect(() => { if (!session) navigate(`/login?returnTo=${encodeURIComponent('/me/notifications')}`, true); }, [Boolean(session)]);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  async function open(item: Notification) {
    if (opening) return;
    setSelected(item.id); setOpening(item.id);
    await notifications.markRead(item.id);
    if (mounted.current) setOpening(null);
  }
  const items = notifications.items;
  return <CustomerShell className="customer-inbox">
    <section className="booking-panel notification-panel"><div className="notification-page-heading"><div><p className="eyebrow">Cập nhật đặt sân</p><h1>Thông báo</h1><p>{notifications.unreadCount} thông báo chưa đọc</p></div>
      <button type="button" onClick={notifications.refresh}>Làm mới thông báo</button></div>
      {notifications.error && <p className="inline-feedback" role="alert">{notifications.error}</p>}
      {notifications.loading && <p className="loading-label" role="status">Đang tải thông báo…</p>}
      {!notifications.loading && !items.length && !notifications.error && <div className="empty-state"><h2>Bạn đã theo dõi mọi cập nhật</h2><p>Chưa có thông báo.</p><a className="secondary-link" href="/venues">Tìm sân &amp; xem lịch</a></div>}
      <ul className="customer-notifications">{items.map(item => <li key={item.id} className={item.readAt ? '' : 'is-unread'}>
        <button type="button" className="customer-notice-open" aria-label={noticeText(item.title)} aria-expanded={selected === item.id} disabled={!!opening}
          onClick={() => void open(item)}><span className="notification-item-heading"><strong>{noticeText(item.title)}</strong><span className={`notification-read-state ${item.readAt ? 'is-read' : ''}`}>{item.readAt ? 'Đã đọc' : 'Chưa đọc'}</span></span>
          <span className="customer-notice-body">{noticeText(item.body)}</span>{item.createdAt && <time dateTime={item.createdAt}>{new Date(item.createdAt).toLocaleString('vi-VN')}</time>}</button>
        {selected === item.id && <div className="notification-item-actions" role="region" aria-label={`Chi tiết: ${noticeText(item.title)}`}>
          <p>{noticeText(item.body)}</p>{opening === item.id && <span role="status">Đang mở thông báo…</span>}
          {bookingLink(item) && <a href={bookingLink(item)!}>Xem đơn đặt sân</a>}
        </div>}
      </li>)}</ul>
      {notifications.nextCursor && <button type="button" disabled={notifications.loading || !!opening} onClick={() => { void notifications.more(); }}>Xem thêm thông báo</button>}
    </section></CustomerShell>;
}
