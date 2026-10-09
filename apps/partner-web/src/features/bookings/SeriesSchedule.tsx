import { bookingStatusLabel, money, type BookingSeriesInfo } from './types';

const weekdays: Record<string, string> = { MONDAY: 'Thứ Hai', TUESDAY: 'Thứ Ba', WEDNESDAY: 'Thứ Tư',
  THURSDAY: 'Thứ Năm', FRIDAY: 'Thứ Sáu', SATURDAY: 'Thứ Bảy', SUNDAY: 'Chủ nhật' };

export function seriesPeriod(series: BookingSeriesInfo) {
  return `${series.startsOn} – ${series.endsOn} · ${series.occurrenceCount} buổi`;
}

export function SeriesSchedule({ series, timezone }: { series: BookingSeriesInfo; timezone: string }) {
  return <section className="series-schedule" aria-label="Lịch cố định toàn kỳ">
    <div className="series-schedule-heading"><div><p className="eyebrow">Lịch cố định · {series.occurrenceCount} buổi</p>
      <h4>{weekdays[series.dayOfWeek] ?? series.dayOfWeek} hàng tuần · {series.localStartTime.slice(0, 5)} · {series.durationMinutes} phút/buổi</h4>
      <p>{seriesPeriod(series)} · {timezone}</p></div>
      <span className="status-badge">Thanh toán 100% cả kỳ</span></div>
    {series.occurrences && <div className="series-table-scroll" tabIndex={0} role="region" aria-label="Danh sách buổi, cuộn ngang khi cần">
      <table><caption>Giá đã chốt từng buổi; mọi buổi dùng chung một quyết định thanh toán.</caption>
        <thead><tr><th scope="col">Ngày chơi</th><th scope="col">Giờ chơi</th><th scope="col">Tiền buổi</th><th scope="col">Trạng thái</th></tr></thead>
        <tbody>{series.occurrences.map(occurrence => <tr key={occurrence.bookingId}>
          <th scope="row">{occurrence.date}</th><td>{occurrence.localStart.slice(0, 5)}–{occurrence.localEnd.slice(0, 5)}</td>
          <td>{money(occurrence.amountExact ?? occurrence.amount)}</td><td>{bookingStatusLabel(occurrence.status)}</td>
        </tr>)}</tbody></table></div>}
  </section>;
}
