import { useEffect, useRef, useState, type FormEvent } from 'react';
import { PartnerApiError, bookingStatusLabel, localDateTime, money, paymentError, type BookingDetail, type BookingList, type PartnerRequest } from './types';
import { BookingDecisions } from './BookingDecisions';

type Venue = { id: string; name: string };
type Filter = { status: string; dateFrom: string; dateTo: string };
const statuses = ['AWAITING_TRANSFER', 'AWAITING_OWNER_CONFIRMATION', 'NEEDS_REVIEW', 'CONFIRMED', 'EXPIRED', 'PAYMENT_REJECTED'];
export function PartnerBookings({ businessId, venues, request, bookingId, openBooking, selectBusiness, onActionBusy }: {
  businessId: string; venues: Venue[]; request: PartnerRequest; bookingId: string | null;
  openBooking: (id: string | null) => void; selectBusiness: (id: string) => boolean;
  onActionBusy: (busy: boolean) => void;
}) {
  const latestRequest = useRef(request); latestRequest.current = request;
  const selectBusinessRef = useRef(selectBusiness); selectBusinessRef.current = selectBusiness;
  const [venueId, setVenueId] = useState(venues[0]?.id ?? '');
  const [draft, setDraft] = useState<Filter>({ status: '', dateFrom: '', dateTo: '' });
  const [filter, setFilter] = useState(draft);
  const [list, setList] = useState<BookingList | null>(null);
  const [listError, setListError] = useState('');
  const [loading, setLoading] = useState(false);
  const [moreLoading, setMoreLoading] = useState(false);
  const [detail, setDetail] = useState<BookingDetail | null>(null);
  const [detailError, setDetailError] = useState('');
  const [detailLoading, setDetailLoading] = useState(false);
  const [reload, setReload] = useState(0);
  const [listReload, setListReload] = useState(0);
  const more = useRef<AbortController | null>(null);
  const scopeGeneration = useRef(0);
  const detailGeneration = useRef(0);
  const actionBusy = useRef(false);
  const detailRef = useRef<BookingDetail | null>(null); detailRef.current = detail;
  function listPath(before?: string) {
    const query = new URLSearchParams({ limit: '20' });
    for (const [key, value] of Object.entries(filter)) if (value) query.set(key, value);
    if (before) query.set('before', before);
    return `/operator/venues/${venueId}/bookings?${query}`;
  }
  useEffect(() => {
    const generation = ++scopeGeneration.current;
    const controller = new AbortController(); let current: AbortController | null = controller;
    setList(null); setListError(''); setLoading(true); setMoreLoading(false);
    if (!venueId) { setLoading(false); return; }
    async function load(signal: AbortSignal, initial = false) {
      try {
        const result = await latestRequest.current<BookingList>(listPath(), 'GET', undefined, undefined, { signal });
        if (!signal.aborted && scopeGeneration.current === generation) { setList(result); setListError(''); }
      } catch (error) { if (!signal.aborted && scopeGeneration.current === generation) {
        if (error instanceof PartnerApiError && [403, 404].includes(error.status)) setList(null);
        setListError(paymentError(error));
      } }
      finally { if (scopeGeneration.current === generation && !signal.aborted && initial) setLoading(false); }
    }
    void load(controller.signal, true).finally(() => { if (current === controller) current = null; });
    const update = () => {
      if (document.visibilityState !== 'visible' || current || more.current || actionBusy.current) return;
      current = new AbortController(); const next = current;
      void load(next.signal).finally(() => { if (current === next) current = null; });
    };
    const timer = window.setInterval(update, 5000);
    window.addEventListener('focus', update); document.addEventListener('visibilitychange', update);
    return () => { current?.abort(); more.current?.abort(); more.current = null; scopeGeneration.current++;
      window.clearInterval(timer); window.removeEventListener('focus', update); document.removeEventListener('visibilitychange', update); };
  }, [venueId, filter, reload, listReload]);
  useEffect(() => {
    const generation = ++detailGeneration.current;
    if (!bookingId) { setDetail(null); setDetailError(''); setDetailLoading(false); return; }
    const controller = new AbortController(); let current: AbortController | null = controller; let alive = true;
    setDetail(null); setDetailError(''); setDetailLoading(true);
    async function load(signal: AbortSignal, initial = false) {
      try {
        const result = await latestRequest.current<BookingDetail>(`/operator/bookings/${bookingId}`, 'GET', undefined, undefined, { signal });
        if (!alive || signal.aborted || detailGeneration.current !== generation) return;
        if (actionBusy.current || (detailRef.current?.bookingId === result.bookingId && detailRef.current.version > result.version)) return;
        if (result.businessId && result.businessId !== businessId) {
          if (!selectBusinessRef.current(result.businessId)) throw new Error('OUTSIDE_BUSINESS');
          return;
        }
        if (!venues.some(item => item.id === result.venueId)) throw new Error('OUTSIDE_BUSINESS');
        setDetail(result); setDetailError('');
        setVenueId(result.venueId);
      } catch (error) { if (alive && !signal.aborted && detailGeneration.current === generation) {
        if (error instanceof PartnerApiError && [403, 404].includes(error.status)) setDetail(null);
        setDetailError(paymentError(error));
      } }
      finally { if (alive && !signal.aborted && detailGeneration.current === generation && initial) setDetailLoading(false); }
    }
    void load(controller.signal, true).finally(() => { if (current === controller) current = null; });
    const update = () => {
      if (document.visibilityState !== 'visible' || current || actionBusy.current) return;
      current = new AbortController(); const next = current;
      void load(next.signal).finally(() => { if (current === next) current = null; });
    };
    const timer = window.setInterval(update, 5000);
    window.addEventListener('focus', update); document.addEventListener('visibilitychange', update);
    return () => { alive = false; detailGeneration.current++; current?.abort(); window.clearInterval(timer);
      window.removeEventListener('focus', update); document.removeEventListener('visibilitychange', update); };
  }, [bookingId, businessId, reload]);
  async function loadMore() {
    if (!list?.nextCursor || more.current) return;
    const generation = scopeGeneration.current; const controller = new AbortController(); more.current = controller;
    setMoreLoading(true); setListError('');
    try {
      const result = await latestRequest.current<BookingList>(listPath(list.nextCursor), 'GET', undefined, undefined, { signal: controller.signal });
      if (!controller.signal.aborted && scopeGeneration.current === generation) setList(previous => ({ ...result,
        items: [...(previous?.items ?? []), ...result.items.filter(item => !previous?.items.some(old => old.bookingId === item.bookingId))] }));
    } catch (error) { if (!controller.signal.aborted && scopeGeneration.current === generation) setListError(paymentError(error)); }
    finally { if (more.current === controller) { more.current = null; setMoreLoading(false); } }
  }
  function apply(event: FormEvent) {
    event.preventDefault();
    if (draft.dateFrom && draft.dateTo && draft.dateFrom > draft.dateTo) { setListError('Ngày kết thúc phải từ ngày bắt đầu trở đi.'); return; }
    if (draft.dateFrom && draft.dateTo && (Date.parse(draft.dateTo) - Date.parse(draft.dateFrom)) / 86400000 > 366) {
      setListError('Chọn khoảng ngày tối đa 366 ngày.'); return;
    }
    setFilter({ ...draft });
  }
  return <section className="partner-bookings" aria-label="Quản lý đơn đặt sân">
    <div className="panel booking-scope"><label>Cơ sở xem đơn <select value={venueId} onChange={event => {
      openBooking(null); setVenueId(event.target.value); setDetail(null); setDraft({ status: '', dateFrom: '', dateTo: '' });
      setFilter({ status: '', dateFrom: '', dateTo: '' });
    }}>{venues.map(venue => <option key={venue.id} value={venue.id}>{venue.name}</option>)}</select></label>
      <p className="muted">Ngày lọc và giờ chơi theo múi giờ của cơ sở. Đơn đã báo chuyển tiếp tục giữ sân trong lúc đối chiếu.</p></div>
    {venues.length === 0 ? <div className="panel empty-state"><h3>Chưa có cơ sở</h3><p>Thêm và hoàn tất hồ sơ cơ sở trước khi nhận đơn.</p></div> : <>
      <div className="booking-counts">
        <button type="button" onClick={() => { const next = { ...filter, status: 'AWAITING_OWNER_CONFIRMATION' }; setDraft(next); setFilter(next); }}>
          <span>Chờ xác nhận</span><strong>{list?.counts.awaitingOwnerConfirmation ?? '—'}</strong></button>
        <button type="button" onClick={() => { const next = { ...filter, status: 'NEEDS_REVIEW' }; setDraft(next); setFilter(next); }}>
          <span>Cần bổ sung</span><strong>{list?.counts.needsReview ?? '—'}</strong></button>
      </div>
      <form className="booking-filters" onSubmit={apply}>
        <label>Trạng thái đơn <select value={draft.status} onChange={e => setDraft({ ...draft, status: e.target.value })}>
          <option value="">Tất cả trạng thái</option>{statuses.map(status => <option key={status} value={status}>{bookingStatusLabel(status)}</option>)}</select></label>
        <label>Từ ngày <input type="date" value={draft.dateFrom} onChange={e => setDraft({ ...draft, dateFrom: e.target.value })} /></label>
        <label>Đến ngày <input type="date" value={draft.dateTo} onChange={e => setDraft({ ...draft, dateTo: e.target.value })} /></label>
        <button type="submit">Lọc đơn</button><button type="button" disabled={loading || moreLoading} onClick={() => setReload(value => value + 1)}>Tải lại đơn</button>
      </form>
      {loading && <p role="status">Đang tải đơn đặt sân…</p>}
      {listError && <p className="feedback error" role="alert">{listError}</p>}
      {!loading && list?.items.length === 0 && <div className="panel empty-state"><h3>Chưa có đơn phù hợp</h3><p>Kiểm tra cơ sở, trạng thái và khoảng ngày đã chọn.</p></div>}
      <ul className="booking-list">{list?.items.map(item => <li key={item.bookingId} className={item.bookingId === bookingId ? 'selected' : ''}>
        <div><strong>{item.bookingNo}</strong><p>{item.courtName} · {item.date} · {item.localStart.slice(0, 5)}–{item.localEnd.slice(0, 5)}</p>
          <span className={`status-badge status-${item.status.toLowerCase()}`}>{bookingStatusLabel(item.status)}</span>
          {item.isOverdue && <span className="status-badge status-overdue">Quá hạn đối chiếu</span>}</div>
        <div className="booking-list-price"><strong>{money(item.amountExact ?? item.amount)}</strong><button type="button" onClick={() => openBooking(item.bookingId)}>Xem đơn {item.bookingNo}</button></div>
      </li>)}</ul>
      {list?.nextCursor && <button type="button" disabled={moreLoading} onClick={() => void loadMore()}>{moreLoading ? 'Đang tải…' : 'Xem thêm đơn'}</button>}
    </>}
    {bookingId && <section className="panel booking-detail" aria-label="Chi tiết đơn đặt sân">
      <div className="panel-heading"><h3>Chi tiết đơn đặt sân</h3><button type="button" onClick={() => openBooking(null)}>Đóng chi tiết</button></div>
      {detailLoading && <p role="status">Đang tải chi tiết đơn…</p>}
      {detailError && <div className="feedback error" role="alert">{detailError}<button type="button" onClick={() => setReload(value => value + 1)}>Tải lại chi tiết</button></div>}
      {detail && <>
        <BookingInformation key={detail.bookingId} booking={detail} request={request} />
        {['AWAITING_OWNER_CONFIRMATION', 'NEEDS_REVIEW'].includes(detail.status) &&
          <BookingDecisions key={`${detail.bookingId}-${reload}`} booking={detail} request={request}
            onBusy={busy => { actionBusy.current = busy; onActionBusy(busy); }} reload={() => setReload(value => value + 1)}
            onUnavailable={error => {
              scopeGeneration.current++; detailGeneration.current++; more.current?.abort(); more.current = null;
              detailRef.current = null; setDetail(null); setList(null); setDetailError(paymentError(error));
              setLoading(false); setMoreLoading(false); setDetailLoading(false);
            }}
            onUpdated={updated => {
              if (updated.businessId !== businessId || !venues.some(venue => venue.id === updated.venueId)) return;
              detailRef.current = updated; setDetail(updated);
              setListReload(value => value + 1);
            }} />}
      </>}
    </section>}
  </section>;
}

