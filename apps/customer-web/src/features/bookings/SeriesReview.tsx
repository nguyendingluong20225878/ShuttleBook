import { useEffect, useRef, useState, type FormEvent } from 'react';
import { CustomerShell } from '../../components/CustomerShell';
import { navigate } from '../../routes/navigation';
import { useCustomerSession } from '../auth/CustomerSession';
import { getPublic } from '../venues/api';
import type { CourtSummary, VenueDetail } from '../venues/types';
import { accessDenied, BookingApiError, data, failure, money, type Booking } from './bookingApi';
import { PriceBreakdown } from './BookingFacts';
import { addDays, calendarMonthAfter, endTime, isDate, timeMinutes, validateSeries, weekdayFromDate, weekdayLabel,
  weekdays, zoneToday, type SeriesForm, type SeriesQuote } from './seriesApi';
import './bookings.css';

const newQuoteCodes = ['QUOTE_CONSUMED', 'QUOTE_EXPIRED', 'QUOTE_CHANGED', 'SERIES_CONFLICT', 'SLOT_UNAVAILABLE', 'IDEMPOTENCY_KEY_REUSED', 'SCHEDULE_UNAVAILABLE', 'PRICE_UNAVAILABLE', 'PAYMENT_SETUP_UNAVAILABLE', 'VALIDATION_FAILED'];

