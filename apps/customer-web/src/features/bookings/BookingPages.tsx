import { useEffect, useRef, useState } from 'react';
import { useCustomerSession } from '../auth/CustomerSession';
import { navigate } from '../../routes/navigation';
import { accessDenied, data, dateTime, failure, money, type Booking, type Slot } from './bookingApi';
import { BookingHistory } from './BookingHistory';
import { BookingStatus } from './BookingStatus';
import { PrivateImage } from './PrivateImage';
import { useVisiblePolling } from '../../hooks/useVisiblePolling';
import { BookingTransfer } from './BookingTransfer';
import { CustomerShell as Shell } from '../../components/CustomerShell';
import { BookingFacts, PriceBreakdown } from './BookingFacts';
import { SeriesSummary } from './SeriesSummary';
import { weekdayLabel } from './seriesApi';
import './bookings.css';

type Quote = { quoteId: string; expiresAt: string; courtId: string; venueId: string; courtName: string; venueName: string;
  date: string; timezone: string; startsAt: string; endsAt: string; amount: number; slots: Slot[]; holdMinutes: number };
function useNow() { const [now, setNow] = useState(Date.now()); useEffect(() => { const timer = setInterval(() => setNow(Date.now()), 1000); return () => clearInterval(timer); }, []); return now; }
function remaining(deadline: string, now: number) { const seconds = Math.max(0, Math.ceil((Date.parse(deadline) - now) / 1000)); return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`; }
function useRequiredSession() {
  const auth = useCustomerSession();
  useEffect(() => { if (!auth.session && location.pathname !== '/login') navigate(`/login?returnTo=${encodeURIComponent(location.pathname + location.search)}`, true); }, [auth.session]);
  return auth;
}
export function BookingReview() {
  const { session, request } = useRequiredSession(); const now = useNow();
  const [quote, setQuote] = useState<Quote | null>(null); const [error, setError] = useState('');
  const [loading, setLoading] = useState(false); const [submitting, setSubmitting] = useState(false); const [retry, setRetry] = useState(0);
  const intent = useRef<{ quoteId: string; key: string; body: string; attempted: boolean } | null>(null); const creating = useRef(false);
  const expired = Boolean(quote && now >= Date.parse(quote.expiresAt));
  const canRetry = Boolean(quote && intent.current?.quoteId === quote.quoteId && intent.current.attempted);
  const params = new URLSearchParams(location.search);
  const courtId = params.get('courtId'); const date = params.get('date'); const startsAt = params.get('startsAt'); const endsAt = params.get('endsAt');
  const venueId = params.get('venueId');
  useEffect(() => {
    if (!session) return;
    const controller = new AbortController(); setLoading(true); setError(''); setQuote(null); intent.current = null;
    void request('/api/v1/availability/quote', { method: 'POST', signal: controller.signal,
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ courtId, date, startsAt, endsAt }) })
      .then(data<Quote>).then(value => { if (!controller.signal.aborted) { setQuote(value); intent.current = { quoteId: value.quoteId, key: crypto.randomUUID(), attempted: false,
        body: JSON.stringify({ courtId: value.courtId, quoteId: value.quoteId, startsAt: value.startsAt, endsAt: value.endsAt }) }; } })
      .catch(reason => { if (!controller.signal.aborted) setError(failure(reason)); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [Boolean(session), request, courtId, date, startsAt, endsAt, retry]);
  async function create() {
    const attempt = intent.current;
    if (!quote || !attempt || attempt.quoteId !== quote.quoteId || creating.current ||
      (Date.parse(quote.expiresAt) <= Date.now() && !attempt.attempted)) return;
    // A lost response may already have committed. Preserve this exact intent for
    // the server's idempotency replay before considering a replacement quote.
    creating.current = true; attempt.attempted = true; setSubmitting(true); setError('');
    try {
      const response = await request('/api/v1/bookings', { method: 'POST', headers: { 'Content-Type': 'application/json',
        'Idempotency-Key': attempt.key }, body: attempt.body });
      const result = await data<Booking>(response); navigate(`/bookings/${result.bookingId}`, true);
    } catch (reason) {
      setError(failure(reason));
      if (reason instanceof Error && ['QUOTE_CONSUMED', 'QUOTE_EXPIRED', 'QUOTE_CHANGED', 'SLOT_UNAVAILABLE', 'IDEMPOTENCY_KEY_REUSED',
        'SCHEDULE_UNAVAILABLE', 'PRICE_UNAVAILABLE', 'PAYMENT_SETUP_UNAVAILABLE', 'VALIDATION_FAILED', 'UNSUPPORTED_FIELD', 'NOT_FOUND', 'FORBIDDEN'].includes(reason.message)) { setQuote(null); intent.current = null; }
    } finally { creating.current = false; setSubmitting(false); }
  }
  return <Shell><section className="booking-panel"><h1>Xác nhận đặt vãng lai</h1><a href={`/venues/${venueId}?date=${date}`}>Quay lại lịch các sân</a>
    {loading && <p role="status">Đang lấy báo giá…</p>}{error && <p role="alert">{error}</p>}
    {quote && <><h2>{quote.venueName} · {quote.courtName}</h2><p>Ngày {quote.date} · Múi giờ {quote.timezone}</p>
      <PriceBreakdown slots={quote.slots} />
      <p className="booking-total">Tổng tiền: {money(quote.amount)}</p><div className="quote-hold-notice"><p>{now < Date.parse(quote.expiresAt) ? 'Đang giữ chỗ tạm cho bạn' : 'Đã hết thời gian giữ chỗ tạm'}: <strong>{remaining(quote.expiresAt, now)}</strong></p>
        <p>Các ca trong báo giá được khóa đến hết thời gian trên. Nếu bạn chưa tạo đơn, các ca sẽ tự trở về trống.</p></div>
      <p>Sau khi tạo đơn thành công, sân tiếp tục được giữ {quote.holdMinutes} phút để bạn chuyển khoản.</p>
      <button type="button" className="primary-action" onClick={() => { void create(); }} disabled={submitting || (expired && !canRetry)}>{submitting ? 'Đang tạo đơn…' : 'Xác nhận tạo đơn'}</button>
      {canRetry && !submitting && <p role="status">Chưa xác định được kết quả tạo đơn. Bạn có thể xác nhận lại cùng yêu cầu đã gửi hoặc xem Đơn của tôi. Không lấy báo giá mới khi chưa kiểm tra đơn cũ.</p>}
      {expired && <p role="status">{canRetry ? 'Báo giá đã hết hạn. Việc gửi lại chỉ kiểm tra yêu cầu cũ; máy chủ sẽ trả đơn đã tạo hoặc xác nhận yêu cầu không còn hợp lệ.' : 'Báo giá đã hết hạn và chỗ tạm đã được giải phóng. Hãy lấy báo giá mới trước khi tiếp tục.'}</p>}</>}
    <button type="button" disabled={loading || submitting || canRetry} onClick={() => { if (!intent.current?.attempted) setRetry(value => value + 1); }}>Lấy báo giá mới</button>
    <p><a href="/me/bookings">Xem Đơn của tôi</a></p>
  </section></Shell>;
}
export function BookingDetail({ id }: { id: string }) {
  const { session, request } = useRequiredSession(); const now = useNow();
  const [storedBooking, setBooking] = useState<Booking | null>(null); const [error, setError] = useState('');
  const accessEpoch = useRef(0);
  const commandEpoch = accessEpoch.current;
  const booking = session && storedBooking?.bookingId === id ? storedBooking : null;
  useEffect(() => { accessEpoch.current++; setBooking(null); setError(''); }, [id, Boolean(session)]);
  const receive = (value: Booking) => {
    if (value.bookingId !== id) {
      if (value.series?.occurrences.some(occurrence => occurrence.bookingId === id)) navigate(`/bookings/${value.bookingId}`, true);
      return;
    }
    setBooking(previous => previous?.bookingId === value.bookingId && previous.version > value.version ? previous : value);
    setError('');
  };
  const denied = () => { accessEpoch.current++; setBooking(null); };
  const refresh = useVisiblePolling(Boolean(session), id,
    async signal => { const epoch = accessEpoch.current; const value = await request(`/api/v1/bookings/${id}`, { signal }).then(data<Booking>); return { value, epoch }; },
    result => { if (result.epoch === accessEpoch.current) receive(result.value); },
    reason => { if (accessDenied(reason)) denied(); setError(failure(reason)); });
  return <Shell><section className="booking-panel"><h1>Chi tiết đơn đặt sân</h1>{error && <p role="alert">{error}</p>}
    {!booking && !error && <p role="status">Đang tải đơn…</p>}{booking && <><h2>Mã đơn: {booking.series?.seriesNo ?? booking.bookingNo}</h2>
      <BookingFacts booking={booking} />
      <p role="status"><BookingStatus status={booking.status} /></p>
      {booking.series ? <SeriesSummary booking={booking} /> : <PriceBreakdown slots={booking.slots} />}
      <p className="booking-total">{booking.series ? 'Số tiền cả kỳ' : 'Số tiền'}: {money(booking.amountExact ?? booking.payment.expectedAmountExact ?? booking.amount)}</p>
      {booking.status === 'AWAITING_TRANSFER' && <><p>Hạn chuyển khoản: {dateTime(booking.paymentDeadline, booking.timezone)}</p>
        <p>Thời gian giữ chỗ còn: <strong>{remaining(booking.paymentDeadline, now)}</strong></p>
        {booking.payment.qrUrl && <PrivateImage path={booking.payment.qrUrl} className="booking-qr" alt="QR nhận tiền của cơ sở cho đơn đặt sân" />}
        <p>Ngân hàng: {booking.payment.bankCode} · Chủ tài khoản: {booking.payment.accountName}</p>
        <p>Số tài khoản: {booking.payment.maskedAccountNumber}</p><p>Nội dung chuyển khoản: <strong>{booking.payment.transferContent}</strong></p>
        {now >= Date.parse(booking.paymentDeadline) && <p role="status">Đã qua hạn chuyển khoản. Đang cập nhật trạng thái từ máy chủ.</p>}</>}
      {['AWAITING_OWNER_CONFIRMATION', 'NEEDS_REVIEW'].includes(booking.status) && <div className="payment-notice">
        <p>Sân vẫn được giữ trong lúc chủ sân đối chiếu giao dịch.</p>
        {booking.series && <p>Tất cả các buổi trong kỳ vẫn được giữ; bạn không cần chuyển thêm tiền cho từng buổi.</p>}
        {booking.payment.firstReportedAt && <p>Đã báo chuyển: {dateTime(booking.payment.firstReportedAt, booking.timezone)}</p>}
        {booking.isOverdue && <p>Đơn đang chờ đối chiếu quá 30 phút. Bạn có thể liên hệ trực tiếp với cơ sở nếu cần.</p>}
      </div>}
      {booking.status === 'CONFIRMED' && <div className="payment-notice"><p>Đặt sân thành công. Lịch sân của bạn đã được xác nhận.</p>
        {booking.payment.confirmedAt && <p>Xác nhận lúc: {dateTime(booking.payment.confirmedAt, booking.timezone)}</p>}
        {(booking.payment.confirmedAmountExact ?? booking.payment.confirmedAmount) != null && <p>Số tiền đã nhận: {money(booking.payment.confirmedAmountExact ?? booking.payment.confirmedAmount!)}</p>}</div>}
      {booking.status === 'EXPIRED' && <p>{booking.series ? 'Toàn bộ các buổi trong kỳ đã được giải phóng do chưa báo chuyển trong thời hạn giữ chỗ.' : 'Khung giờ đã được giải phóng do chưa báo chuyển trong thời hạn giữ chỗ.'}</p>}
      {booking.status === 'PAYMENT_REJECTED' && <p>{booking.series ? 'Chủ sân không xác nhận giao dịch. Toàn bộ các buổi đã được giải phóng; vui lòng liên hệ cơ sở để đối chiếu nếu cần.' : 'Chủ sân không xác nhận giao dịch. Khung giờ đã được giải phóng; vui lòng liên hệ cơ sở để đối chiếu nếu cần.'}</p>}
      {['AWAITING_TRANSFER', 'NEEDS_REVIEW'].includes(booking.status) && <BookingTransfer key={`${booking.bookingId}-${booking.status}`} booking={booking} now={now}
        onAccessDenied={reason => { denied(); setError(failure(reason)); }}
        onBooking={value => { if (commandEpoch === accessEpoch.current) { receive(value); refresh(); } }} />}
      <BookingHistory booking={booking} />
      <p>Đơn được tạo: {dateTime(booking.createdAt, booking.timezone)}</p>
      {booking.expiredAt && <p>Hết hạn: {dateTime(booking.expiredAt, booking.timezone)}</p>}
    </>}<button type="button" onClick={refresh}>Làm mới đơn</button><p><a href="/me/bookings">Đơn của tôi</a></p>
  </section></Shell>;
}
export function MyBookings() {
  const { session, request } = useRequiredSession(); const [items, setItems] = useState<Booking[]>([]); const [error, setError] = useState('');
  const [cursor, setCursor] = useState<string | null>(null); const [pageCursor, setPageCursor] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  useEffect(() => { if (!session) { setItems([]); setCursor(null); setPageCursor(null); } }, [Boolean(session)]);
  const refresh = useVisiblePolling(Boolean(session), pageCursor ?? 'my-bookings', signal =>
    request(`/api/v1/me/bookings${pageCursor ? `?before=${pageCursor}` : ''}`, { signal }).then(data<{ items: Booking[]; nextCursor: string | null }>), result => {
      setItems(previous => pageCursor ? [...previous.filter(item => !result.items.some(value => value.bookingId === item.bookingId)), ...result.items] : result.items);
      setCursor(result.nextCursor); setError(''); setLoading(false);
    }, reason => { if (accessDenied(reason)) { setItems([]); setCursor(null); } setError(failure(reason)); setLoading(false); });
  return <Shell><section className="booking-panel"><h1>Đơn của tôi</h1>{error && <p role="alert">{error}</p>}
    {loading && <p role="status">Đang tải đơn…</p>}{!loading && !items.length && !error && <p>Chưa có đơn đặt sân.</p>}<ul className="booking-list">{items.map(item => <li key={item.bookingId}><a href={`/bookings/${item.bookingId}`}>{item.series?.seriesNo ?? item.bookingNo}</a>
      <p>{item.venueName} · {item.courtName} · {item.series ? `${item.series.startsOn} → ${item.series.endsOn} · ${item.series.occurrenceCount} buổi cố định` : `${item.date} ${item.localStart}–${item.localEnd}`}</p>
      {item.series && <p>{weekdayLabel(item.series.dayOfWeek)} hằng tuần · {item.localStart}–{item.localEnd}</p>}
      <p>{item.series ? 'Tổng cả kỳ: ' : ''}{money(item.amountExact ?? item.payment?.expectedAmountExact ?? item.amount)} · <BookingStatus status={item.status} /></p></li>)}</ul>
    <button type="button" onClick={() => { if (pageCursor) { setPageCursor(null); setLoading(true); } else refresh(); }}>Làm mới danh sách</button>
    {cursor && <button type="button" onClick={() => { setPageCursor(cursor); setLoading(true); }}>Xem thêm đơn</button>}</section></Shell>;
}
