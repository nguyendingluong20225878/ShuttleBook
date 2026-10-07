const labels: Record<string, string> = {
  AWAITING_TRANSFER: 'Đang chờ chuyển khoản', AWAITING_OWNER_CONFIRMATION: 'Chờ xác nhận', NEEDS_REVIEW: 'Cần bổ sung bằng chứng',
  CONFIRMED: 'Đã xác nhận', EXPIRED: 'Đã hết hạn', PAYMENT_REJECTED: 'Không xác nhận giao dịch',
};
export function bookingStatusLabel(status: string) { return labels[status] ?? 'Đang cập nhật trạng thái'; }
export function BookingStatus({ status }: { status: string }) {
  return <span className={`booking-status status-${status.toLowerCase()}`}>{bookingStatusLabel(status)}</span>;
}
