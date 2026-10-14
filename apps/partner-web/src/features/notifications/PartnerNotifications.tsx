import { useCallback, useEffect, useRef, useState } from 'react';
import { noticeText } from '@shuttlebook/ui/notice-text';
import { PartnerApiError, type PartnerRequest } from '../bookings/types';
import { usePartnerToast } from '../../components/PartnerToast';

type Notice = { id: string; title: string; body: string; readAt?: string | null; bookingId?: string | null; action?: string | null; profileBusinessId?: string | null };
type NoticesEnvelope = { data: Notice[]; unreadCount?: number; nextCursor?: string | null };
export function usePartnerNotifications(request: PartnerRequest) {
  const { showToast } = usePartnerToast();
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
    if (!(failure instanceof PartnerApiError) || ![401, 403, 404].includes(failure.status)) return;
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
    if (reading.current.has(id)) return false;
    current.current?.abort(); current.current = null; setLoading(false);
    const controller = new AbortController(); reading.current.set(id, controller);
    const epoch = generation.current; setReadingCount(reading.current.size); setError('');
    try {
      const result = await latest.current<{ id: string; readAt?: string; unreadCount?: number }>('/me/notifications/' + encodeURIComponent(id) + '/read', 'POST', undefined, undefined, { signal: controller.signal });
      if (!mounted.current || controller.signal.aborted || generation.current !== epoch) return false;
      if (result.readAt) setNotices(previous => previous.map(notice => notice.id === id ? { ...notice, readAt: result.readAt } : notice));
      if (typeof result.unreadCount === 'number') setUnread(result.unreadCount);
      readDirty.current = true;
    } catch (failure) { if (mounted.current && !controller.signal.aborted && generation.current === epoch) {
      denied(failure); showToast('Không mở được thông báo. Vui lòng thử lại.', 'error');
    } return false; }
    finally {
      if (reading.current.get(id) === controller) reading.current.delete(id);
      if (mounted.current) setReadingCount(reading.current.size);
    }
    // The last completed mutation reloads all loaded pages, including server unread/read state.
    if (readDirty.current && mounted.current && generation.current === epoch && !reading.current.size) {
      readDirty.current = false; await refresh();
    }
    return mounted.current && generation.current === epoch;
  }
  return { notices, unread, cursor, loading: loading || readingCount > 0, error, refresh, read };
}
export function PartnerNotifications({ model, openBooking, openProfile }: { model: ReturnType<typeof usePartnerNotifications>;
  openBooking: (id: string) => void; openProfile?: (businessId?: string) => void }) {
  const [reading, setReading] = useState<string | null>(null);
  const [selected, setSelected] = useState<string | null>(null);
  const mounted = useRef(true);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  async function open(notice: Notice) {
    if (reading || model.loading) return;
    setSelected(notice.id); setReading(notice.id);
    const success = notice.readAt ? true : await model.read(notice.id);
    if (!mounted.current) return;
    setReading(null);
    if (!success) return;
    if (notice.action === 'OPERATOR_BOOKING' && notice.bookingId && /^[0-9a-f-]{36}$/i.test(notice.bookingId)) openBooking(notice.bookingId);
    if (notice.action === 'PARTNER_PROFILE' && openProfile) openProfile(notice.profileBusinessId ?? undefined);
  }
  return <section aria-label="Thông báo của bạn">
    <button type="button" disabled={model.loading} onClick={() => void model.refresh()}>Tải lại thông báo</button>
    {model.loading && <p role="status">Đang tải thông báo…</p>}
    {model.error && <p className="feedback error" role="alert">{model.error}</p>}
    {!model.loading && !model.error && model.notices.length === 0 && <div className="panel empty-state"><h3>Chưa có thông báo</h3><p>Phản hồi hồ sơ và thông báo đơn đặt sân sẽ hiển thị tại đây.</p></div>}
    <ul className="notice-list">{model.notices.map(notice => <li className={notice.readAt ? '' : 'unread'} key={notice.id}>
      <button type="button" className="partner-notice-open" aria-label={noticeText(notice.title)} aria-expanded={selected === notice.id}
        disabled={!!reading || model.loading} onClick={() => void open(notice)}><strong>{noticeText(notice.title)}</strong><span>{noticeText(notice.body)}</span>
        <span className="status-badge">{notice.readAt ? 'Đã đọc' : 'Chưa đọc'}</span></button>
      {selected === notice.id && <div className="partner-notice-detail" role="region" aria-label={`Chi tiết: ${noticeText(notice.title)}`}>
        <p>{noticeText(notice.body)}</p>{reading === notice.id && <p role="status">Đang mở thông báo…</p>}
      </div>}
    </li>)}</ul>
    {model.cursor && <button type="button" disabled={model.loading} onClick={() => void model.refresh(model.cursor!)}>Thông báo trước đó</button>}
  </section>;
}
