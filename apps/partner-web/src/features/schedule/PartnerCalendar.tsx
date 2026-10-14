import { useEffect, useRef, useState } from 'react';
import { ScheduleVenue, venueToday } from '../../PartnerOperations';
import { PartnerApiError, PartnerRequest, bookingStatusLabel, money } from '../bookings/types';
import { statusLabel } from '../workspace/navigation';

type Event = { id: string; kind: string; startsAt: string; endsAt: string; blocksBooking: boolean; isPast: boolean;
  occurrenceId?: string; paymentAnchorId?: string; bookingNo?: string; seriesNo?: string; bookingType?: string;
  bookingStatus?: string; paymentStatus?: string; occurrenceAmount?: string; paymentTotalAmount?: string;
  reason?: string; maintenanceStatus?: string; dataWarning?: boolean };
type Slot = { startsAt: string; endsAt: string; startsAtUtc: string; endsAtUtc: string; isPast: boolean;
  occupancy: string; canRequestNewBooking: boolean; isOpen: boolean; pricePerSlotExact: string | null };
type CalendarCourt = { id: string; name: string; status: string; canRequestNewBooking: boolean;
  slots: Slot[]; events: Event[] };
type Calendar = { venueId: string; venueName: string; venueStatus: string; date: string; timezone: string;
  generatedAt: string; dayStartsAt: string; dayEndsAt: string; courts: CalendarCourt[] };
const paymentLabel = (status?: string) => ({ PAID: 'Đã thanh toán', PENDING: 'Chưa thanh toán', UNPAID: 'Chưa thanh toán',
  AWAITING_TRANSFER: 'Chờ chuyển khoản', EXPIRED: 'Hết hạn chuyển khoản',
  TRANSFER_REPORTED: 'Đã báo chuyển', REPORTED: 'Đã báo chuyển', NEEDS_REVIEW: 'Cần đối chiếu', REJECTED: 'Không xác nhận' }[status ?? ''] ?? status ?? '');
const overlap = (event: Event, slot: Slot) => Date.parse(event.startsAt) < Date.parse(slot.endsAtUtc) && Date.parse(event.endsAt) > Date.parse(slot.startsAtUtc);
function slotLabel(slot: Slot) {
  if (slot.occupancy === 'QUOTE_HOLD') return 'Giữ báo giá';
  if (slot.occupancy === 'MAINTENANCE') return 'Bảo trì';
  if (slot.occupancy === 'BOOKING') return 'Có đơn';
  if (slot.isPast) return 'Đã qua';
  if (!slot.canRequestNewBooking) return slot.isOpen && !slot.pricePerSlotExact ? 'Chưa có giá' : 'Không nhận đặt';
  return 'Trống';
}
function shiftedDate(date: string, days: number) {
  const value = new Date(`${date}T12:00:00Z`); value.setUTCDate(value.getUTCDate() + days); return value.toISOString().slice(0, 10);
}

