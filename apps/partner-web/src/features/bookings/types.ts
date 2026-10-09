export type RequestOptions = { signal?: AbortSignal; idempotencyKey?: string; envelope?: boolean; blob?: boolean; bodyText?: string };
export type PartnerRequest = <T>(path: string, method?: string, body?: object, version?: number, options?: RequestOptions) => Promise<T>;
export class PartnerApiError extends Error {
  constructor(public code: string, public status: number) { super(code); }
}
export const bookingStatusLabel = (status: string) => ({
  AWAITING_TRANSFER: 'Chờ chuyển khoản', AWAITING_OWNER_CONFIRMATION: 'Chờ xác nhận', NEEDS_REVIEW: 'Cần bổ sung',
  CONFIRMED: 'Đã xác nhận', EXPIRED: 'Hết hạn chuyển khoản', PAYMENT_REJECTED: 'Không xác nhận giao dịch',
}[status] ?? status);
export const money = (amount: number | string) => `${new Intl.NumberFormat('vi-VN').format(typeof amount === 'string' ? BigInt(amount) : amount)}đ`;
export const localDateTime = (value: string | null | undefined, timezone: string) => value
  ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short', timeZone: timezone }).format(new Date(value)) : '—';
export type BookingSeriesInfo = { seriesId: string; seriesNo: string; startsOn: string; endsOn: string;
  dayOfWeek: string; localStartTime: string; durationMinutes: number; occurrenceCount: number; paymentPlan: 'FULL_SERIES';
  occurrences?: Array<{ bookingId: string; date: string; localStart: string; localEnd: string; startsAt: string; endsAt: string;
    amount: number; amountExact?: string; status: string }> };
export type BookingSummary = { bookingId: string; bookingNo: string; bookingType?: string; series?: BookingSeriesInfo | null;
  venueId: string; venueName: string; courtName: string;
  status: string; date: string; localStart: string; localEnd: string; timezone: string; amount: number; amountExact?: string; version: number;
  isOverdue?: boolean; confirmationDueAt?: string | null };
export type BookingDetail = BookingSummary & { businessId?: string; customer?: { maskedContact: string };
  paymentDeadline: string; createdAt: string; payment: { paymentId: string; status: string; expectedAmount: number; expectedAmountExact?: string;
    firstReportedAt: string | null; lastReportedAt: string | null; confirmedAmount: number | null; confirmedAmountExact?: string | null;
    confirmedAt: string | null; confirmedBy?: string | null; bankCode: string; accountName: string;
    maskedAccountNumber: string; transferContent: string };
  slots: Array<{ startsAt: string; endsAt: string; pricePerSlot: number }>;
  evidence: Array<{ evidenceId: string; kind: string; bankReference: string | null; note: string | null; reportedAt: string; proofUrl: string | null }>;
  decisions: Array<{ decisionId: string; resolution: string; reasonCode: string | null; reason: string | null;
    confirmedAmount: number | null; confirmedAmountExact?: string | null; bankReference: string | null; note: string | null; decidedAt: string }> };
export type BookingList = { items: BookingSummary[]; nextCursor: string | null;
  counts: { awaitingOwnerConfirmation: number; needsReview: number } };
export function paymentError(error: unknown) {
  const code = error instanceof PartnerApiError ? error.code : '';
  return ({ PRECONDITION_FAILED: 'Đơn vừa được cập nhật. Tải lại đơn và kiểm tra trước khi quyết định.',
    STATE_CONFLICT: 'Trạng thái đơn đã thay đổi. Hãy tải lại đơn.', PAYMENT_AMOUNT_MISMATCH: 'Số tiền chưa khớp tổng tiền của đơn. Yêu cầu khách bổ sung để đối chiếu.',
    IDEMPOTENCY_KEY_REUSED: 'Nội dung quyết định đã thay đổi. Kiểm tra lại rồi gửi một quyết định mới.',
    VALIDATION_FAILED: 'Thông tin quyết định chưa hợp lệ. Kiểm tra các trường đã nhập.',
    NOT_FOUND: 'Không tìm thấy đơn trong quyền quản lý của bạn.', FORBIDDEN: 'Bạn không còn quyền xử lý đơn này.',
    RATE_LIMITED: 'Bạn gửi yêu cầu quá nhanh. Vui lòng đợi rồi thử lại.',
  }[code] ?? 'Không kết nối được máy chủ. Bạn có thể thử lại; hệ thống giữ nguyên mã yêu cầu để tránh xử lý trùng.');
}
