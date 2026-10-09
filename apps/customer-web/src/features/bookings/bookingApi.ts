export type Slot = { startsAt: string; endsAt: string; pricePerSlot: number; pricePerSlotExact?: string };
export type SeriesOccurrence = { bookingId: string; date: string; localStart: string; localEnd: string;
  startsAt: string; endsAt: string; amount: number; amountExact?: string; status: string; slots?: Slot[] };
export type BookingSeries = { seriesId: string; seriesNo: string; startsOn: string; endsOn: string; dayOfWeek: string;
  localStartTime: string; durationMinutes: number; occurrenceCount: number; paymentPlan: 'FULL_SERIES'; occurrences: SeriesOccurrence[] };
export type PaymentEvidence = { evidenceId: string; kind: string; bankReference: string | null; note: string | null;
  reportedAt: string; proofUrl: string | null };
export type PaymentDecision = { decisionId: string; resolution: string; reasonCode: string | null; reason: string | null;
  confirmedAmount: number | null; confirmedAmountExact?: string | null; bankReference: string | null; note: string | null; decidedAt: string };
export type Booking = { bookingId: string; bookingNo: string; status: string; version: number; venueName: string; courtName: string;
  bookingType?: string; series?: BookingSeries | null;
  date: string; localStart: string; localEnd: string; timezone: string; amount: number; amountExact?: string; paymentDeadline: string;
  createdAt: string; expiredAt: string | null; slots: Slot[]; evidence?: PaymentEvidence[]; decisions?: PaymentDecision[];
  isOverdue?: boolean; confirmationDueAt?: string | null;
  payment: { paymentId?: string; status: string; bankCode: string; accountName: string; maskedAccountNumber: string;
    qrUrl: string | null; transferContent: string; expectedAmount?: number; expectedAmountExact?: string; firstReportedAt?: string | null;
    lastReportedAt?: string | null; confirmedAmount?: number | null; confirmedAmountExact?: string | null; confirmedAt?: string | null; confirmedBy?: string | null } };
export const money = (amount: number | string | bigint) => `${new Intl.NumberFormat('vi-VN').format(typeof amount === 'string' ? BigInt(amount) : amount)}đ`;
export const dateTime = (value: string, timezone: string) => new Date(value).toLocaleString('vi-VN', { timeZone: timezone });
export class BookingApiError extends Error {
  constructor(code: string, public readonly status: number, public readonly problem?: { conflictDates?: string[];
    conflicts?: { date: string; startsAt?: string; endsAt?: string; code?: string }[] }) { super(code); }
}
export function accessDenied(reason: unknown) {
  return reason instanceof BookingApiError && [401, 403, 404].includes(reason.status) ||
    reason instanceof Error && reason.message === 'SESSION_REQUIRED';
}
export async function data<T>(response: Response): Promise<T> {
  const body = await response.json().catch(() => null);
  if (!response.ok) throw new BookingApiError(body?.code ?? 'REQUEST_FAILED', response.status, body ?? undefined);
  return body?.data as T;
}
const messages: Record<string, string> = {
  QUOTE_CONSUMED: 'Báo giá này đã được dùng để tạo đơn. Hãy mở Đơn của tôi để kiểm tra đơn đã tạo; nếu muốn đặt lịch khác, quay lại lịch sân và lấy báo giá mới.',
  QUOTE_EXPIRED: 'Báo giá đã hết hạn. Hãy lấy báo giá mới.',
  QUOTE_CHANGED: 'Giá hoặc cấu hình sân đã thay đổi. Hãy lấy và xác nhận báo giá mới.',
  SLOT_UNAVAILABLE: 'Khung giờ vừa được giữ hoặc khóa. Hãy quay lại lịch để chọn ca khác.',
  SERIES_CONFLICT: 'Một hoặc nhiều buổi vừa bị trùng lịch. Chưa tạo lịch cố định; hãy kiểm tra các ngày xung đột và lấy báo giá mới.',
  SCHEDULE_UNAVAILABLE: 'Có buổi nằm ngoài giờ hoạt động hoặc giờ địa phương chưa hợp lệ. Hãy chọn lại lịch.',
  PRICE_UNAVAILABLE: 'Khung giờ này chưa có bảng giá hợp lệ.', PAYMENT_SETUP_UNAVAILABLE: 'Cơ sở chưa có QR nhận tiền hợp lệ.',
  VALIDATION_FAILED: 'Thông tin chưa hợp lệ. Hãy kiểm tra các trường đã nhập.',
  NOT_FOUND: 'Không tìm thấy đơn hoặc tệp trong quyền của bạn.',
  IDEMPOTENCY_KEY_REUSED: 'Nội dung yêu cầu đã thay đổi. Hãy làm mới đơn trước khi gửi một yêu cầu mới.',
  SESSION_REQUIRED: 'Phiên đã hết hạn. Hãy đăng nhập để tiếp tục.', RATE_LIMITED: 'Bạn gửi yêu cầu quá nhanh. Vui lòng thử lại sau.',
  UNAUTHORIZED: 'Phiên đã hết hạn. Hãy đăng nhập để tiếp tục.', FORBIDDEN: 'Bạn không còn quyền xem hoặc xử lý đơn này.',
  PAYMENT_DEADLINE_EXPIRED: 'Đã hết hạn báo chuyển khoản. Hãy làm mới đơn để xem trạng thái.',
  STATE_CONFLICT: 'Đơn vừa được xử lý. Hãy làm mới đơn để xem trạng thái mới nhất.',
  PRECONDITION_FAILED: 'Đơn đã thay đổi. Hãy làm mới và kiểm tra lại trước khi gửi.',
  PRECONDITION_REQUIRED: 'Chưa lấy được phiên bản mới của đơn. Hãy làm mới đơn.',
  UPLOAD_NOT_READY: 'Ảnh chưa tải lên hoàn tất. Hãy tải lại ảnh rồi gửi.',
  UPLOAD_MISMATCH: 'Ảnh tải lên không khớp. Hãy chọn và tải lại ảnh.', MEDIA_UNAVAILABLE: 'Chưa tải được ảnh. Vui lòng thử lại sau.',
  UPLOAD_NOT_FOUND: 'Ảnh chưa được tải lên. Hãy thử lại hoặc chọn lại ảnh.',
  UPLOAD_FAILED: 'Chưa tải được ảnh biên lai. Bạn có thể thử lại hoặc gửi báo chuyển không kèm ảnh.',
};
export function failure(reason: unknown) {
  return messages[reason instanceof Error ? reason.message : ''] ?? 'Không thể kết nối máy chủ. Bạn có thể thử lại cùng yêu cầu hoặc xem Đơn của tôi.';
}
