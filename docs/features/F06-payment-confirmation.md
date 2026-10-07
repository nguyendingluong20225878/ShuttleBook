# F06 — báo chuyển, đối chiếu và xác nhận thanh toán

## Trạng thái và phạm vi

Spec trước code ngày 2026-10-07; **DONE — người dùng xác nhận nghiệm thu, các điều chỉnh sau nghiệm thu đã triển khai/kiểm thử local đạt**. Bằng chứng cuối ở testcase/progress; không còn lỗi nghiêm trọng đã biết. Người dùng chưa gửi biên bản chi tiết từng ca tay; đây là xác nhận nghiệm thu của người dùng, bản điều chỉnh mới có gate tự động riêng. Phụ thuộc booking CASUAL F05 và owner membership F02, QR/price/policy F03, availability F04. Scope: customer report/supplement + proof private local; owner list/detail/confirm/review/final reject; outbox/in-app/polling; SLA alert; audit/concurrency/idempotency/migration. F07 series, F08 invitations, external notification providers, payment gateway/dispute/refund/repricing, S3 live không thuộc mốc này. CONFIRMED kết thúc thành công; không check-in/completed/customer cancel/reschedule.

## Chính sách

- Đã chốt F05: quote TTL 120 giây, horizon 60 ngày local venue, hold snapshot court (5–60 phút, mặc định 20). Không thay đổi.
- **Cập nhật theo nghiệm thu 2026-10-07:** người dùng chấp nhận F06 và yêu cầu giản lược form: bỏ ô mã giao dịch ở customer và owner. API vẫn nhận `bankReference` tùy chọn để tương thích lịch sử/client cũ; bỏ/null/trắng chuẩn hóa null, nếu cung cấp vẫn giới hạn 100 ký tự. Biên lai chụp màn hình và ghi chú tùy chọn (diễn giải theo “có thể cung cấp ảnh”), không tự sinh mã ngân hàng giả. Owner giữ số tiền thực nhận và quyết định/lý do cần thiết. Confirm chỉ khi confirmedAmount == expectedAmount; SLA 30 phút owner + Admin một lần, gồm NEEDS_REVIEW, bổ sung không reset và không giải phóng sân vì chậm xác nhận giữ nguyên.
- Reference nullable/trim ≤100; note nullable/trim ≤1.000; reason trim 1–1.000, reasonCode `EVIDENCE_REQUIRED|AMOUNT_MISMATCH|REFERENCE_MISMATCH|TRANSACTION_NOT_FOUND|OTHER` (mã lý do cũ vẫn tương thích, UI mới không cần chọn mã giao dịch). PNG/JPEG/WebP 1–5.242.880 bytes, sha256 base64 32 bytes, MIME/magic/checksum xác minh; proof không nhận raw key/external URL.

## Actor và quyền

- Customer ACTIVE chỉ đơn mình; guest 401, sai type 403, ngoài scope 404.
- Operator ACTIVE và business membership OWNER ACTIVE bao phủ venue được booking.read/payment.confirm. F08 chưa có invitation/venue permission; không tự cấp quyền theo accountType.
- Admin nhận cảnh báo SLA tối thiểu, không được confirm/reject/proof ở endpoint F06. Alert không cấp quyền đọc bằng chứng. Read/action link luôn reauthorize.
- Owner còn membership ACTIVE có thể đối chiếu booking cũ dù venue/court ngừng nhận đơn mới. Business/membership/user bị suspend/revoke chặn action; allocation không tự release vì thay đổi catalog.

## State pairs

