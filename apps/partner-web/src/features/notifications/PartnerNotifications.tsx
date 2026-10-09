import { useCallback, useEffect, useRef, useState } from 'react';
import { PartnerApiError, type PartnerRequest } from '../bookings/types';

type Notice = { id: string; title: string; body: string; readAt?: string | null; bookingId?: string | null; action?: string | null };
type NoticesEnvelope = { data: Notice[]; unreadCount?: number; nextCursor?: string | null };
export function usePartnerNotifications(request: PartnerRequest) {
  const latest = useRef(request); latest.current = request;
  const [notices, setNotices] = useState<Notice[]>([]);
  const [unread, setUnread] = useState(0);
  const [cursor, setCursor] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [readingCount, setReadingCount] = useState(0);
  const [error, setError] = useState('');
  const current = useRef<AbortController | null>(null);
  const mounted = useRef(false);
  const generation = useRef(0);
  const loadedPages = useRef(1);
  const reading = useRef(new Map<string, AbortController>());
  const readDirty = useRef(false);
  function denied(failure: unknown) {
    if (!(failure instanceof PartnerApiError) || ![403, 404].includes(failure.status)) return;
    generation.current++; current.current?.abort(); current.current = null;
    for (const controller of reading.current.values()) controller.abort();
    reading.current.clear(); readDirty.current = false; loadedPages.current = 1;
    setNotices([]); setUnread(0); setCursor(null); setLoading(false); setReadingCount(0);
  }
  const refresh = useCallback(async (before?: string) => {
    if (current.current || reading.current.size) return;
    const controller = new AbortController(); current.current = controller;
    const epoch = generation.current;
    setLoading(true); setError('');
    try {
      let next = before; let first: NoticesEnvelope | null = null; let last: NoticesEnvelope | null = null;
      const items = new Map<string, Notice>(); const cursors = new Set<string>();
      for (let page = 0; page < (before ? 1 : loadedPages.current); page++) {
        const result = await latest.current<NoticesEnvelope>(
          '/me/notifications/' + (next ? '?before=' + encodeURIComponent(next) : ''),
          'GET', undefined, undefined, { signal: controller.signal, envelope: true });
        if (!mounted.current || controller.signal.aborted || epoch !== generation.current) return;
        first ??= result; last = result;
        for (const notice of result.data) items.set(notice.id, notice);
        if (!result.nextCursor || cursors.has(result.nextCursor)) break;
        cursors.add(result.nextCursor); next = result.nextCursor;
      }
      if (!first || !last || !mounted.current || controller.signal.aborted || epoch !== generation.current) return;
      if (before) loadedPages.current++;
      setNotices(previous => {
        if (!before) return [...items.values()];
        const combined = new Map(previous.map(notice => [notice.id, notice]));
        for (const notice of items.values()) combined.set(notice.id, notice);
        return [...combined.values()];
      });
      setUnread(first.unreadCount ?? [...items.values()].filter(item => !item.readAt).length);
      setCursor(last.nextCursor ?? null);
    } catch (failure) { if (mounted.current && !controller.signal.aborted && epoch === generation.current) {
      denied(failure); setError('Không tải được thông báo. Vui lòng thử lại.');
    } }
    finally { if (current.current === controller) { current.current = null; if (mounted.current) setLoading(false); } }
  }, []);
  useEffect(() => {
    mounted.current = true; void refresh();
    const update = () => { if (document.visibilityState === 'visible') void refresh(); };
    const timer = window.setInterval(update, 5000);
    window.addEventListener('focus', update); document.addEventListener('visibilitychange', update);
    return () => { mounted.current = false; generation.current++; current.current?.abort(); current.current = null;
      for (const controller of reading.current.values()) controller.abort(); reading.current.clear(); readDirty.current = false;
      window.clearInterval(timer); window.removeEventListener('focus', update); document.removeEventListener('visibilitychange', update); };
  }, [refresh]);
  async function read(id: string) {
    if (reading.current.has(id)) return;
    current.current?.abort(); current.current = null; setLoading(false);
    const controller = new AbortController(); reading.current.set(id, controller);
    const epoch = generation.current; setReadingCount(reading.current.size); setError('');
    try {
      await latest.current('/me/notifications/' + encodeURIComponent(id) + '/read', 'POST', undefined, undefined, { signal: controller.signal });
      if (!mounted.current || controller.signal.aborted || generation.current !== epoch) return;
      readDirty.current = true;
    } catch (failure) { if (mounted.current && !controller.signal.aborted && generation.current === epoch) {
      denied(failure); setError('Không đánh dấu được thông báo. Vui lòng thử lại.');
    } }
    finally {
      if (reading.current.get(id) === controller) reading.current.delete(id);
      if (mounted.current) setReadingCount(reading.current.size);
    }
    // The last completed mutation reloads all loaded pages, including server unread/read state.
    if (readDirty.current && mounted.current && generation.current === epoch && !reading.current.size) {
      readDirty.current = false; await refresh();
    }
  }
  return { notices, unread, cursor, loading: loading || readingCount > 0, error, refresh, read };
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
