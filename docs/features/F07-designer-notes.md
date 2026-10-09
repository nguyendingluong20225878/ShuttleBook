# F07 — ghi chú thiết kế kỹ thuật

> **Thay đổi theo yêu cầu 2026-10-08:** quote hợp lệ của Customer ACTIVE giữ chỗ tạm đến expiresAt (mặc định120s), vãng lai một khoảng/cố định toàn kỳ. Khi create, chuyển allocation tạm sang booking nguyên tử; hết hạn tự giải phóng. Quy tắc này thay thế mọi mô tả quote không giữ chỗ trước đó. Contract và testcase mới: [F07-quote-reservations](./F07-quote-reservations.md).


Ngày: 2026-10-07. **ĐÃ DUYỆT**: người dùng trả lời “đồng ý cả 3”: thu 100% toàn kỳ bằng một QR/một xác nhận, horizon 60 ngày/tối đa 12 buổi, quote 120 giây/holdMinutes sân. Hết hạn chưa báo chuyển giải phóng cả kỳ; đã báo chuyển/NEEDS_REVIEW không tự giải phóng vì owner chậm. Contract dưới đây là căn cứ triển khai.

## Phạm vi và contract

- Một lịch cố định lặp hàng tuần, cùng court, một thứ, cùng giờ; mỗi buổi ít nhất `max(120, court.minimumBookingMinutes)`, chia hết 30 phút. Không ép thời lượng là bội `bookingBlockMinutes`.
- `startsOn/endsOn` là ngày địa phương, hai đầu bao gồm; `endsOn >= startsOn.AddMonths(1)`. Ngày bắt đầu không cần trùng thứ đã chọn. Sinh những ngày cùng thứ trong cửa sổ; không dùng 4 tuần thay cho một tháng lịch.
- Kiểm tra toàn bộ cửa sổ trong horizon của timezone venue, và thời điểm bắt đầu buổi đầu phải ở tương lai. Không hỗ trợ ca qua nửa đêm ngoài khả năng F03 hiện có.
- Kiểm tra mọi biên 30 phút có giờ địa phương invalid/ambiguous; trả schedule unavailable thay vì tự chọn một offset DST.
- Quote tính giá từng buổi/từng ca từ F03 trong một snapshot transaction. Giá của từng buổi có thể khác nhau; tổng dùng phép cộng có kiểm tra giới hạn `numeric(18,0)`.
- Tạo tất cả series/bookings/allocations/payment/audit/outbox/idempotency một transaction; một xung đột rollback toàn bộ. Không bỏ ngày xung đột.

### API mới

`POST /api/v1/booking-series/quote` (Customer authenticated, rate limit quote):

```json
{"courtId":"<uuid>","dayOfWeek":"TUESDAY","localStartTime":"18:00","durationMinutes":120,"startsOn":"2026-10-13","endsOn":"2026-11-13"}
```

Response envelope `data/traceId`, data: `quoteId` nullable, `expiresAt` nullable, `canCreate`, `courtId`, `venueId`, `courtName`, `venueName`, `timezone`, `dayOfWeek`, `startsOn`, `endsOn`, `localStartTime`, `durationMinutes`, `occurrenceCount`, `amount`, `amountExact`, `currency`, `holdMinutes`, `minimumBookingMinutes`, `bookingBlockMinutes`, `occurrences`, `conflicts`.

- Occurrence preview: `date`, `localStart`, `localEnd`, `startsAt`, `endsAt`, `amount`, `amountExact`, `slots`.
- Conflict: `date`, `startsAt`, `endsAt`, `code`. Nếu chỉ allocation bị trùng nhưng giá đầy đủ, trả preview + conflicts, `canCreate=false`, `quoteId=null`, không tạo quote dùng được.
- Không invent giá cho ngày thiếu price; trả `409 PRICE_UNAVAILABLE` kèm ngày liên quan. Court/venue chưa published/active trả `404 NOT_FOUND` như F05.

`POST /api/v1/booking-series` nhận `{quoteId}` và `Idempotency-Key`. Thành công 201 trả DTO group-aware dùng lại màn booking F06, `bookingId` là payment anchor. Replay vẫn kiểm tra user/scope hiện tại, rồi trả aggregate hiện tại; không phụ thuộc quote đã hết hạn. Đổi body với key cũ trả `409 IDEMPOTENCY_KEY_REUSED`.