| Command | Nguồn booking/payment | Đích booking/payment | Allocation |
|---|---|---|---|
| INITIAL report còn hạn | AWAITING_TRANSFER/AWAITING_TRANSFER | AWAITING_OWNER_CONFIRMATION/TRANSFER_REPORTED | RESERVED |
| NEEDS_REVIEW | AWAITING_OWNER_CONFIRMATION/TRANSFER_REPORTED | NEEDS_REVIEW/NEEDS_REVIEW | RESERVED |
| SUPPLEMENT | NEEDS_REVIEW/NEEDS_REVIEW | AWAITING_OWNER_CONFIRMATION/TRANSFER_REPORTED | RESERVED |
| Confirm | Hai cặp chờ trên | CONFIRMED/PAID | RESERVED |
| FINAL_REJECTION | Hai cặp chờ trên | PAYMENT_REJECTED/REJECTED | RELEASED |
| Expiry | AWAITING_TRANSFER/AWAITING_TRANSFER, now ≥ deadline | EXPIRED/EXPIRED | RELEASED |

Initial report kiểm tra now sau booking lock; now == deadline không hợp lệ. Supplement không áp dụng deadline cũ. Terminal không revive; replay không mutation. Every real transition version +1, append-only history, audit và outbox cùng transaction.

## API contract

Base `/api/v1`; `{data,traceId}`; Problem Details `{status,code,traceId}`. Mutation thanh toán có Idempotency-Key 1–128 `[A-Za-z0-9._-]`, If-Match `"positive-version"`; thiếu If-Match 428 PRECONDITION_REQUIRED, sai format 400 VALIDATION_FAILED, stale 412 PRECONDITION_FAILED. Duplicate JSON/unknown fields reject, body ≤8 KiB. Optional fields có thể bỏ hoặc null.

| Route | Input/quyền | Output |
|---|---|---|
| POST /bookings/{id}/transfer-evidence | CUSTOMER; `{proofUploadId?,note?,bankReference?}` (cho phép `{}`) | 200 booking detail mới; INITIAL hoặc SUPPLEMENT từ state |
| POST /bookings/{id}/proof-uploads/presign | CUSTOMER; `{contentType,sizeBytes,sha256Base64}` | 200 `{id,uploadUrl,uploadHeaders,expiresAt,contentType,sizeBytes}` |
| POST /uploads/{id}/complete | Customer sở hữu PAYMENT_PROOF hoặc guard operator media cũ | `{id,status}` READY, retry không duplicate audit |
| PUT /uploads/{id}/content | Signed upload local, giữ contract cũ | 204, immutable file/checksum/MIME |
| GET /uploads/{id}/view | Customer sở hữu proof hoặc owner có scope và proof đã gắn evidence | bytes private no-store; QR/ảnh venue giữ guard cũ |
| GET /operator/venues/{venueId}/bookings | OWNER; `status?,dateFrom?,dateTo?,limit?,before?` | `{items,nextCursor,counts}` |
| GET /operator/bookings/{id} | OWNER scoped | Booking detail + `customer:{maskedContact}`, không reference/account trên list |
| POST /operator/bookings/{id}/confirm-payment | OWNER; `{confirmedAmount,note?,bankReference?}` | 200 detail, PAID/CONFIRMED |
| POST /operator/bookings/{id}/reject-payment | OWNER; `{resolution,reasonCode,reason}` | 200 detail; resolution NEEDS_REVIEW hoặc FINAL_REJECTION |
| GET /payments/{id} | Customer owns hoặc scoped OWNER | Payment DTO + bookingId/version; không full account/raw key |
| GET /me/notifications | Auth user; `limit?,before?` | `{data:[...],traceId,unreadCount,nextCursor}`; giữ data array cũ |
| POST /me/notifications/{id}/read | Own notification | id/readAt, idempotent |

List: limit 1–100 mặc định 20, UUID cursor descending id; status chỉ enum booking, dateFrom/dateTo yyyy-MM-dd trên local_date snapshot venue, range ordered ≤366 ngày khi có cả hai. Counts `{awaitingOwnerConfirmation,needsReview}` cùng venue/date scope nhưng độc lập filter status. Notification limit mặc định 50/max100, unreadCount toàn user trong scope còn được đọc.

### Booking detail DTO mở rộng không phá F05