export function PartnerCalendar({ venue, courtId, date, chooseDate, request, openBooking, revision }: {
  venue: ScheduleVenue; courtId: string; date: string; chooseDate: (date: string) => void;
  request: PartnerRequest; openBooking: (id: string) => void; revision: number;
}) {
  const [data, setData] = useState<Calendar | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [denied, setDenied] = useState(false);
  const [reload, setReload] = useState(0);
  const [wholeDay, setWholeDay] = useState(false);
  const requestRef = useRef(request); requestRef.current = request;
  const scopeRef = useRef('');
  useEffect(() => {
    let current = true; let pending = false; let stopped = false;
    const controller = new AbortController();
    const scope = `${venue.id}/${courtId}/${date}`;
    if (scopeRef.current !== scope) { setData(null); setError(''); }
    scopeRef.current = scope;
    setLoading(true); setDenied(false);
    const load = async () => {
      if (!current || pending || stopped || document.visibilityState === 'hidden') return;
      pending = true;
      try {
        const params = new URLSearchParams({ date }); if (courtId) params.set('courtId', courtId);
        const next = await requestRef.current<Calendar>(`/operator/venues/${venue.id}/calendar?${params}`, 'GET', undefined, undefined,
          { signal: controller.signal });
        if (current) { setData(next); setError(''); }
      } catch (failure) {
        if (!current || controller.signal.aborted) return;
        if (failure instanceof PartnerApiError && [401, 403, 404].includes(failure.status)) {
          stopped = true; setData(null); setDenied(true);
        }
        setError(failure instanceof PartnerApiError && failure.code === 'CALENDAR_TOO_LARGE'
          ? 'Lịch quá lớn để tải đầy đủ. Chọn một sân để xem; dữ liệu chưa tải không được coi là trống.'
          : failure instanceof Error ? failure.message : 'Không tải được lịch sân.');
      } finally { pending = false; if (current) setLoading(false); }
    };
    void load(); const timer = window.setInterval(() => void load(), 15000);
    const focus = () => void load();
    window.addEventListener('focus', focus); document.addEventListener('visibilitychange', focus);
    return () => { current = false; controller.abort(); window.clearInterval(timer);
      window.removeEventListener('focus', focus); document.removeEventListener('visibilitychange', focus); };
  }, [venue.id, courtId, date, revision, reload]);
  const courts = [...(data?.courts ?? [])].sort((a, b) => a.name.localeCompare(b.name, 'vi', { numeric: true }));
  const allColumns = courts[0]?.slots ?? [];
  // Start at useful hours, while retaining events outside the latest opening schedule.
  const relevant = allColumns.map((_, index) => index).filter(index => courts.some(court => {
    const slot = court.slots[index]; return slot && (slot.isOpen || court.events.some(event => overlap(event, slot)));
  }));
  const first = !wholeDay && relevant.length ? relevant[0] : 0;
  const last = !wholeDay && relevant.length ? relevant[relevant.length - 1] + 1 : allColumns.length;
  const columns = allColumns.slice(first, last);
  const time = (value: string) => new Intl.DateTimeFormat('vi-VN', { timeZone: data?.timezone ?? venue.timezone,
    hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(new Date(value));
  return <section aria-label="Lịch sân theo ngày" className="partner-calendar">
    <div className="calendar-toolbar"><div className="calendar-date-controls">
      <button type="button" aria-label="Ngày trước" onClick={() => chooseDate(shiftedDate(date, -1))}>‹</button>
      <label>Ngày xem lịch <input type="date" min="1000-01-01" max="9998-12-31" required value={date}
        onChange={event => { if (event.target.value) chooseDate(event.target.value); }} /></label>
      <button type="button" aria-label="Ngày sau" onClick={() => chooseDate(shiftedDate(date, 1))}>›</button>
      <button type="button" onClick={() => chooseDate(venueToday(venue.timezone))}>Hôm nay</button>
    </div><button type="button" disabled={loading} onClick={() => setReload(value => value + 1)}>Tải lại lịch</button></div>
    <p className="muted">Cập nhật khi đang xem, khoảng 15 giây/lần. Chọn đơn để mở đối chiếu thanh toán.</p>
    {loading && <p role="status">Đang tải lịch sân…</p>}
    {error && <p className="feedback error" role="alert">{data ? 'Chưa cập nhật. Đang hiển thị bản tải trước. ' : ''}{error}</p>}
    {denied && <p>Bạn không còn quyền xem lịch này. Dữ liệu của phiên trước đã được xóa.</p>}
    {data && <>
      <p className="calendar-updated">Cập nhật lúc {new Date(data.generatedAt).toLocaleString('vi-VN', { timeZone: data.timezone })}</p>
      <div className="calendar-legend" aria-label="Chú giải lịch">{[['free', 'Trống'], ['quote_hold', 'Giữ báo giá'],
        ['booking', 'Có đơn'], ['maintenance', 'Bảo trì'], ['closed', 'Không nhận đặt'], ['past', 'Đã qua']].map(([kind, label]) =>
        <span key={kind}><i className={`legend-${kind}`} />{label}</span>)}</div>
      <div className="calendar-window"><p className="muted">{columns.length ? `Hiển thị ${columns[0].startsAt}–${columns.at(-1)!.endsAt}. ` : ''}Các đơn ngoài giờ mở vẫn được hiển thị.</p>
        <button type="button" onClick={() => setWholeDay(value => !value)}>{wholeDay ? 'Giờ hoạt động' : 'Xem toàn ngày'}</button></div>
      {data.courts.length === 0 ? <div className="panel"><p>Chưa có sân trong cơ sở này.</p></div> : <>
        <div className="calendar-scroll" tabIndex={0} role="region" aria-label="Bảng lịch sân, cuộn ngang để xem các giờ">
          <table className="calendar-grid"><caption className="sr-only">Lịch {data.venueName} ngày {date}, mỗi cột 30 phút</caption>
            <thead><tr><th scope="col">Sân / Giờ</th>{columns.map((slot, index) => <th scope="col" key={index}>{slot.startsAt.slice(0, 5)}</th>)}</tr></thead>
            <tbody>{courts.map(court => <tr key={court.id}><th scope="row">{court.name}<small>{statusLabel(court.status)}</small></th>
              {court.slots.slice(first, last).map((slot, index) => {
                const event = court.events.find(event => event.blocksBooking && overlap(event, slot));
                const kind = slot.occupancy !== 'FREE' ? slot.occupancy.toLowerCase() : slot.isPast ? 'past' : slot.canRequestNewBooking ? 'free' : 'closed';
                return <td key={index} className={`calendar-cell calendar-${kind}`}>
                  {event?.paymentAnchorId ? <a href={`#/bookings/${event.paymentAnchorId}`} aria-label={`${event.bookingNo} · ${court.name} · ${slot.startsAt}`}
                    onClick={e => { e.preventDefault(); openBooking(event.paymentAnchorId!); }}>{slotLabel(slot)}</a>
                    : <span title={event?.reason ?? ''}>{slotLabel(slot)}</span>}
                </td>;
              })}</tr>)}</tbody>
          </table>
        </div>
        <div className="calendar-events"><h4>Lịch đặt và khoảng giữ sân</h4>{courts.map(court => <section className="panel" key={court.id} aria-label={`Chi tiết lịch ${court.name}`}>
          <h4>{court.name}</h4>{!court.events.length ? <p>Không có đơn, báo giá đang giữ hoặc bảo trì trong ngày.</p>
            : <ul className="calendar-event-list">{court.events.map(event => <li key={event.id} className={!event.blocksBooking ? 'calendar-history' : ''}>
              <div><strong>{time(event.startsAt)}–{time(event.endsAt)}</strong>{' '}
                <span className="status-badge">{event.kind === 'BOOKING' ? event.bookingType === 'CASUAL' ? 'Vãng lai' : 'Cố định'
                  : event.kind === 'QUOTE_HOLD' ? 'Giữ báo giá' : 'Bảo trì'}</span>
                {event.kind === 'BOOKING' && <><p>{bookingStatusLabel(event.bookingStatus ?? '')}{paymentLabel(event.paymentStatus) !== bookingStatusLabel(event.bookingStatus ?? '') && ` · ${paymentLabel(event.paymentStatus)}`}</p>
                  {event.occurrenceAmount && <p>Tiền buổi: {money(event.occurrenceAmount)}{event.seriesNo && event.paymentTotalAmount && ` · Tổng kỳ: ${money(event.paymentTotalAmount)}`}</p>}</>}
                {event.reason && <p>{event.reason}</p>}{!event.blocksBooking && <small>Lịch sử · Đã giải phóng chỗ</small>}
                {event.isPast && <small> · Đã qua</small>}{event.dataWarning && <p className="feedback error">Trạng thái đơn và khoảng giữ sân chưa khớp. Khoảng này vẫn giữ chỗ; tải lại và kiểm tra đơn trước khi xử lý.</p>}
              </div>{event.paymentAnchorId && <a className="button secondary" href={`#/bookings/${event.paymentAnchorId}`}
                aria-label={`Xem đơn ${event.seriesNo ?? event.bookingNo}`} title={event.seriesNo ?? event.bookingNo}
                onClick={e => { e.preventDefault(); openBooking(event.paymentAnchorId!); }}>Xem đơn</a>}
            </li>)}</ul>}
        </section>)}</div>
      </>}
    </>}
  </section>;
}
