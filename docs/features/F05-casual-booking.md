# F05 — quote và booking vãng lai

Trạng thái: IN_PROGRESS — triển khai/kiểm thử tự động local hoàn tất, chờ người dùng nghiệm thu tay. Contract trước code 2026-10-07. Người dùng đã chấp nhận quote 2 phút, horizon 60 ngày, hold từng sân và xác nhận giá mới khi thay đổi trong phiên triển khai này.

## Phạm vi, chính sách và quyền

Guest xem lịch F04 và tạo quote không giữ chỗ. Customer ACTIVE đăng nhập tiếp tục lựa chọn, xác nhận quote, tạo một CASUAL/AWAITING_TRANSFER và xem đơn/QR riêng của mình. F06 báo chuyển/xác nhận, F07 series, S3 live được hoãn; không có customer hủy/đổi lịch.

Quyết định cấu hình mặc định theo prompt: `Booking:QuoteSeconds=120`, `Booking:MaxAdvanceDays=60`. Ngày theo timezone venue, hôm nay đến hôm nay+60 (inclusive); start phải sau now. Quote tối đa một ngày trong giờ mở, các ca 30 phút liên tiếp; duration >= minimum. **Cập nhật theo nghiệm thu 2026-10-07:** sau khi đạt minimum, cho phép thêm từng ca 30 phút, không yêu cầu tổng duration chia hết bookingBlockMinutes; sân minimum 120 phút chấp nhận 4, 5, 6, 7... ca liên tiếp còn trống/có giá. Giữ cấu hình block và snapshot hiện có, không đổi policy sân hoặc quy tắc series F07. Hold lấy court.HoldMinutes (5–60), snapshot khi tạo; deadline = createdAt + hold. Giá/policy/QR đổi sau quote trả QUOTE_CHANGED và cần xác nhận quote mới. Quote không cam kết giữ slot. Query/body field lạ hoặc trùng bị từ chối. Các mặc định có thể điều chỉnh bằng configuration, không sửa máy toàn cục.

## API và lỗi

`/api/v1`, envelope `{data,traceId}`, Problem Details. Quote public rate limited; create/list/detail/QR cần bearer CUSTOMER ACTIVE và kiểm tra user DB. Owner/operator/Admin không có quyền customer booking.

- POST `/availability/quote` `{courtId,date,startsAt,endsAt}` (local YYYY-MM-DD, HH:mm). 200: quoteId, expiresAt, venue/court IDs/names, timezone/date, UTC start/end, slots [{startsAt,endsAt,pricePerSlot}], amount/currency, block/min/hold. Không có QR/account/object key.
- POST `/bookings` `{courtId,startsAt,endsAt,quoteId}` (ISO UTC), `Idempotency-Key` 1–128 printable safe characters. 201: detail aggregate. Retry cùng customer/key/canonical body trả cùng booking, không nhân đôi; key/body khác 409 IDEMPOTENCY_KEY_REUSED. Replay tồn tại được đọc trước kiểm tra quote hết hạn. Response dùng trạng thái hiện tại của booking, không lưu signed URL.
- GET `/me/bookings?limit=20&before=<id>`: max100, cursor UUIDv7, list không QR/account; GET `/bookings/{id}`: chủ booking, snapshot/timeline/payment instructions. Ngoài scope 404. GET `/bookings/{id}/qr`: chủ booking, đúng snapshot upload, local bytes hoặc S3 redirect ngắn hạn; no-store/nosniff. Không phát token qua URL; UI lấy QR bằng bearer fetch.

400 VALIDATION_FAILED/UNSUPPORTED_FIELD; 401 UNAUTHORIZED; 403 FORBIDDEN; 404 NOT_FOUND; 409 SLOT_UNAVAILABLE, PRICE_UNAVAILABLE, PAYMENT_SETUP_UNAVAILABLE, QUOTE_EXPIRED, QUOTE_CHANGED, IDEMPOTENCY_KEY_REUSED, SCHEDULE_UNAVAILABLE; 429 RATE_LIMITED; provider 503 MEDIA_UNAVAILABLE. DST ambiguous/invalid không được tự chọn offset.

