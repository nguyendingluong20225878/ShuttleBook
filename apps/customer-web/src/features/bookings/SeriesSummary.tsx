import { BookingStatus } from './BookingStatus';
import { money, type Booking } from './bookingApi';
import { weekdayLabel } from './seriesApi';

export function SeriesSummary({ booking }: { booking: Booking }) {
  const series = booking.series;
  if (!series) return null;
  return <section className="series-summary" aria-label="Lịch các buổi trong kỳ">
    <div className="series-summary-heading"><h3>Lịch cố định · {series.occurrenceCount} buổi</h3><span className="series-label">Thanh toán toàn kỳ</span></div>
    <p>{weekdayLabel(series.dayOfWeek)} hằng tuần · {series.localStartTime} · {series.durationMinutes} phút/buổi</p>
    <p>Một lần chuyển khoản cho toàn bộ các buổi. Chủ sân đối chiếu và xác nhận cả kỳ.</p>
    <ol className="series-occurrence-list">{series.occurrences.map(occurrence => <li key={occurrence.bookingId}>
      <div><span className="series-field-label">Ngày chơi</span><strong>{occurrence.date}</strong></div>
      <div><span className="series-field-label">Khung giờ</span><span>{occurrence.localStart}–{occurrence.localEnd}</span></div>
      <div><span className="series-field-label">Giá buổi</span><strong>{money(occurrence.amountExact ?? occurrence.amount)}</strong></div>
      <BookingStatus status={occurrence.status} />
    </li>)}</ol>
  </section>;
}