export function SeriesReview() {
  const { session, request } = useCustomerSession();
  const params = new URLSearchParams(location.search);
  const venueId = params.get('venueId') ?? ''; const courtId = params.get('courtId') ?? '';
  const sourceDate = isDate(params.get('date') ?? '') ? params.get('date')! : '';
  const start = params.get('startsAt') ?? '18:00';
  const initialDuration = timeMinutes(params.get('endsAt') ?? '20:00') - timeMinutes(start);
  const [venue, setVenue] = useState<VenueDetail | null>(null); const [court, setCourt] = useState<CourtSummary | null>(null);
  const [form, setForm] = useState<SeriesForm>(() => ({ courtId, dayOfWeek: weekdayFromDate(sourceDate), localStartTime: start,
    durationMinutes: Number.isFinite(initialDuration) ? Math.max(120, initialDuration) : 120,
    startsOn: sourceDate, endsOn: calendarMonthAfter(sourceDate) }));
  const [quote, setQuote] = useState<SeriesQuote | null>(null); const [error, setError] = useState('');
  const [conflictDates, setConflictDates] = useState<string[]>([]); const [loadingVenue, setLoadingVenue] = useState(true);
  const [loading, setLoading] = useState(false); const [submitting, setSubmitting] = useState(false);
  const [now, setNow] = useState(Date.now()); const [retried, setRetried] = useState(false);
  const feedback = useRef<HTMLDivElement>(null); const quoteController = useRef<AbortController | null>(null);
  const createController = useRef<AbortController | null>(null); const busy = useRef(false);
  const intent = useRef<{ quoteId: string; key: string; attempted: boolean } | null>(null);
  const minimum = Math.max(120, court?.minimumBookingMinutes ?? 120);
  const today = venue ? zoneToday(venue.timezone) : '';
  const expired = Boolean(quote?.expiresAt && now >= Date.parse(quote.expiresAt));
  const canRetry = Boolean(intent.current?.attempted && quote?.quoteId === intent.current.quoteId);
  const backLink = `/venues/${encodeURIComponent(venueId)}?${new URLSearchParams({ date: form.startsOn || sourceDate, mode: 'fixed' })}`;

  useEffect(() => {
    if (!session) navigate(`/login?returnTo=${encodeURIComponent(location.pathname + location.search)}`, true);
  }, [Boolean(session)]);
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 1000);
    return () => { clearInterval(timer); quoteController.current?.abort(); createController.current?.abort(); };
  }, []);
  useEffect(() => { if ((error || conflictDates.length) && !loading && !submitting) feedback.current?.focus(); }, [error, conflictDates.length, loading, submitting]);
  useEffect(() => {
    if (!session) return;
    const controller = new AbortController(); setLoadingVenue(true);
    if (!venueId || !courtId) { setError('Hãy chọn cơ sở, sân và khung giờ từ bảng lịch trước.'); setLoadingVenue(false); return; }
    void getPublic<VenueDetail>(`/venues/${encodeURIComponent(venueId)}`, controller.signal)
      .then(value => {
        if (controller.signal.aborted) return;
        const selected = value.courts.find(item => item.id === courtId);
        if (!selected) throw new Error('NOT_FOUND');
        setVenue(value); setCourt(selected);
        setForm(previous => {
          const begins = previous.startsOn || zoneToday(value.timezone);
          return { ...previous, startsOn: begins, endsOn: previous.endsOn || calendarMonthAfter(begins),
            dayOfWeek: previous.startsOn ? previous.dayOfWeek : weekdayFromDate(begins),
            durationMinutes: Math.max(previous.durationMinutes, 120, selected.minimumBookingMinutes) };
        });
      }).catch(reason => { if (!controller.signal.aborted) setError(failure(reason)); })
      .finally(() => { if (!controller.signal.aborted) setLoadingVenue(false); });
    return () => controller.abort();
  }, [Boolean(session), venueId, courtId]);

  function change(update: Partial<SeriesForm>) {
    quoteController.current?.abort(); setLoading(false);
    setForm(previous => ({ ...previous, ...update })); setQuote(null); setError(''); setConflictDates([]); intent.current = null; setRetried(false);
  }
  async function getQuote(event: FormEvent) {
    event.preventDefault();
    if (busy.current || !venue || !court) return;
    const validation = validateSeries(form, minimum, today);
    if (validation) { setError(validation); setQuote(null); return; }
    const controller = new AbortController(); quoteController.current?.abort(); quoteController.current = controller;
    busy.current = true; setLoading(true); setError(''); setConflictDates([]); setQuote(null); intent.current = null; setRetried(false);
    try {
      const value = await request('/api/v1/booking-series/quote', { method: 'POST', signal: controller.signal,
        headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(form) }).then(data<SeriesQuote>);
      if (controller.signal.aborted) return;
      setQuote(value);
      if (value.canCreate && value.quoteId) intent.current = { quoteId: value.quoteId, key: crypto.randomUUID(), attempted: false };
      else setConflictDates([...new Set(value.conflicts.map(conflict => conflict.date))]);
    } catch (reason) {
      if (!controller.signal.aborted) {
        if (accessDenied(reason) && reason instanceof Error && reason.message === 'SESSION_REQUIRED') navigate(`/login?returnTo=${encodeURIComponent(location.pathname + location.search)}`, true);
        setError(failure(reason));
        if (reason instanceof BookingApiError) setConflictDates(reason.problem?.conflictDates ?? reason.problem?.conflicts?.map(conflict => conflict.date) ?? []);
      }
    } finally { busy.current = false; if (!controller.signal.aborted) setLoading(false); }
  }
  async function create() {
    if (busy.current || !quote?.canCreate || !quote.quoteId || !intent.current || (expired && !canRetry)) return;
    busy.current = true; intent.current.attempted = true; setSubmitting(true); setError(''); setRetried(true);
    const controller = new AbortController(); createController.current = controller;
    try {
      const value = await request('/api/v1/booking-series', { method: 'POST', signal: controller.signal,
        headers: { 'Content-Type': 'application/json', 'Idempotency-Key': intent.current.key },
        body: JSON.stringify({ quoteId: intent.current.quoteId }) }).then(data<Booking>);
      if (!controller.signal.aborted) navigate(`/bookings/${value.bookingId}`, true);
    } catch (reason) {
      if (!controller.signal.aborted) {
        setError(failure(reason));
        if (reason instanceof BookingApiError) setConflictDates(reason.problem?.conflictDates ?? reason.problem?.conflicts?.map(conflict => conflict.date) ?? []);
        if (accessDenied(reason) || reason instanceof Error && newQuoteCodes.includes(reason.message)) { setQuote(null); intent.current = null; setRetried(false); }
      }
    } finally { busy.current = false; if (!controller.signal.aborted) setSubmitting(false); }
  }
  if (!session) return null;
  const seconds = quote?.expiresAt ? Math.max(0, Math.ceil((Date.parse(quote.expiresAt) - now) / 1000)) : 0;
  return <CustomerShell><section className="booking-panel series-review-panel"><p className="eyebrow">Đặt lịch hàng tuần</p>
    <h1>Thiết lập lịch cố định</h1><a href={venueId ? backLink : '/venues'}>Quay lại lịch các sân</a>
    {loadingVenue && <p role="status">Đang tải thông tin sân…</p>}
    {venue && court && <><h2>{venue.name} · {court.name}</h2><p>Múi giờ: {venue.timezone}. Chọn một thứ và cùng khung giờ cho cả kỳ.</p>
      <form className="series-form" onSubmit={event => { void getQuote(event); }} noValidate aria-busy={loading || submitting}>
        <fieldset disabled={loading || submitting}><legend>Khung giờ và kỳ đặt</legend>
          <label>Ngày trong tuần<select value={form.dayOfWeek} onChange={event => change({ dayOfWeek: event.target.value })}>{weekdays.map(day => <option key={day.value} value={day.value}>{day.label}</option>)}</select></label>
          <label>Giờ bắt đầu<input type="time" step={1800} value={form.localStartTime} onChange={event => change({ localStartTime: event.target.value })} /></label>
          <label>Thời lượng mỗi buổi (phút)<input type="number" inputMode="numeric" min={minimum} step={30} max={1440} value={form.durationMinutes} onChange={event => change({ durationMinutes: Number(event.target.value) })} /></label>
          <label>Ngày bắt đầu kỳ<input type="date" min={today} max={addDays(today, 60)} value={form.startsOn} onChange={event => change({ startsOn: event.target.value })} /></label>
          <label>Ngày kết thúc kỳ<input type="date" min={calendarMonthAfter(form.startsOn)} max={addDays(today, 60)} value={form.endsOn} onChange={event => change({ endsOn: event.target.value })} /></label>
        </fieldset>
        <p className="form-note">{weekdayLabel(form.dayOfWeek)} hằng tuần · {form.localStartTime}–{endTime(form.localStartTime, form.durationMinutes)}. Mỗi buổi tối thiểu {minimum} phút, kỳ ít nhất một tháng lịch; tối đa 12 buổi trong 60 ngày đặt trước.</p>
        <button type="submit" disabled={loading || submitting}>{loading ? 'Đang kiểm tra toàn kỳ…' : quote ? 'Lấy báo giá mới cho kỳ' : 'Xem báo giá toàn kỳ'}</button>
      </form></>}
    {(error || conflictDates.length > 0) && <div className="series-feedback" role="alert" ref={feedback} tabIndex={-1}>
      {error && <p>{error}</p>}{conflictDates.length > 0 && <><strong>Các ngày xung đột</strong><ul>{[...new Set(conflictDates)].map(date => <li key={date}>{date}</li>)}</ul>
        <p>Chưa tạo lịch hoặc giữ chỗ. Hãy đổi kỳ, thứ hoặc khung giờ rồi kiểm tra lại; không tự bỏ buổi bị trùng.</p></>}
    </div>}
    {quote && <section className="series-quote" aria-label="Báo giá toàn kỳ"><h2>{quote.occurrenceCount} buổi · {quote.startsOn} → {quote.endsOn}</h2>
      <p>{weekdayLabel(quote.dayOfWeek)} · {quote.localStartTime} · {quote.durationMinutes} phút/buổi</p>
      <ol className="series-quote-list">{quote.occurrences.map(occurrence => <li key={occurrence.date}><div className="series-quote-row"><strong>{occurrence.date}</strong>
        <span>{occurrence.localStart}–{occurrence.localEnd}</span><strong>{money(occurrence.amountExact ?? occurrence.amount)}</strong></div>
        <PriceBreakdown slots={occurrence.slots} /></li>)}</ol>
      <p className="booking-total">Tổng tiền cả kỳ: {money(quote.amountExact ?? quote.amount)}</p>
      <p>Thanh toán 100% toàn kỳ bằng một QR. Chủ sân xác nhận một lần cho tất cả các buổi.</p>
      {quote.canCreate && quote.quoteId && <><div className="quote-hold-notice"><p>{expired ? 'Đã hết thời gian giữ chỗ tạm' : 'Đang giữ chỗ tạm toàn kỳ cho bạn'}: <strong>{Math.floor(seconds / 60)}:{String(seconds % 60).padStart(2, '0')}</strong></p>
          <p>Toàn bộ các buổi trong báo giá được khóa đến hết thời gian trên. Nếu bạn chưa tạo lịch, các buổi sẽ tự trở về trống.</p></div>
        <p>Tạo lịch thành công giữ toàn bộ các buổi {quote.holdMinutes} phút để bạn chuyển khoản.</p>
        <button type="button" className="primary-action" disabled={submitting || (expired && !canRetry)} onClick={() => { void create(); }}>{submitting ? 'Đang tạo lịch…' : retried ? 'Thử lại tạo lịch cùng yêu cầu' : 'Xác nhận tạo lịch cố định'}</button>
        {expired && <p role="status">{canRetry ? 'Báo giá đã hết hạn. Bạn có thể thử lại đúng yêu cầu đã gửi hoặc kiểm tra Đơn của tôi.' : 'Báo giá đã hết hạn và chỗ tạm đã được giải phóng. Hãy lấy và xác nhận báo giá mới.'}</p>}</>}
      {!quote.canCreate && <p className="series-conflict-note">Toàn kỳ chưa thể đặt do có ngày xung đột.</p>}
    </section>}
    <p><a href="/me/bookings">Xem Đơn của tôi</a></p>
  </section></CustomerShell>;
}
