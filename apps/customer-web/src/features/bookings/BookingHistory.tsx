import type { Booking } from './bookingApi';
import { dateTime, money } from './bookingApi';
import { PrivateImage } from './PrivateImage';

export function BookingHistory({ booking }: { booking: Booking }) {
  const events = [
    ...(booking.evidence ?? []).map(item => ({ id: item.evidenceId, at: item.reportedAt, title: item.kind === 'SUPPLEMENT' ? 'Đã bổ sung bằng chứng' : 'Đã báo chuyển khoản',
      reference: item.bankReference, note: item.note, proof: item.proofUrl, reason: null as string | null, amount: null as number | string | null })),
    ...(booking.decisions ?? []).map(item => ({ id: item.decisionId, at: item.decidedAt,
      title: item.resolution === 'CONFIRMED' ? 'Chủ sân đã xác nhận nhận tiền' : item.resolution === 'NEEDS_REVIEW' ? 'Chủ sân yêu cầu bổ sung' : 'Chủ sân không xác nhận giao dịch',
      reference: item.bankReference, note: item.note, proof: null, reason: item.reason, amount: item.confirmedAmountExact ?? item.confirmedAmount })),
  ].sort((a, b) => Date.parse(a.at) - Date.parse(b.at));
  if (!events.length) return null;
  return <section className="payment-history" aria-label="Lịch sử thanh toán"><h3>Lịch sử thanh toán</h3>
    <ol>{events.map(item => <li key={item.id}><h4>{item.title}</h4><time dateTime={item.at}>{dateTime(item.at, booking.timezone)}</time>
      {item.reference && <p>Mã giao dịch: <strong>{item.reference}</strong></p>}{item.reason && <p>Lý do: {item.reason}</p>}
      {item.note && <p>Ghi chú: {item.note}</p>}{item.amount !== null && <p>Số tiền đã xác nhận: {money(item.amount)}</p>}
      {item.proof && <PrivateImage path={item.proof} alt="Biên lai chuyển khoản đã gửi" className="payment-proof" />}
    </li>)}</ol></section>;
}
