import { useEffect, useState } from 'react';

type Report = { id: string; postId: string; reason: string; createdAt: string; status: string;
  post: { venueName: string; courtName: string; date: string; startsAt: string; endsAt: string; slotsOpen: number } };
type HiddenPost = { id: string; venueName: string; courtName: string; date: string;
  startsAt: string; endsAt: string; slotsOpen: number; status: string };
const base = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';
const reasons: Record<string, string> = { SPAM: 'Bài rác', MISLEADING: 'Thông tin gây hiểu nhầm', UNSAFE: 'Không an toàn' };

export function AdminRecruitment({ accessToken }: { accessToken: string }) {
  const [items, setItems] = useState<Report[]>([]); const [loading, setLoading] = useState(true);
  const [hidden, setHidden] = useState<HiddenPost[]>([]);
  const [error, setError] = useState(''); const [message, setMessage] = useState(''); const [busy, setBusy] = useState<string | null>(null);
  const [reload, setReload] = useState(0);
  useEffect(() => { const controller = new AbortController(); setLoading(true); setError('');
    void Promise.all([fetch(`${base}/api/v1/admin/recruitment/reports`, { signal: controller.signal,
      headers: { Authorization: `Bearer ${accessToken}` } }).then(async response => {
      const body = await response.json(); if (!response.ok) throw new Error(body.code ?? 'REQUEST_FAILED');
      return body.data as { items: Report[] };
    }), fetch(`${base}/api/v1/admin/recruitment/posts?status=HIDDEN`, { signal: controller.signal,
      headers: { Authorization: `Bearer ${accessToken}` } }).then(async response => {
      const body = await response.json(); if (!response.ok) throw new Error(body.code ?? 'REQUEST_FAILED');
      return body.data as { items: HiddenPost[] };
    })]).then(([reports, posts]) => { if (!controller.signal.aborted) { setItems(reports.items); setHidden(posts.items); } })
      .catch(() => { if (!controller.signal.aborted) setError('Chưa tải được báo cáo. Kiểm tra phiên và thử lại.'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); }); return () => controller.abort();
  }, [accessToken, reload]);
  async function decide(id: string, action: 'hide' | 'dismiss') {
    if (busy) return; setBusy(id); setError(''); setMessage('');
    try {
      const response = await fetch(`${base}/api/v1/admin/recruitment/reports/${id}/${action}`, {
        method: 'POST', headers: { Authorization: `Bearer ${accessToken}`, 'Content-Type': 'application/json' }, body: '{}'
      });
      if (!response.ok) { const body = await response.json(); throw new Error(body.code ?? 'REQUEST_FAILED'); }
      setMessage(action === 'hide' ? 'Đã ẩn bài và lưu quyết định.' : 'Đã bỏ qua báo cáo và lưu quyết định.');
      setReload(value => value + 1);
    } catch { setError('Chưa xử lý được báo cáo. Hãy làm mới và thử lại.'); } finally { setBusy(null); }
  }
  async function restore(id: string) {
    if (busy) return; setBusy(id); setError(''); setMessage('');
    try {
      const response = await fetch(`${base}/api/v1/admin/recruitment/posts/${id}/restore`, {
        method: 'POST', headers: { Authorization: `Bearer ${accessToken}`, 'Content-Type': 'application/json' }, body: '{}'
      });
      if (!response.ok) { const body = await response.json(); throw new Error(body.code ?? 'REQUEST_FAILED'); }
      setMessage('Đã khôi phục bài.'); setReload(value => value + 1);
    } catch { setError('Chưa khôi phục được bài. Lịch chơi có thể đã hết hạn hoặc không còn hợp lệ.'); }
    finally { setBusy(null); }
  }
  return <section className="admin-card" aria-label="Báo cáo bài tìm người"><h2>Báo cáo bài tìm người</h2>
    <p>Người báo không tự ẩn bài. Quản trị xem thông tin lịch chơi và quyết định.</p>
    {loading && <p role="status">Đang tải báo cáo…</p>}{error && <p role="alert">{error} <button type="button" onClick={() => setReload(v => v + 1)}>Thử lại</button></p>}
    {message && <p role="status">{message}</p>}{!loading && !items.length && !error && <p>Không có báo cáo chờ xử lý.</p>}
    <ul>{items.map(item => <li key={item.id} style={{ borderTop: '1px solid #d9e4df', padding: '1rem 0' }}>
      <strong>{item.post.venueName} · {item.post.courtName}</strong>
      <p>{item.post.date} {item.post.startsAt}–{item.post.endsAt} · Còn {item.post.slotsOpen} suất</p>
      <p>Lý do: {reasons[item.reason] ?? item.reason} · {new Date(item.createdAt).toLocaleString('vi-VN')}</p>
      <button type="button" disabled={!!busy} onClick={() => void decide(item.id, 'hide')}>Ẩn bài</button>{' '}
      <button type="button" disabled={!!busy} onClick={() => void decide(item.id, 'dismiss')}>Bỏ qua báo cáo</button>
    </li>)}</ul>
    <h2>Bài đang ẩn</h2>{!loading && !hidden.length && <p>Không có bài đang ẩn.</p>}
    <ul>{hidden.map(item => <li key={item.id} style={{ borderTop: '1px solid #d9e4df', padding: '1rem 0' }}>
      <strong>{item.venueName} · {item.courtName}</strong><p>{item.date} {item.startsAt}–{item.endsAt}</p>
      <button type="button" disabled={!!busy} onClick={() => void restore(item.id)}>Khôi phục bài</button>
    </li>)}</ul>
  </section>;
}