Giữ `bookingId,bookingNo,bookingType,status,venueId,courtId,venueName,courtName,timezone,date,localStart,localEnd,startsAt,endsAt,amount,currency,paymentDeadline,version,createdAt,expiredAt,slots,bookingBlockMinutes,minimumBookingMinutes,holdMinutes`.

`payment` giữ `paymentId,status,bankCode,accountName,maskedAccountNumber,qrUrl,transferContent`; thêm `expectedAmount,firstReportedAt,lastReportedAt,confirmedAmount,confirmedAt,confirmedBy`. `expectedAmountExact` và `confirmedAmountExact` là chuỗi thập phân không làm tròn dành cho UI đối chiếu VND tới 18 chữ số; giữ các number cũ để tương thích F05. Confirm gửi integer JSON chính xác. qrUrl chỉ AWAITING_TRANSFER như F05; các state sau không yêu cầu customer trả lại tiền hoặc lấy QR hiện hành.

Toplevel:

- `evidence`: `[{evidenceId,kind,bankReference,note,reportedAt,proofUrl}]`; kind INITIAL/SUPPLEMENT; proofUrl `/api/v1/uploads/{uploadId}/view` hoặc null, chỉ khi attachment đã xác minh. Không raw key/signed URL.
- `decisions`: `[{decisionId,resolution,reasonCode,reason,confirmedAmount,bankReference,note,decidedAt}]`; resolution CONFIRMED/NEEDS_REVIEW/FINAL_REJECTION.
- `isOverdue`, `confirmationDueAt` nullable; không kéo dài hold deadline.
- Operator thêm `customer:{maskedContact}`. Customer không nhận contact của user khác. Actor ID quyết định chỉ metadata cần thiết, không public full audit.

Notification thêm `bookingId` nullable, `action` nullable: CUSTOMER_BOOKING/OPERATOR_BOOKING/ADMIN_PAYMENT_ALERT; client tạo đường nội bộ có allowlist. Alert Admin hiển thị metadata mã đơn/cơ sở/thời gian chờ, không link API payment owner hoặc proof. Read/list lọc scope hiện tại nếu là operator event.

### Errors

400 VALIDATION_FAILED/UNSUPPORTED_FIELD; 401 UNAUTHORIZED; 403 FORBIDDEN; 404 NOT_FOUND; 409 STATE_CONFLICT/PAYMENT_DEADLINE_EXPIRED/IDEMPOTENCY_KEY_REUSED/PAYMENT_AMOUNT_MISMATCH/UPLOAD_NOT_READY/UPLOAD_MISMATCH; 412 PRECONDITION_FAILED; 428 PRECONDITION_REQUIRED; 429 RATE_LIMITED; 503 MEDIA_UNAVAILABLE. Scope sai upload trả404; chưaREADY đúng scope409. Confirm amount lệch trả lỗi không mutation; owner gửi quyết định NEEDS_REVIEW riêng.

## Thiết kế dữ liệu và lock

- payments: thêm first/lastReportedAt, confirmedAmount/By/At, confirmationAlertedAt. Không thay recipient/price snapshot. PAID metadata có check; ngăn nullable bypass. Migration tăng dần F06ExactPaymentAmount bổ sung CHECK PAID phải có confirmedAmount == expectedAmount theo policy đã duyệt. F06OptionalBankReference cho evidence reference nullable và bỏ reference-required khỏi CHECK decision CONFIRMED; vẫn bắt buộc số tiền/người/thời gian, giữ reason-required review/reject, mọi FK/READY/unique proof. Không sửa migration cũ; Down từ chối khi đã có dữ liệu thiếu reference để tránh tự sinh/sửa lịch sử.
- payment_evidence: paymentId/bookingId/customerId, reference/note/kind/reportedAt, proofUploadId nullable; FK/composite FK payment-booking, booking-customer, upload-booking/customer/venue cho proof, unique proof attachment, lengths/checks. Không sửa/xóa history qua API.
- payment_decisions: paymentId/bookingId/actorUserId/resolution/amount/reference/note/reasonCode/reason/decidedAt; FK/checks/append-only.
- MediaUpload thêm BookingId nullable, purpose PAYMENT_PROOF đúng booking/owner/venue; QR/VENUE_IMAGE cũ BookingId null, không thay schema các upload đã có.
- Idempotency rename CustomerId → ActorUserId/actor_user_id tăng dần giữ giá trị/key/hash cũ; unique actor/operation/key, update CASUAL_CREATE query tương thích. Không delete records F05.
- Outbox thêm target kind? Chốt dispatcher explicit booking owner recipient resolution, không default Admin cho event booking. Notification giữ unique outbox/user; bookingId/action có thể derive từ join outbox, không cần duplicate schema.