## Dữ liệu/giao dịch

`booking_quotes`: server-side immutable court/venue interval, local/timezone, amount, snapshot slots/policy/payment setup fingerprint, expires. `bookings`: customer/venue/court/allocation, bookingNo unique, CASUAL, status, UTC/local snapshots, amount, deadline, version, created/expired. `payments`: booking unique, status/amount, recipient JSON snapshot gồm QR upload ID/object key/checksum và bank fields. `idempotency_records`: customer/operation/key unique, requestHash, bookingId; giữ record cùng lịch sử booking, không tự xóa làm retry tạo trùng. Snapshot JSON trong booking thay booking_items vì F05 đúng một court/allocation; FK composite allocation/court và court/venue bảo vệ scope. Không tạo schema F06/F07.

Create lock idempotency theo pg advisory xact lock hash customer/key; lock business → venue → court (cùng quy tắc published/revision F02, court F03), đọc lại status/hours/rules/payment setup/quote; khóa và kiểm tra customer active. So sánh fingerprint giá/policy/recipient, insert BOOKING RESERVED và aggregate/audit/outbox/idempotency trong transaction. Exclusion `ex_f03_allocation_no_overlap` quyết định cuối, 23P01 map SLOT_UNAVAILABLE. Quote public snapshot nhất quán trong repeatable-read. Giá VND numeric(18,0), local-time API grid và DB duration 30 phút, không áp UTC phút 00/30 cho mọi timezone.

Expiry service trong Infrastructure, Worker gọi mỗi 5 giây batch20: booking FOR UPDATE SKIP LOCKED, chỉ AWAITING_TRANSFER/payment AWAITING_TRANSFER deadline<=now; booking/payment EXPIRED, allocation RELEASED, version tăng và audit/outbox cùng transaction. Báo chuyển lock booking trước khi transition; báo chuyển commit trước expiry thì không release. Event BOOKING_CREATED/BOOKING_EXPIRED tới customer qua worker outbox, không báo owner đã thanh toán. Không xóa QR snapshot upload/file.

## UI và nghiệm thu

Auth shared memory session, refresh single-flight/generation guard, routes dùng History API; login/register trở lại internal returnTo, không token storage. Selection đưa court/date/start/end vào URL review; quote mới khi F5/back, tạo bằng stable Idempotency-Key cho retry cùng quote. Customer F5 cần đăng nhập lại theo cơ chế F01 hiện tại, giữ returnTo booking và đọc lại đơn, không tạo mới. Lịch mọi sân/biên giờ/toggle F04 giữ nguyên. Review có quote countdown/slot prices/errors; detail có QR/payment snapshot/countdown và Đơn của tôi; không nút báo chuyển F06.

Acceptance: đúng policy/giá/timezone/snapshot; 20 create overlap hai API host đúng 1 aggregate; idempotency concurrency đúng 1; maintenance conflict; QR/auth scope; expiry đa Worker/race an toàn; migration fresh/repeat/upgrade giữ F04; browser desktop/mobile mock và ít nhất một browser/API/PostGIS flow thật. Test phù hợp PASS, review tuần tự, không nghiêm trọng còn mở trước DONE.

## Bàn giao local 2026-10-07

Đã triển khai toàn bộ scope F05; bằng chứng từng case và giới hạn ở `docs/testing/F05-test-cases.md`, hướng dẫn VS Code PowerShell ở `docs/testing/F05-manual-acceptance.md`. Review tuần tự chưa có reviewer độc lập. Không migrate DB phát triển, commit/push hoặc deploy. F06/F07/S3 live giữ phạm vi riêng; customer F5 vẫn login lại theo memory session đã có.
