import { useEffect, useRef, useState, type ReactNode } from 'react';
import { CustomerNav, useCustomerSession } from '../auth/CustomerSession';
import { navigate } from '../../routes/navigation';
import { accessDenied, data, dateTime, failure, money, type Booking, type Slot } from './bookingApi';
import { BookingHistory } from './BookingHistory';
import { BookingStatus } from './BookingStatus';
import { PrivateImage } from './PrivateImage';
import { useVisiblePolling } from '../../hooks/useVisiblePolling';
import { BookingTransfer } from './BookingTransfer';

const base = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';
type Quote = { quoteId: string; expiresAt: string; courtId: string; venueId: string; courtName: string; venueName: string;
  date: string; timezone: string; startsAt: string; endsAt: string; amount: number; slots: Slot[]; holdMinutes: number };
function useNow() { const [now, setNow] = useState(Date.now()); useEffect(() => { const timer = setInterval(() => setNow(Date.now()), 1000); return () => clearInterval(timer); }, []); return now; }
function remaining(deadline: string, now: number) { const seconds = Math.max(0, Math.ceil((Date.parse(deadline) - now) / 1000)); return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`; }
function Shell({ children }: { children: ReactNode }) { return <main className="customer-shell"><header className="site-header"><a className="brand" href="/venues">ShuttleBook</a><CustomerNav /></header>{children}</main>; }
function useRequiredSession() {
  const auth = useCustomerSession();
  useEffect(() => { if (!auth.session && location.pathname !== '/login') navigate(`/login?returnTo=${encodeURIComponent(location.pathname + location.search)}`, true); }, [auth.session]);
  return auth;
}
export function BookingReview() {
  const { session, request } = useRequiredSession(); const now = useNow();
  const [quote, setQuote] = useState<Quote | null>(null); const [error, setError] = useState('');
  const [loading, setLoading] = useState(false); const [submitting, setSubmitting] = useState(false); const [retry, setRetry] = useState(0);
  const intent = useRef<{ quoteId: string; key: string } | null>(null); const creating = useRef(false);
  const params = new URLSearchParams(location.search);
  const courtId = params.get('courtId'); const date = params.get('date'); const startsAt = params.get('startsAt'); const endsAt = params.get('endsAt');
  const venueId = params.get('venueId');
  useEffect(() => {
    if (!session) return;
    const controller = new AbortController(); setLoading(true); setError(''); setQuote(null);
    void fetch(`${base}/api/v1/availability/quote`, { method: 'POST', signal: controller.signal,
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ courtId, date, startsAt, endsAt }) })
      .then(data<Quote>).then(value => { if (!controller.signal.aborted) { setQuote(value); intent.current = { quoteId: value.quoteId, key: crypto.randomUUID() }; } })
      .catch(reason => { if (!controller.signal.aborted) setError(failure(reason)); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [Boolean(session), courtId, date, startsAt, endsAt, retry]);
  async function create() {
    if (!quote || creating.current || Date.parse(quote.expiresAt) <= Date.now()) return;
    creating.current = true; setSubmitting(true); setError('');
    try {
      const response = await request('/api/v1/bookings', { method: 'POST', headers: { 'Content-Type': 'application/json',
        'Idempotency-Key': intent.current!.key }, body: JSON.stringify({ courtId: quote.courtId, quoteId: quote.quoteId,
        startsAt: quote.startsAt, endsAt: quote.endsAt }) });
      const result = await data<Booking>(response); navigate(`/bookings/${result.bookingId}`, true);
    } catch (reason) {
      setError(failure(reason));
      if (reason instanceof Error && ['QUOTE_EXPIRED', 'QUOTE_CHANGED', 'SLOT_UNAVAILABLE', 'IDEMPOTENCY_KEY_REUSED'].includes(reason.message)) setQuote(null);
    } finally { creating.current = false; setSubmitting(false); }
  }
  return <Shell><section className="booking-panel"><h1>Xác nhận đặt vãng lai</h1><a href={`/venues/${venueId}?date=${date}`}>Quay lại lịch các sân</a>
    {loading && <p role="status">Đang lấy báo giá…</p>}{error && <p role="alert">{error}</p>}
    {quote && <><h2>{quote.venueName} · {quote.courtName}</h2><p>Ngày {quote.date} · Múi giờ {quote.timezone}</p>
      <ul>{quote.slots.map(slot => <li key={slot.startsAt}>{slot.startsAt}–{slot.endsAt}: {money(slot.pricePerSlot)}</li>)}</ul>
      <p className="booking-total">Tổng tiền: {money(quote.amount)}</p><p>Báo giá còn: <strong>{remaining(quote.expiresAt, now)}</strong></p>
      <p>Sau khi tạo đơn, sân được giữ {quote.holdMinutes} phút để bạn chuyển khoản. Báo giá hiện tại chưa giữ chỗ.</p>
      <button type="button" onClick={() => { void create(); }} disabled={submitting || now >= Date.parse(quote.expiresAt)}>{submitting ? 'Đang tạo đơn…' : 'Xác nhận tạo đơn'}</button>
      {now >= Date.parse(quote.expiresAt) && <p role="status">Báo giá đã hết hạn.</p>}</>}
    <button type="button" disabled={loading || submitting} onClick={() => setRetry(value => value + 1)}>Lấy báo giá mới</button>
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
    if (value.bookingId !== id) return;
    setBooking(previous => previous?.bookingId === value.bookingId && previous.version > value.version ? previous : value);
    setError('');
  };
  const denied = () => { accessEpoch.current++; setBooking(null); };
  const refresh = useVisiblePolling(Boolean(session), id,
    async signal => { const epoch = accessEpoch.current; const value = await request(`/api/v1/bookings/${id}`, { signal }).then(data<Booking>); return { value, epoch }; },
    result => { if (result.epoch === accessEpoch.current) receive(result.value); },
    reason => { if (accessDenied(reason)) denied(); setError(failure(reason)); });
  return <Shell><section className="booking-panel"><h1>Chi tiết đơn đặt sân</h1>{error && <p role="alert">{error}</p>}
    {!booking && !error && <p role="status">Đang tải đơn…</p>}{booking && <><h2>Mã đơn: {booking.bookingNo}</h2>
      <p>{booking.venueName} · {booking.courtName} · {booking.date} {booking.localStart}–{booking.localEnd}</p><p>Múi giờ: {booking.timezone}</p>
      <p role="status"><BookingStatus status={booking.status} /></p>
      <ul>{booking.slots.map(slot => <li key={slot.startsAt}>{slot.startsAt}–{slot.endsAt}: {money(slot.pricePerSlot)}</li>)}</ul>
      <p className="booking-total">Số tiền: {money(booking.amountExact ?? booking.payment.expectedAmountExact ?? booking.amount)}</p>
      {booking.status === 'AWAITING_TRANSFER' && <><p>Hạn chuyển khoản: {dateTime(booking.paymentDeadline, booking.timezone)}</p>
        <p>Thời gian giữ chỗ còn: <strong>{remaining(booking.paymentDeadline, now)}</strong></p>
        {booking.payment.qrUrl && <PrivateImage path={booking.payment.qrUrl} className="booking-qr" alt="QR nhận tiền của cơ sở cho đơn đặt sân" />}
        <p>Ngân hàng: {booking.payment.bankCode} · Chủ tài khoản: {booking.payment.accountName}</p>
        <p>Số tài khoản: {booking.payment.maskedAccountNumber}</p><p>Nội dung chuyển khoản: <strong>{booking.payment.transferContent}</strong></p>
        {now >= Date.parse(booking.paymentDeadline) && <p role="status">Đã qua hạn chuyển khoản. Đang cập nhật trạng thái từ máy chủ.</p>}</>}
      {['AWAITING_OWNER_CONFIRMATION', 'NEEDS_REVIEW'].includes(booking.status) && <div className="payment-notice">
        <p>Sân vẫn được giữ trong lúc chủ sân đối chiếu giao dịch.</p>
        {booking.payment.firstReportedAt && <p>Đã báo chuyển: {dateTime(booking.payment.firstReportedAt, booking.timezone)}</p>}
        {booking.isOverdue && <p>Đơn đang chờ đối chiếu quá 30 phút. Bạn có thể liên hệ trực tiếp với cơ sở nếu cần.</p>}
      </div>}
      {booking.status === 'CONFIRMED' && <div className="payment-notice"><p>Đặt sân thành công. Lịch sân của bạn đã được xác nhận.</p>
        {booking.payment.confirmedAt && <p>Xác nhận lúc: {dateTime(booking.payment.confirmedAt, booking.timezone)}</p>}
        {(booking.payment.confirmedAmountExact ?? booking.payment.confirmedAmount) != null && <p>Số tiền đã nhận: {money(booking.payment.confirmedAmountExact ?? booking.payment.confirmedAmount!)}</p>}</div>}
      {booking.status === 'EXPIRED' && <p>Khung giờ đã được giải phóng do chưa báo chuyển trong thời hạn giữ chỗ.</p>}
      {booking.status === 'PAYMENT_REJECTED' && <p>Chủ sân không xác nhận giao dịch. Khung giờ đã được giải phóng; vui lòng liên hệ cơ sở để đối chiếu nếu cần.</p>}
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
    {loading && <p role="status">Đang tải đơn…</p>}{!loading && !items.length && !error && <p>Chưa có đơn đặt sân.</p>}<ul className="booking-list">{items.map(item => <li key={item.bookingId}><a href={`/bookings/${item.bookingId}`}>{item.bookingNo}</a>
      <p>{item.venueName} · {item.courtName} · {item.date} {item.localStart}–{item.localEnd}</p><p>{money(item.amountExact ?? item.payment?.expectedAmountExact ?? item.amount)} · <BookingStatus status={item.status} /></p></li>)}</ul>
    <button type="button" onClick={() => { if (pageCursor) { setPageCursor(null); setLoading(true); } else refresh(); }}>Làm mới danh sách</button>
    {cursor && <button type="button" onClick={() => { setPageCursor(cursor); setLoading(true); }}>Xem thêm đơn</button>}</section></Shell>;
}