Mutation lock order: advisory actor/operation/key → booking FOR UPDATE → user FOR SHARE → membership/business FOR SHARE khi operator → payment FOR UPDATE → allocation khi release. Không khóa court/venue sau booking; F05/F03 không khóa booking cũ nên tránh vòng khóa. Reauthorize trong transaction. Replay sau resource scope/auth, trước IfMatch/deadline/state; replay read booking/payment nhất quán bằng transaction snapshot hoặc booking lock.

Quyền operator đọc từ business ACTIVE + membership OWNER ACTIVE + user ACTIVE, venue liên kết booking; không yêu cầu venue PUBLISHED/court ACTIVE cho xử lý tiền đơn cũ. Membership revoke/user suspend đợi SHARE lock command; command mới sau revoke bị chặn.

Worker expiry/SLA đều booking lock trước payment; no new user/business locks inversion. SLA scans pending, now≥firstReportedAt+policy, SKIP LOCKED; confirmationAlertedAt dedup, outbox/audit cùng tx, không status/version/release mutation. Confirm/reject concurrent xử lý lại status dưới khóa.

## Outbox và UI

- Event PAYMENT_TRANSFER_REPORTED/PAYMENT_EVIDENCE_SUPPLEMENTED tới owners đang ACTIVE đúng business. PAYMENT_NEEDS_REVIEW/PAYMENT_CONFIRMED/PAYMENT_REJECTED tới đúng customer. PAYMENT_CONFIRMATION_OVERDUE tới scoped owner + Admin theo policy chốt. Recipients revalidate trước tạo notification; no recipient giữ message để retry/cảnh báo, không gửi fallback Admin sai event.
- Trích `OutboxDispatch.ProcessOne` vào Infrastructure để test runtime dispatcher cùng logic Worker, SKIP LOCKED + atomic create/processed mark; failure backoff 5*2^attempt cap3600, alert threshold8 (technical configurable), metadata exception type không payload. Unknown event không marked processed. Dedup outbox/user DB unique.
- Customer report/proof/history/supplement; trạng thái Chờ xác nhận/Đã xác nhận/Cần bổ sung/Không xác nhận giao dịch/Hết hạn. Poll5s visible/focus, no autoPOST; auth returnTo hiện có.
- Partner thêm sidebar Đơn đặt sân, business/venue scope, filter/paging/detail/actions/history; giữ Thanh toán&QR cấu hình. Final reject confirmation, 412 reload không tự quyết định lại. Notifications polling/unread/link/retry không phá approval.
- Admin SLA notification list tối thiểu trong portal hiện có, không grant payment.confirm; no full dispute/reporting module.

## Acceptance/test gate

F06-T01–T20 trong prompt là acceptance: happy/live flow, validation/private proof, exact deadline/races, same/different-key retries, supplement sau deadline, confirm/reject race, scoped inactive/revoked roles, immutable snapshot, fresh/upgrade migration, outbox retry/crash/multiple workers, SLA dedup, F5/back/mobile/scope change, CONFIRMED constraint/exclusion. Chi tiết trước code trong testcase. UI mock chỉ UI; DB semantics chạy PostGIS thật. S3 live NOT RUN; F05 giữ IN_PROGRESS theo nghiệm thu hiện tại. Chỉ chốt feature theo evidence/runtime và acceptance đã đạt.
