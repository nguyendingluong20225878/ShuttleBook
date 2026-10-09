import { useEffect, useId, useRef, useState } from 'react';
import { AdminIcon } from './components/AdminIcon';
import type { AdminSummary } from './features/workspace/AdminWorkspace';

type Row = { id: string; businessId: string; businessName: string; kind: string; status: string; submittedAt: string };
type CurrentRevision = Revision & { venueName: string; version: number };
type Approval = { id: string; kind: string; status: string; snapshot: string; reason?: string; current?: CurrentRevision | null };
type ApprovalPage = { items: Row[]; nextCursor: string | null; totalCount: number; pendingCount: number };
type Revision = { address: string; contact: string; timezone: string; latitude: number; longitude: number;
  bankCode: string; accountName: string; accountNumber: string; qrUploadId: string };
type Snapshot = { name: string; legalName: string; contact: string; venues: Array<{
  name: string; address: string; contact: string; latitude: number; longitude: number; timezone: string; imageUploadId: string;
  bankCode: string; accountName: string; accountNumber: string; qrUploadId: string;
  courts: Array<{ name: string; hours: Array<{ dayOfWeek: number; opensAt: string; closesAt: string }>;
    prices: Array<{ dayOfWeek: number; startsAt: string; endsAt: string; pricePerSlot: number }> }> }> };
type Envelope<T> = { data: T };
const base = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';
const weekday = (day: number) => day === 0 ? 'Chủ nhật' : `Thứ ${day + 1}`;
const formatTime = (time: string) => time.slice(0, 5);
const formatDate = (value: string) => new Date(value).toLocaleString('vi-VN');

