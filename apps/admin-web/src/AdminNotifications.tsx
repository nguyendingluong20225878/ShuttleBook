import { useEffect, useRef, useState } from 'react';
import { AdminIcon } from './components/AdminIcon';
import type { AdminSummary } from './features/workspace/AdminWorkspace';

type Notice = { id: string; title: string; body: string; createdAt: string; readAt: string | null;
  bookingId?: string | null; action?: string | null };
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';

export function AdminNotifications({ accessToken, onSummary }: { accessToken: string; onSummary?: (summary: AdminSummary) => void }) {
  const [items, setItems] = useState<Notice[]>([]);
  const [unread, setUnread] = useState(0);
  const [next, setNext] = useState<string | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [loaded, setLoaded] = useState(false);
  const [reading, setReading] = useState<string | null>(null);
  const controller = useRef<AbortController | null>(null);
  const mounted = useRef(true);
  const generation = useRef(0);
  const loadedPages = useRef(1);
  const cachedItems = useRef<Notice[]>([]);
  const readController = useRef<AbortController | null>(null);

  useEffect(() => {
    mounted.current = true; generation.current++; setReading(null);
    void load();
    // Admin API requests update server activity: only explicit user refreshes may load again.
    return () => { mounted.current = false; generation.current++; controller.current?.abort(); readController.current?.abort(); };
  }, [accessToken]);
  useEffect(() => { onSummary?.({ count: loaded ? unread : null, error: !!error }); }, [loaded, unread, error, onSummary]);

  function denyAccess() {
    generation.current++; controller.current?.abort(); readController.current?.abort();
    cachedItems.current = []; loadedPages.current = 1;
    setItems([]); setUnread(0); setNext(null); setLoaded(false); setBusy(false); setReading(null);
    setError('Phiên hoặc quyền truy cập không còn hợp lệ. Vui lòng đăng nhập lại.');
  }

  async function load(cursor?: string) {
    controller.current?.abort();
    const active = new AbortController(); controller.current = active;
    const scope = generation.current;
    setBusy(true);
    try {
      const targetPages = cursor ? 1 : loadedPages.current;
      let currentCursor: string | null = cursor ?? null;
      const notices: Notice[] = []; let actualPages = 0; let unreadCount = 0;
      const seenCursors = new Set<string>();
      for (let page = 0; page < targetPages; page++) {
        const response = await fetch(`${apiBaseUrl}/api/v1/me/notifications${currentCursor ? `?before=${encodeURIComponent(currentCursor)}` : ''}`,
          { headers: { Authorization: `Bearer ${accessToken}` }, signal: active.signal });
        if (!mounted.current || active.signal.aborted || scope !== generation.current) return;
        if (!response.ok) {
          if (response.status === 401 || response.status === 403 || response.status === 404) { denyAccess(); return; }
          throw new Error('Không thể tải thông báo. Vui lòng thử lại.');
        }
        const payload = await response.json() as { data: Notice[]; unreadCount?: number; nextCursor?: string | null };
        if (!mounted.current || active.signal.aborted || scope !== generation.current) return;
        notices.push(...payload.data); actualPages++;
        if (page === 0) unreadCount = payload.unreadCount ?? payload.data.filter(item => !item.readAt).length;
        currentCursor = payload.nextCursor ?? null;
        if (!currentCursor) break;
        if (seenCursors.has(currentCursor)) throw new Error('Không thể tiếp tục danh sách thông báo. Vui lòng thử lại.');
        seenCursors.add(currentCursor);
      }
      const authoritative = cursor ? [...cachedItems.current, ...notices] : notices;
      const unique = [...new Map(authoritative.map(item => [item.id, item])).values()];
      cachedItems.current = unique; loadedPages.current = cursor ? loadedPages.current + actualPages : actualPages;
      setItems(unique); setUnread(unreadCount); setNext(currentCursor); setError(''); setLoaded(true);
    } catch (reason) {
      if (!active.signal.aborted && mounted.current && scope === generation.current) setError(reason instanceof Error ? reason.message : 'Không thể tải thông báo.');
    } finally { if (!active.signal.aborted && mounted.current && scope === generation.current) setBusy(false); }
  }

  async function markRead(id: string) {
    if (reading || busy) return;
    setReading(id); setError('');
    const scope = generation.current;
    const active = new AbortController(); readController.current = active;
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/me/notifications/${encodeURIComponent(id)}/read`,
        { method: 'POST', headers: { Authorization: `Bearer ${accessToken}` }, signal: active.signal });
      if (!mounted.current || active.signal.aborted || scope !== generation.current) return;
      if (!response.ok) {
        if (response.status === 401 || response.status === 403 || response.status === 404) { denyAccess(); return; }
        throw new Error('Chưa đánh dấu được thông báo. Vui lòng thử lại.');
      }
      await load();
    } catch (reason) { if (mounted.current && !active.signal.aborted && scope === generation.current) setError(reason instanceof Error ? reason.message : 'Không thể cập nhật thông báo.'); }
    finally { if (mounted.current && scope === generation.current) setReading(null); }
  }

  return <section className="admin-panel admin-notifications" aria-labelledby="admin-notices-title" aria-busy={busy}>
    <header className="admin-panel-heading"><h2 id="admin-notices-title">Thông báo <span>{loaded ? `${unread} chưa đọc` : 'Đang tải…'}</span></h2>
      <button type="button" className="admin-button-secondary" onClick={() => void load()} disabled={busy || !!reading}><AdminIcon name="refresh" />Làm mới thông báo</button></header>
    <p className="admin-guidance">Cảnh báo đối chiếu chỉ nhắc vận hành; khung giờ vẫn được giữ và chưa xác nhận thanh toán.</p>
    {error && <p className="admin-error" role="alert">{error}</p>}
    {busy && !loaded ? <p className="admin-state" role="status">Đang tải thông báo…</p> : loaded && !items.length && !error ?
      <div className="admin-state"><AdminIcon name="notifications" /><p>Chưa có thông báo.</p></div> :
      <ul>{items.map(item => <li key={item.id} className={item.readAt ? '' : 'unread'}>
        <div className="admin-notice-heading"><strong>{item.title}</strong><span className={`admin-badge ${item.readAt ? 'is-muted' : ''}`}>{item.readAt ? 'Đã đọc' : 'Chưa đọc'}</span></div><p>{item.body}</p>
        <time dateTime={item.createdAt}>{new Date(item.createdAt).toLocaleString('vi-VN')}</time>
        {item.action === 'ADMIN_PAYMENT_ALERT' && <small>Liên hệ chủ sân để xử lý đối chiếu. Cổng Admin không xác nhận tiền thay chủ sân.</small>}
        {!item.readAt && <button type="button" className="admin-button-secondary" disabled={!!reading || busy} onClick={() => void markRead(item.id)}>{reading === item.id ? 'Đang cập nhật…' : 'Đánh dấu đã đọc'}</button>}
      </li>)}</ul>}
    {next && <button type="button" className="admin-button-secondary" disabled={busy || !!reading} onClick={() => void load(next)}>Xem thêm thông báo</button>}
  </section>;
}