Existing customer/operator booking routes được dùng lại. DTO giữ `bookingId`, `bookingNo`, `bookingType="RECURRING_OCCURRENCE"` (enum tài liệu 03), `status`, `version`, `amount/amountExact` là **tổng cả kỳ**, `payment.expectedAmount/expectedAmountExact` là tổng cả kỳ, `payment.transferContent=seriesNo`, `payment.qrUrl` là URL anchor. Thêm:

```json
{"series":{"seriesId":"<uuid>","seriesNo":"<unique>","startsOn":"2026-10-13","endsOn":"2026-11-13","dayOfWeek":"TUESDAY","localStartTime":"18:00","durationMinutes":120,"occurrenceCount":5,"paymentPlan":"FULL_SERIES","occurrences":[{"bookingId":"<uuid>","date":"2026-10-13","localStart":"18:00","localEnd":"20:00","startsAt":"<UTC>","endsAt":"<UTC>","amount":400000,"amountExact":"400000","status":"AWAITING_TRANSFER"}]}}
```

Lists trả một hàng cho mỗi đơn vãng lai/lịch cố định, không một hàng mỗi occurrence. Date filter operator phải match nếu **bất kỳ** occurrence nằm trong khoảng lọc. Counts là số nhóm thanh toán thực sự, không số buổi. Admin chỉ nhận SLA alert, không đọc payment/proof/confirm.

### Error

- `400 VALIDATION_FAILED`: enum/date/grid/duration/month/horizon/max occurrences/unknown invalid body. Unknown field vẫn `UNSUPPORTED_FIELD`.
- `409 QUOTE_EXPIRED`, `QUOTE_CHANGED`, `PAYMENT_SETUP_UNAVAILABLE`, `SCHEDULE_UNAVAILABLE`, `PRICE_UNAVAILABLE` giữ F05 semantics.
- `409 SERIES_CONFLICT`: create bị trùng một hay nhiều ngày, problem+json thêm `conflicts`/`conflictDates`, không rò khách/booking đã chiếm chỗ.
- F06 commands giữ `428 PRECONDITION_REQUIRED`, `412 PRECONDITION_FAILED`, `409 STATE_CONFLICT`, `PAYMENT_DEADLINE_EXPIRED`, `PAYMENT_AMOUNT_MISMATCH`, `UPLOAD_NOT_READY/MISMATCH`; 401/403/404 theo role/scope hiện tại.

## Dữ liệu và tính nhất quán

Thêm `BookingSeries`: id UUIDv7, series_no unique, customer_id/venue_id/court_id scoped FKs, starts_on/ends_on/day_of_week/local_start/duration_minutes/timezone, payment_plan FULL_SERIES, occurrence_count, amount numeric(18,0), created_at. Thiết kế triển khai lưu hold_minutes/payment_deadline snapshot đồng nhất trên các occurrence và đọc canonical anchor, không nhân bản thêm trên series. Status/version canonical lấy từ payment anchor; tất cả occurrence nhận cùng status/version/deadline trong mỗi command, không thêm state Active/Completed khác CONFIRMED.

Thêm `BookingSeriesQuote` riêng lưu input chuẩn hóa, occurrence/slot/amount snapshot, fingerprint, amount numeric(18,0), expires_at/created_at. Policy/recipient/timezone được gắn vào fingerprint để recheck trước create; recipient đầy đủ chỉ được snapshot trên payment khi tạo. Cập nhật2026-10-08: BookingQuote vãng lai cũng có reservation có quyền sở hữu; quote lịch sử không có reservation phải lấy lại mới. Fingerprint bao gồm toàn bộ ngày/UTC/slots/prices + court policy + recipient + timezone; quote thành công giữ allocation QUOTE_HOLD cho mọi buổi.

`Booking.series_id` nullable, `booking_type` CASUAL/RECURRING_OCCURRENCE và CHECK tương ứng null/non-null. FK `(series_id,customer_id,venue_id,court_id)` → Series `(id,customer_id,venue_id,court_id)` bảo đảm scope. Unique `(series_id,local_date)` tránh lặp một ngày. Mỗi occurrence giữ amount/slots riêng, một allocation riêng, không ghi tổng kỳ vào amount của buổi đầu.

### Một payment và anchor