function PrivateProof({ url, request }: { url: string; request: PartnerRequest }) {
  const [image, setImage] = useState<string | null>(null);
  const [error, setError] = useState(''); const [loading, setLoading] = useState(false);
  const current = useRef<AbortController | null>(null); const blobUrl = useRef<string | null>(null);
  useEffect(() => () => { current.current?.abort(); if (blobUrl.current) URL.revokeObjectURL(blobUrl.current); }, [url]);
  async function load() {
    if (!/^\/api\/v1\/uploads\/[0-9a-f-]{36}\/view$/i.test(url)) { setError('Liên kết bằng chứng không hợp lệ.'); return; }
    const controller = new AbortController(); current.current = controller;
    setLoading(true); setError('');
    try {
      const blob = await request<Blob>(url.replace(/^\/api\/v1/, ''), 'GET', undefined, undefined, { blob: true, signal: controller.signal });
      if (controller.signal.aborted) return;
      if (blobUrl.current) URL.revokeObjectURL(blobUrl.current);
      blobUrl.current = URL.createObjectURL(blob); setImage(blobUrl.current);
    } catch { if (!controller.signal.aborted) setError('Không tải được bằng chứng trong quyền của bạn.'); }
    finally { if (!controller.signal.aborted) setLoading(false); }
  }
  return <div className="private-proof">{image ? <><img src={image} alt="Biên lai chuyển khoản khách cung cấp" />
    <button type="button" onClick={() => { if (blobUrl.current) URL.revokeObjectURL(blobUrl.current); blobUrl.current = null; setImage(null); }}>Ẩn biên lai</button></>
    : <button type="button" disabled={loading} onClick={() => void load()}>{loading ? 'Đang tải biên lai…' : 'Xem biên lai'}</button>}
    {error && <p role="alert">{error}</p>}</div>;
}
function BookingInformation({ booking, request }: { booking: BookingDetail; request: PartnerRequest }) {
  const payment = booking.payment;
  const history = [...(booking.evidence ?? []).map(item => ({ id: item.evidenceId, at: item.reportedAt,
    title: item.kind === 'SUPPLEMENT' ? 'Khách bổ sung bằng chứng' : 'Khách báo đã chuyển khoản', detail: item.bankReference, note: item.note, proofUrl: item.proofUrl })),
    ...(booking.decisions ?? []).map(item => ({ id: item.decisionId, at: item.decidedAt,
      title: item.resolution === 'CONFIRMED' ? 'Chủ sân xác nhận thanh toán' : item.resolution === 'NEEDS_REVIEW' ? 'Chủ sân yêu cầu bổ sung' : 'Chủ sân không xác nhận giao dịch',
      detail: item.reason ?? (item.confirmedAmount !== null ? `${money(item.confirmedAmountExact ?? item.confirmedAmount)}${item.bankReference ? ` · ${item.bankReference}` : ''}` : ''), note: item.note, proofUrl: null }))]
    .sort((a, b) => b.at.localeCompare(a.at));
  return <>
    <div className="booking-detail-heading"><div><p className="eyebrow">{booking.bookingNo}</p><h4>{booking.venueName} / {booking.courtName}</h4>
      <p>{booking.date} · {booking.localStart.slice(0, 5)}–{booking.localEnd.slice(0, 5)} · {booking.timezone}</p></div>
      <span className={`status-badge status-${booking.status.toLowerCase()}`}>{bookingStatusLabel(booking.status)}</span></div>
    <dl className="booking-facts"><div><dt>Tổng tiền theo đơn</dt><dd>{money(payment.expectedAmountExact ?? payment.expectedAmount)}</dd></div>
      <div><dt>Khách đặt sân</dt><dd>{booking.customer?.maskedContact ?? 'Đã bảo vệ thông tin liên hệ'}</dd></div>
      <div><dt>Tài khoản nhận tiền đã chốt</dt><dd>{payment.bankCode} · {payment.maskedAccountNumber}<small>{payment.accountName}</small></dd></div>
      <div><dt>Nội dung chuyển khoản</dt><dd>{payment.transferContent}</dd></div>
      <div><dt>Lần báo chuyển đầu tiên</dt><dd>{localDateTime(payment.firstReportedAt, booking.timezone)}</dd></div>
      <div><dt>Tiền đã xác nhận</dt><dd>{payment.confirmedAmount !== null ? money(payment.confirmedAmountExact ?? payment.confirmedAmount) : 'Chưa xác nhận'}</dd></div></dl>
    {booking.isOverdue && <p className="feedback">Đơn đang chờ đối chiếu quá hạn. Sân vẫn được giữ; hãy kiểm tra giao dịch và phản hồi khách.</p>}
    {['AWAITING_OWNER_CONFIRMATION', 'NEEDS_REVIEW'].includes(booking.status) && <p className="feedback">Đối chiếu giao dịch với tài khoản và tổng tiền đã chốt trong đơn trước khi quyết định.</p>}
    {booking.status === 'CONFIRMED' && <p className="feedback success">Đã xác nhận thanh toán lúc {localDateTime(payment.confirmedAt, booking.timezone)}. Đơn đã hoàn tất luồng đặt sân.</p>}
    <h4>Lịch sử đối chiếu</h4>
    {history.length === 0 ? <p className="muted">Khách chưa báo chuyển khoản.</p> : <ol className="booking-history">{history.map(item => <li key={item.id}>
      <strong>{item.title}</strong><time dateTime={item.at}>{localDateTime(item.at, booking.timezone)}</time><p>{item.detail}</p>
      {item.note && <p>{item.note}</p>}{item.proofUrl && <PrivateProof url={item.proofUrl} request={request} />}
    </li>)}</ol>}
  </>;
}