export function AdminApprovals({ accessToken, onSummary }: { accessToken: string; onSummary?: (summary: AdminSummary) => void }) {
  const [rows, setRows] = useState<Row[]>([]);
  const [search, setSearch] = useState('');
  const [kind, setKind] = useState('');
  const [query, setQuery] = useState({ q: '', kind: '' });
  const [next, setNext] = useState<string | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [pendingCount, setPendingCount] = useState<number | null>(null);
  const [confirming, setConfirming] = useState(false);
  const loadedPages = useRef(1);
  const rowCache = useRef<Row[]>([]);
  const [selected, setSelected] = useState<Approval | null>(null);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [loaded, setLoaded] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [loadError, setLoadError] = useState('');
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [images, setImages] = useState<Record<string, string>>({});
  const [imageBusy, setImageBusy] = useState<string | null>(null);
  const imageUrls = useRef<Record<string, string>>({});
  const controller = useRef<AbortController | null>(null);
  const detailController = useRef<AbortController | null>(null);
  const imageController = useRef<AbortController | null>(null);
  const mounted = useRef(true);
  const cacheGeneration = useRef(0);
  const reasonId = useId();
  const comparisonHintId = useId();

  function clearImages() {
    imageController.current?.abort();
    Object.values(imageUrls.current).forEach(url => URL.revokeObjectURL(url));
    imageUrls.current = {};
    setImages({}); setImageBusy(null);
  }
  function denyAccess() {
    cacheGeneration.current++;
    if (!mounted.current) return;
    clearImages(); setRows([]); rowCache.current = []; setNext(null); setTotalCount(0); setPendingCount(null); setConfirming(false); setSelected(null); setReason(''); setLoaded(false); setDetailLoading(false); setLoading(false); setBusy(false);
    setLoadError('Phiên hoặc quyền truy cập không còn hợp lệ. Vui lòng đăng nhập lại.');
  }
  async function request<T>(path: string, method = 'GET', body?: object, signal?: AbortSignal): Promise<T> {
    const requestGeneration = cacheGeneration.current;
    const response = await fetch(`${base}/api/v1/admin/approval-requests${path}`, { method, signal,
      headers: { Authorization: `Bearer ${accessToken}`, ...(body ? { 'Content-Type': 'application/json' } : {}) },
      ...(body ? { body: JSON.stringify(body) } : {}) });
    if (!response.ok) {
      if (!signal?.aborted && mounted.current && requestGeneration === cacheGeneration.current && (response.status === 401 || response.status === 403)) denyAccess();
      const problem = await response.json().catch(() => ({})) as { code?: string };
      if (response.status === 409) throw new Error('Hồ sơ đã thay đổi hoặc đã được xử lý. Vui lòng tải lại danh sách và mở hồ sơ để kiểm tra lại.');
      throw new Error(problem.code ? `Yêu cầu không thành công (${problem.code}).` : 'Không thể tải hồ sơ. Vui lòng thử lại.');
    }
    return (await response.json() as Envelope<T>).data;
  }
  async function reload(cursor?: string) {
    controller.current?.abort();
    const active = new AbortController(); controller.current = active;
    const generation = cacheGeneration.current;
    setLoading(true);
    try {
      const targetPages = cursor ? 1 : loadedPages.current;
      const fetched: Row[] = [];
      let nextCursor: string | null = cursor ?? null;
      let actualPages = 0; let filteredTotal = 0; let globalPending = 0;
      const seenCursors = new Set<string>();
      for (let page = 0; page < targetPages; page++) {
        const params = new URLSearchParams({ paged: 'true', limit: '20' });
        if (query.q) params.set('q', query.q);
        if (query.kind) params.set('kind', query.kind);
        if (nextCursor) params.set('before', nextCursor);
        const result = await request<ApprovalPage | Row[]>(`/?${params}`, 'GET', undefined, active.signal);
        if (!mounted.current || active.signal.aborted || generation !== cacheGeneration.current) return;
        const data = Array.isArray(result) ? { items: result, nextCursor: null, totalCount: result.length, pendingCount: result.length } : result;
        fetched.push(...data.items); actualPages++;
        filteredTotal = data.totalCount; globalPending = data.pendingCount; nextCursor = data.nextCursor;
        if (!nextCursor) break;
        if (seenCursors.has(nextCursor)) throw new Error('Không thể tiếp tục danh sách. Vui lòng tải lại.');
        seenCursors.add(nextCursor);
      }
      const authoritative = cursor ? [...rowCache.current, ...fetched] : fetched;
      const unique = [...new Map(authoritative.map(row => [row.id, row])).values()];
      rowCache.current = unique; loadedPages.current = cursor ? loadedPages.current + actualPages : actualPages;
      setRows(unique); setNext(nextCursor); setTotalCount(filteredTotal); setPendingCount(globalPending); setLoaded(true); setLoadError('');
    } catch (failure) {
      if (mounted.current && !active.signal.aborted && generation === cacheGeneration.current) setLoadError(failure instanceof Error ? failure.message : 'Không thể tải hồ sơ.');
    } finally { if (mounted.current && !active.signal.aborted && generation === cacheGeneration.current) setLoading(false); }
  }
  useEffect(() => {
    mounted.current = true;
    cacheGeneration.current++;
    detailController.current?.abort(); detailController.current = null; imageController.current = null; setDetailLoading(false); setBusy(false);
    loadedPages.current = 1; rowCache.current = []; setRows([]); setNext(null); setLoaded(false); setPendingCount(null); setSelected(null); setReason(''); setConfirming(false); setError(''); setMessage('');
    clearImages();
    void reload();
    return () => {
      mounted.current = false; controller.current?.abort(); detailController.current?.abort(); imageController.current?.abort();
      Object.values(imageUrls.current).forEach(url => URL.revokeObjectURL(url)); imageUrls.current = {};
    };
  }, [accessToken, query]);
  useEffect(() => { onSummary?.({ count: loaded ? pendingCount : null, error: !!loadError }); }, [pendingCount, loaded, loadError, onSummary]);

  async function viewApproval(id: string) {
    detailController.current?.abort(); clearImages();
    const active = new AbortController(); detailController.current = active;
    const generation = cacheGeneration.current;
    setDetailLoading(true); setError(''); setMessage(''); setSelected(null); setReason(''); setConfirming(false);
    try {
      const approval = await request<Approval>(`/${id}`, 'GET', undefined, active.signal);
      if (mounted.current && !active.signal.aborted && generation === cacheGeneration.current) setSelected(approval);
    } catch (failure) {
      if (mounted.current && !active.signal.aborted && generation === cacheGeneration.current) setError(failure instanceof Error ? failure.message : 'Không thể tải chi tiết hồ sơ.');
    } finally { if (mounted.current && !active.signal.aborted && generation === cacheGeneration.current) setDetailLoading(false); }
  }
  async function viewImage(id: string) {
    imageController.current?.abort();
    const active = new AbortController(); imageController.current = active;
    const generation = cacheGeneration.current;
    setImageBusy(id); setError('');
    try {
      const response = await fetch(`${base}/api/v1/uploads/${encodeURIComponent(id)}/view`,
        { headers: { Authorization: `Bearer ${accessToken}` }, signal: active.signal });
      if (!response.ok) {
        if (!active.signal.aborted && mounted.current && generation === cacheGeneration.current && (response.status === 401 || response.status === 403)) denyAccess();
        throw new Error('Không xem được ảnh. Vui lòng thử lại.');
      }
      const blob = await response.blob();
      if (!mounted.current || active.signal.aborted || generation !== cacheGeneration.current) return;
      const blobUrl = URL.createObjectURL(blob);
      if (imageUrls.current[id]) URL.revokeObjectURL(imageUrls.current[id]);
      imageUrls.current = { ...imageUrls.current, [id]: blobUrl };
      setImages(imageUrls.current);
    } catch (failure) {
      if (mounted.current && !active.signal.aborted && generation === cacheGeneration.current) setError(failure instanceof Error ? failure.message : 'Không xem được ảnh.');
    } finally { if (mounted.current && !active.signal.aborted && generation === cacheGeneration.current) setImageBusy(null); }
  }
  async function decide(path: string, body?: object) {
    if (!selected || busy || (path === 'approve' && !confirming) || (path === 'request-changes' && (reason.trim().length < 10 || reason.trim().length > 1000))) return;
    setBusy(true); setMessage(''); setError('');
    const generation = cacheGeneration.current;
    try {
      await request(`/${selected.id}/${path}`, 'POST', body);
      if (!mounted.current || generation !== cacheGeneration.current) return;
      setSelected(null); setReason(''); setConfirming(false); clearImages(); await reload(); setMessage('Đã lưu quyết định.');
    } catch (failure) { if (mounted.current && generation === cacheGeneration.current) setError(failure instanceof Error ? failure.message : 'Có lỗi xảy ra.'); }
    finally { if (mounted.current && generation === cacheGeneration.current) { setBusy(false); setConfirming(false); } }
  }
  let profile: Snapshot | null = null;
  let revision: Revision | null = null;
  try {
    if (selected?.kind === 'VENUE_REVISION') revision = JSON.parse(selected.snapshot) as Revision;
    else if (selected) {
      const parsed = JSON.parse(selected.snapshot) as Snapshot;
      if (Array.isArray(parsed.venues) && parsed.venues.every(venue => Array.isArray(venue.courts) &&
        venue.courts.every(court => Array.isArray(court.hours) && Array.isArray(court.prices)))) profile = parsed;
    }
  } catch { profile = null; revision = null; }

  const reasonLength = reason.trim().length;
  const current = selected?.current;
  const imageButton = (id: string, label: string) => <button type="button" className="admin-button-secondary" disabled={!!imageBusy || busy}
    onClick={() => void viewImage(id)}>{imageBusy === id ? 'Đang tải ảnh…' : label}</button>;
  return <section className="admin-panel admin-approvals sb-panel" aria-labelledby="admin-approvals-title" aria-busy={loading || busy || detailLoading}>
    <header className="admin-panel-heading"><div><h2 id="admin-approvals-title">Hồ sơ chờ duyệt</h2><p>Hồ sơ mới và các thay đổi thông tin cơ sở.</p></div>
      <button type="button" className="admin-button-secondary" disabled={busy || loading} onClick={() => void reload()}><AdminIcon name="refresh" />Tải lại danh sách</button></header>
    <form className="admin-approval-filters" onSubmit={event => { event.preventDefault(); setQuery({ q: search.trim(), kind }); }}>
      <label>Tìm tên doanh nghiệp<input className="sb-field" type="search" value={search} maxLength={120} disabled={busy} onChange={event => setSearch(event.target.value)} /></label>
      <label>Loại hồ sơ<select className="sb-field" aria-label="Loại hồ sơ" value={kind} disabled={busy} onChange={event => setKind(event.target.value)}>
        <option value="">Tất cả loại hồ sơ</option><option value="ONBOARDING">Hồ sơ mới</option><option value="VENUE_REVISION">Thay đổi cơ sở</option>
      </select></label>
      <button type="submit" className="sb-action" disabled={busy}>Tìm hồ sơ</button>
      {(query.q || query.kind) && <button type="button" className="admin-button-secondary" disabled={busy} onClick={() => { setSearch(''); setKind(''); setQuery({ q: '', kind: '' }); }}>Xóa bộ lọc</button>}
    </form>
    {loaded && <p className="admin-guidance" role="status">Hiển thị {rows.length}/{totalCount} hồ sơ phù hợp · {pendingCount} hồ sơ chờ xử lý toàn hệ thống.</p>}
    {loadError && <p className="admin-error sb-notice sb-notice--error" role="alert">{loadError}</p>}
    {loading && !loaded ? <p className="admin-state" role="status">Đang tải hồ sơ…</p> : loaded && rows.length === 0 && !loadError ?
      <div className="admin-state"><AdminIcon name="approvals" /><p>{query.q || query.kind ? 'Không có hồ sơ phù hợp bộ lọc.' : 'Chưa có hồ sơ chờ duyệt.'}</p></div> : null}
    <ul className="admin-approval-list">{rows.map(row => <li key={row.id}>
      <button type="button" className={`admin-approval-row ${selected?.id === row.id ? 'is-selected' : ''}`} disabled={busy || detailLoading}
        onClick={() => void viewApproval(row.id)}>
        <span className="admin-approval-row-content"><strong>{row.businessName}</strong><span className="admin-row-meta">
          <span className="admin-badge">{row.kind === 'VENUE_REVISION' ? 'Thay đổi cơ sở' : 'Hồ sơ mới'}</span><time dateTime={row.submittedAt}>{formatDate(row.submittedAt)}</time>
        </span></span><AdminIcon name="arrow" />
      </button>
    </li>)}</ul>
    {next && <button type="button" className="admin-button-secondary" disabled={loading || busy} onClick={() => void reload(next)}>Xem thêm hồ sơ</button>}
    {detailLoading && <p role="status" className="admin-state">Đang tải chi tiết hồ sơ…</p>}
    {selected && !profile && !revision && <p role="alert" className="admin-error">Không đọc được thông tin hồ sơ. Vui lòng tải lại danh sách và chọn hồ sơ một lần nữa.</p>}
    {selected && (profile || revision) && <div className="admin-approval-detail">
      <header className="admin-detail-heading"><div><p className="admin-eyebrow">CHI TIẾT HỒ SƠ</p><h3>{profile?.name ?? 'Thông tin đề nghị thay đổi'}</h3></div>
        <button type="button" className="admin-button-secondary" disabled={busy} onClick={() => { setSelected(null); setReason(''); setConfirming(false); clearImages(); }}>Đóng chi tiết</button></header>
      {revision && <article className="admin-venue-card">
        <h4>{current?.venueName ?? 'Thông tin cơ sở'}{current && <small> · Phiên bản đang công bố {current.version}</small>}</h4>
        {!current && <p className="admin-guidance">Chưa tải được thông tin đang công bố để đối chiếu. Vui lòng tải lại hồ sơ trước khi phê duyệt.</p>}
        <p id={comparisonHintId} className="admin-comparison-hint">Vuốt ngang bảng để xem thông tin đề nghị thay đổi; dùng phím mũi tên khi bảng được chọn.</p>
        <div className="admin-revision-diff" role="region" tabIndex={0} aria-label="Đối chiếu thông tin cơ sở" aria-describedby={comparisonHintId}>
          <table><thead><tr><th scope="col">Thông tin</th><th scope="col">Đang công bố</th><th scope="col">Đề nghị thay đổi</th></tr></thead>
            <tbody>{([
              ['Địa chỉ', current?.address, revision.address], ['Liên hệ', current?.contact, revision.contact],
              ['Múi giờ', current?.timezone, revision.timezone],
              ['Tọa độ', current ? `${current.latitude}, ${current.longitude}` : undefined, `${revision.latitude}, ${revision.longitude}`],
              ['Ngân hàng', current?.bankCode, revision.bankCode], ['Tên tài khoản', current?.accountName, revision.accountName],
              ['Số tài khoản', current?.accountNumber, revision.accountNumber],
            ] as Array<[string, string | undefined, string]>).map(([label, oldValue, proposed]) => <tr key={label} className={oldValue !== undefined && oldValue !== proposed ? 'is-changed' : ''}>
              <th scope="row">{label}{oldValue !== undefined && oldValue !== proposed && <small>Đã thay đổi</small>}</th><td>{oldValue ?? 'Chưa có dữ liệu'}</td><td>{proposed}</td>
            </tr>)}</tbody>
          </table>
        </div>
        <div className="admin-qr-comparison">
          {current?.qrUploadId && <section><h5>QR đang công bố</h5>{imageButton(current.qrUploadId, 'Xem QR đang công bố')}
            {images[current.qrUploadId] && <img className="admin-private-image" src={images[current.qrUploadId]} alt="QR đang công bố" width="240" />}</section>}
          <section><h5>QR đề nghị thay đổi</h5>{imageButton(revision.qrUploadId, 'Xem QR mới')}
            {images[revision.qrUploadId] && <img className="admin-private-image" src={images[revision.qrUploadId]} alt="QR mới" width="240" />}</section>
        </div>
      </article>}
      {profile && <>
        <dl className="admin-facts"><div><dt>Tên pháp lý</dt><dd>{profile.legalName}</dd></div><div><dt>Liên hệ</dt><dd>{profile.contact}</dd></div></dl>
        {profile.venues.map((venue, index) => <article className="admin-venue-card" key={index}>
          <h4>{venue.name}</h4>
          <dl className="admin-facts"><div><dt>Địa chỉ</dt><dd>{venue.address}</dd></div><div><dt>Liên hệ</dt><dd>{venue.contact}</dd></div>
            <div><dt>Múi giờ</dt><dd>{venue.timezone}</dd></div><div><dt>Tọa độ</dt><dd>{venue.latitude}, {venue.longitude}</dd></div>
            <div><dt>Ngân hàng</dt><dd>{venue.bankCode}</dd></div><div><dt>Tài khoản nhận tiền</dt><dd>{venue.accountName} · {venue.accountNumber}</dd></div></dl>
          <div className="admin-actions">{imageButton(venue.imageUploadId, 'Xem ảnh cơ sở')}{imageButton(venue.qrUploadId, 'Xem QR')}</div>
          <div className="admin-image-list">{images[venue.imageUploadId] && <img className="admin-private-image" src={images[venue.imageUploadId]} alt={`Ảnh ${venue.name}`} width="240" />}
            {images[venue.qrUploadId] && <img className="admin-private-image" src={images[venue.qrUploadId]} alt={`QR ${venue.name}`} width="240" />}</div>
          {venue.courts.map((court, courtIndex) => <section className="admin-court-card" key={courtIndex}>
            <h5>{court.name}</h5><div className="admin-court-config"><div><h6>Giờ hoạt động</h6>
              <ul>{court.hours.map((hour, hourIndex) => <li key={hourIndex}>{weekday(hour.dayOfWeek)} <strong>{formatTime(hour.opensAt)}–{formatTime(hour.closesAt)}</strong></li>)}</ul>
            </div><div><h6>Bảng giá / 30 phút</h6>
              <ul>{court.prices.map((price, priceIndex) => <li key={priceIndex}>{weekday(price.dayOfWeek)} · {formatTime(price.startsAt)}–{formatTime(price.endsAt)}
                <strong>{price.pricePerSlot.toLocaleString('vi-VN')} ₫</strong></li>)}</ul>
            </div></div>
          </section>)}
        </article>)}
      </>}
      {selected.status === 'PENDING' && <div className="admin-decision">
        <div><h4>Quyết định duyệt hồ sơ</h4><p>Phê duyệt khi thông tin cơ sở, lịch, giá và QR đã phù hợp.</p>
          <button type="button" disabled={busy || (selected.kind === 'VENUE_REVISION' && !current)} onClick={() => { setConfirming(true); setError(''); }}>Phê duyệt</button>
          {confirming && <div className="admin-approve-confirm" role="group" aria-labelledby="admin-confirm-title">
            <h5 id="admin-confirm-title">Xác nhận phê duyệt hồ sơ</h5>
            <p>{profile ? `Công bố doanh nghiệp ${profile.name} cùng ${profile.venues.length} cơ sở và cấu hình sân trong hồ sơ.` : `Áp dụng thông tin và tài khoản nhận tiền đề nghị cho ${current?.venueName ?? 'cơ sở này'}.`}</p>
            <p>Hãy kiểm tra địa chỉ, lịch, giá và QR trước khi xác nhận. Quyết định được lưu ngay sau bước này.</p>
            <div className="admin-actions"><button type="button" disabled={busy} onClick={() => void decide('approve')}>{busy ? 'Đang lưu…' : 'Xác nhận phê duyệt'}</button>
              <button type="button" className="admin-button-secondary" disabled={busy} onClick={() => setConfirming(false)}>Hủy phê duyệt</button></div>
          </div>}</div>
        <div className="admin-changes"><label htmlFor={reasonId}>Lý do cần chỉnh sửa</label><textarea id={reasonId} rows={3} disabled={busy} value={reason}
          onChange={event => setReason(event.target.value)} aria-describedby={`${reasonId}-help`} aria-invalid={reasonLength > 1000} />
          <small id={`${reasonId}-help`}>Nêu rõ thông tin cần sửa, từ 10 đến 1.000 ký tự sau khi bỏ khoảng trắng đầu/cuối. {reasonLength}/1.000 ký tự.</small>
          <button type="button" className="admin-button-secondary" disabled={busy || reasonLength < 10 || reasonLength > 1000}
            onClick={() => void decide('request-changes', { reason: reason.trim() })}>Yêu cầu chỉnh sửa</button></div>
      </div>}
    </div>}
    {error && <p className="admin-error" role="alert">{error}</p>}
    {message && <p role="status" className="admin-success">{message}</p>}
  </section>;
}