Khuyến nghị thêm `Booking.payment_scope_id NOT NULL = COALESCE(series_id,id)` (CHECK), backfill bookings cũ=id. Thêm `Payment.payment_scope_id NOT NULL`, backfill payment cũ=booking_id, unique payment_scope_id; composite FK `(booking_id,payment_scope_id)` → Booking `(id,payment_scope_id)` non-null alternate key. Như vậy một payment đại diện toàn nhóm, không thể gắn anchor khác series. Anchor là booking có payment, thường buổi đầu. Tránh alternate key chứa nullable SeriesId vì EF sẽ làm cột đó required, làm hỏng CASUAL; tránh FK vòng series→anchor→series.

Evidence/Decision/MediaUpload/Idempotency giữ FK anchor hiện tại, không nhân bản payment/evidence cho mỗi buổi. Không cần mở proof đến toàn venue hay Admin. Proof presign cho bất kỳ occurrence được chuẩn hóa thành anchor; READY upload vẫn gắn anchor/customer/venue chính xác.

## Giao dịch và lock

- Create: actor/operation/key advisory lock → replay current auth → business → venue → court (F02/F03 lock order) → current user FOR SHARE → recompute all occurrences/fingerprint → insert all atomic.
- Shared `ResolveAnchor` đọc booking scope, tìm payment bằng payment_scope_id. Phải normalize ID **trước hash/idempotency/locking**, không khóa một occurrence rồi mới series: dễ deadlock hoặc mutate một buổi.
- Command: advisory lock → series FOR UPDATE (nếu fixed) → all bookings ORDER BY id FOR UPDATE → actor FOR SHARE → current business/membership FOR SHARE khi owner → payment FOR UPDATE → allocations ORDER BY id FOR UPDATE nếu release. Worker dùng cùng thứ tự series → bookings → payment → allocations.
- Replay sau fresh auth/scope, trước If-Match/state/deadline. Fixed commands cùng khóa/If-Match canonical anchor; cập nhật toàn bộ status/version một lần, payment/evidence/decision/event/audit/idempotency chỉ một lần. Thông báo/amount/account/QR dùng series snapshot, không tổng một buổi.
- Report/supplement/review/confirm/reject đều áp dụng toàn kỳ. Confirm đối chiếu đúng tổng kỳ. Final reject giải phóng tất cả. Hết hạn chỉ AWAITING_TRANSFER/unreported; không tự release review/reported/confirmed.
- Expiry/SLA candidate selection phải tìm nhóm/anchor, không chọn+lock occurrence rồi đảo lock order. `SKIP LOCKED` ở canonical lock; nhiều Worker không xử lý lặp. FirstReportedAt/SLA một payment, supplement không reset, cảnh báo owner+Admin một lần.
- Outbox có thể giữ event types F06 và entityId anchor; dispatcher nhìn series để render seriesNo/số buổi/kỳ/tổng tiền. Một event logical, notification dedup giữ nguyên. Deep link phải mở anchor đúng role; không gửi proof/account đầy đủ trong payload.
- Nếu exclusion 23P01 xảy ra, rollback và dùng context/transaction mới đọc conflictDates; không query transaction đã abort hoặc tracker còn pending entities. Việc court lock serialize creation chưa thay thế exclusion constraint.

## Migration và kiểm thử bắt buộc

- Incremental migration từ F06 cuối, bảo toàn casual/payment/evidence/hash/QR cũ; backfill payment_scope_id, tạo nullable series_id, constraints/index/FKs. Down phải fail nếu còn series/RECURRING_OCCURRENCE thay vì xóa lịch hoặc ép CASUAL.
- Unit recurrence: <120, court min, non-grid, <calendar month, Jan31→Feb28, 4/5 occurrences, weekday startsOn khác, boundary horizon/count, DST spring/fall và timezone UTC-offset lẻ.
- PostgreSQL: create full series snapshots; fourth-week conflict no partial writes; overlapping casual/series/concurrent series exactly one winner; key replay+body change; price/QR/min/hold/timezone quote changed; sum overflow; DB FK/type/grid/no overlap; upgrade old casual with evidence idempotency unchanged.
- Commands: report optional screenshot/note/{}; needs review+supplement after old deadline; exact total confirm +wrong amount no mutation; scope/revoked/suspended/Admin/proof; occurrence-ID attempt cannot mutate one member; idem/version/races report-expiry, confirm-reject; reject/expiry release full; reported/review never auto release; SLA/outbox retry/dedup once per group.
- Public availability every date shows reserved after create/confirm, releases every date on reject/expiry; customer/operator DTO/list/count/filter one group; old F05/F06 regression.
