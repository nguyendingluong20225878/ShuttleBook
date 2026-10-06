import { useEffect, useState } from 'react';

type Row = { id: string; businessId: string; businessName: string; kind: string; status: string; submittedAt: string };
type Approval = { id: string; kind: string; status: string; snapshot: string; reason?: string };
type Revision = { address: string; contact: string; timezone: string; latitude: number; longitude: number;
  bankCode: string; accountName: string; accountNumber: string; qrUploadId: string };
type Snapshot = { name: string; legalName: string; contact: string; venues: Array<{
  name: string; address: string; contact: string; latitude: number; longitude: number; timezone: string; imageUploadId: string;
  bankCode: string; accountName: string; accountNumber: string; qrUploadId: string;
  courts: Array<{ name: string; hours: Array<{ dayOfWeek: number; opensAt: string; closesAt: string }>;
    prices: Array<{ dayOfWeek: number; startsAt: string; endsAt: string; pricePerSlot: number }> }> }> };
type Envelope<T> = { data: T };
type Notice = { id: string; title: string; body: string; createdAt: string; readAt: string | null };
const base = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';

export function AdminApprovals({ accessToken }: { accessToken: string }) {
  const [rows, setRows] = useState<Row[]>([]);
  const [selected, setSelected] = useState<Approval | null>(null);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [images, setImages] = useState<Record<string, string>>({});
  const [notices, setNotices] = useState<Notice[]>([]);

  async function request<T>(path: string, method = 'GET', body?: object): Promise<T> {
    const response = await fetch(`${base}/api/v1/admin/approval-requests${path}`, { method,
      headers: { Authorization: `Bearer ${accessToken}`, ...(body ? { 'Content-Type': 'application/json' } : {}) },
      ...(body ? { body: JSON.stringify(body) } : {}) });
    if (!response.ok) {
      const error = await response.json().catch(() => ({})) as { code?: string };
      throw new Error(error.code ? `Yêu cầu không thành công (${error.code}).` : 'Không thể kết nối API.');
    }
    return (await response.json() as Envelope<T>).data;
  }
  async function reload() {
    const [approvals, notifications] = await Promise.all([
      request<Row[]>('/'),
      fetch(`${base}/api/v1/me/notifications/`, { headers: { Authorization: `Bearer ${accessToken}` } })
        .then(async response => response.ok ? (await response.json() as Envelope<Notice[]>).data : [])
        .catch(() => [] as Notice[])
    ]);
    setRows(approvals); setNotices(notifications);
  }
  useEffect(() => { void reload().catch(error => setMessage(String(error))); }, [accessToken]);
  async function viewImage(id: string) {
    try {
      const response = await fetch(`${base}/api/v1/uploads/${id}/view`,
        { headers: { Authorization: `Bearer ${accessToken}` } });
      if (!response.ok) throw new Error('Không xem được ảnh.');
      const blobUrl = URL.createObjectURL(await response.blob());
      setImages(current => { if (current[id]) URL.revokeObjectURL(current[id]); return { ...current, [id]: blobUrl }; });
    } catch { setMessage('Không xem được ảnh.'); }
  }
  async function decide(path: string, body?: object) {
    if (!selected) return;
    setBusy(true); setMessage('');
    try {
      await request(`/${selected.id}/${path}`, 'POST', body);
      setSelected(null); setReason(''); await reload(); setMessage('Đã lưu quyết định.');
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Có lỗi xảy ra.'); }
    finally { setBusy(false); }
  }
  let profile: Snapshot | null = null;
  let revision: Revision | null = null;
  try {
    if (selected?.kind === 'VENUE_REVISION') revision = JSON.parse(selected.snapshot) as Revision;
    else if (selected) profile = JSON.parse(selected.snapshot) as Snapshot;
  } catch { profile = null; revision = null; }
  return <section className="admin-approvals">
    <h2>Hồ sơ chờ duyệt</h2>
    <button type="button" disabled={busy} onClick={() => void reload()}>Tải lại danh sách</button>
    {notices.length > 0 && <aside aria-label="Thông báo">
      <h3>Thông báo</h3>
      <ul>{notices.map(notice => <li key={notice.id}>
        <strong>{notice.title}</strong> · {notice.body} · {new Date(notice.createdAt).toLocaleString('vi-VN')}
        {!notice.readAt && <button type="button" onClick={() => void fetch(`${base}/api/v1/me/notifications/${notice.id}/read`,
          { method: 'POST', headers: { Authorization: `Bearer ${accessToken}` } }).then(reload)
          .catch(() => setMessage('Không đánh dấu được thông báo.'))}>
          Đánh dấu đã đọc
        </button>}
      </li>)}</ul>
    </aside>}
    {rows.length === 0 && <p>Chưa có hồ sơ chờ duyệt.</p>}
    <ul>{rows.map(row => <li key={row.id}>
      <button type="button" onClick={() => void request<Approval>(`/${row.id}`).then(setSelected).catch(error => setMessage(String(error)))}>
        {row.businessName} · {row.kind === 'VENUE_REVISION' ? 'Thay đổi cơ sở' : 'Hồ sơ mới'} · {new Date(row.submittedAt).toLocaleString('vi-VN')}
      </button>
    </li>)}</ul>
    {selected && (profile || revision) && <div>
      {revision && <article>
        <h3>Thông tin đề nghị thay đổi</h3>
        <p>Địa chỉ: {revision.address} · Liên hệ: {revision.contact} · {revision.latitude}, {revision.longitude} · {revision.timezone}</p>
        <p>Nhận tiền: {revision.bankCode}, {revision.accountName}, {revision.accountNumber}</p>
        <button type="button" onClick={() => void viewImage(revision!.qrUploadId)}>Xem QR mới</button>
        {images[revision.qrUploadId] && <img src={images[revision.qrUploadId]} alt="QR mới" width="240" />}
      </article>}
      {profile && <>
      <h3>{profile.name}</h3>
      <p>Tên pháp lý: {profile.legalName}</p><p>Liên hệ: {profile.contact}</p>
      {profile.venues.map((venue, index) => <article key={index}>
        <h4>{venue.name}</h4>
        <p>{venue.address} · {venue.contact} · {venue.latitude}, {venue.longitude} · {venue.timezone}</p>
        <p>Nhận tiền: {venue.bankCode}, {venue.accountName}, {venue.accountNumber}</p>
        <button type="button" onClick={() => void viewImage(venue.imageUploadId)}>Xem ảnh cơ sở</button>
        <button type="button" onClick={() => void viewImage(venue.qrUploadId)}>Xem QR</button>
        {images[venue.imageUploadId] && <img src={images[venue.imageUploadId]} alt={`Ảnh ${venue.name}`} width="240" />}
        {images[venue.qrUploadId] && <img src={images[venue.qrUploadId]} alt={`QR ${venue.name}`} width="240" />}
        {venue.courts.map((court, courtIndex) => <div key={courtIndex}>
          <strong>{court.name}</strong>
          <ul>{court.hours.map((hour, hourIndex) => <li key={hourIndex}>
            Ngày {hour.dayOfWeek}, mở {hour.opensAt}–{hour.closesAt}
          </li>)}</ul>
          <ul>{court.prices.map((price, priceIndex) => <li key={priceIndex}>
            Ngày {price.dayOfWeek}, {price.startsAt}–{price.endsAt}: {price.pricePerSlot.toLocaleString('vi-VN')} ₫/30 phút
          </li>)}</ul>
        </div>)}
      </article>)}
      </>}
      <button type="button" disabled={busy} onClick={() => void decide('approve')}>Phê duyệt</button>
      <label>Lý do cần chỉnh sửa <textarea value={reason} onChange={event => setReason(event.target.value)} /></label>
      <button type="button" disabled={busy || reason.trim().length < 10} onClick={() => void decide('request-changes', { reason })}>
        Yêu cầu chỉnh sửa
      </button>
    </div>}
    {message && <p role="status">{message}</p>}
  </section>;
}
