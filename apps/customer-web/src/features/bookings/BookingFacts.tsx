import { money, type Booking, type Slot } from './bookingApi';

export function BookingFacts({ booking }: { booking: Booking }) {
  return <dl className="customer-booking-facts">
    <div><dt>Cơ sở / sân</dt><dd>{booking.venueName}<small>{booking.courtName}</small></dd></div>
    <div><dt>{booking.series ? 'Kỳ đặt cố định' : 'Ngày chơi'}</dt><dd>{booking.series ? `${booking.series.startsOn} → ${booking.series.endsOn}` : booking.date}
      <small>{booking.localStart}–{booking.localEnd}{booking.series ? ` · ${booking.series.occurrenceCount} buổi` : ''}</small></dd></div>
    <div><dt>Múi giờ</dt><dd>{booking.timezone}</dd></div>
  </dl>;
}

export function PriceBreakdown({ slots }: { slots: Slot[] }) {
  return <details className="customer-price-breakdown"><summary>Chi tiết giá từng ca 30 phút</summary>
    <ul>{slots.map(slot => <li key={slot.startsAt}><span>{slot.startsAt}–{slot.endsAt}</span>
      <strong>{money(slot.pricePerSlotExact ?? slot.pricePerSlot)}</strong></li>)}</ul>
  </details>;
}
