# API, bảo mật và kế hoạch triển khai

## 1. Quy ước API

- Base path: `/api/v1`.
- JSON dùng camelCase.
- Thời điểm dùng ISO-8601 UTC; response lịch kèm timezone/local label khi cần.
- Command quan trọng nhận `Idempotency-Key`.
- Update trạng thái nhận `If-Match` hoặc `version`.
- Lỗi dùng RFC Problem Details với `code`, `status`, `detail`, `traceId` và validation errors.
- Danh sách dùng cursor pagination; limit mặc định 20, tối đa 100.
- API production: `https://api.tenmien.vn/api/v1`; chỉ cho phép CORS từ `https://tenmien.vn`, `https://partner.tenmien.vn` và `https://admin.tenmien.vn`.

## 2. API catalog

| Module | Method và route | Quyền | Mục đích |
|---|---|---|---|
| Auth | `POST /auth/register` | Public | Tạo `CUSTOMER/PENDING_VERIFICATION`; server tự gán role |
| Auth | `POST /auth/verify-contact` | Public | Xác minh OTP và chuyển customer sang `ACTIVE` |
| Auth | `POST /auth/verification-resend` | Public | Gửi lại OTP với phản hồi chống dò account |
| Auth | `POST /auth/login` | Public | Access + refresh token |
| Auth | `POST /auth/refresh` | Refresh token | Rotate token family |
| Auth | `POST /auth/logout` | Authenticated | Revoke session |
| Partner Auth | `POST /partner-auth/register` | Public | Tạo `VENUE_OPERATOR/PENDING_ONBOARDING` |
| Partner Auth | `POST /partner-auth/verify` | OTP/link | Xác minh contact của chủ sân |
| Partner Onboarding | `POST /partner-onboarding/businesses` | Verified pending operator | Tạo business nháp và owner membership |
| Partner Onboarding | `POST /partner-onboarding/businesses/{id}/venues` | Business owner | Khai báo cơ sở/sân nháp |
| Partner Onboarding | `POST /partner-onboarding/businesses/{id}/submit` | Business owner | Gửi hồ sơ cho Admin duyệt |
| Operator | `POST /operator-invitations/accept` | One-time token | Nhân viên nhận lời mời vận hành |
| Venues | `GET /venues` | Public | Tìm theo khu vực và filter |
| Venues | `GET /venues/nearby` | Public | Chỉ tìm cơ sở gần theo lat/lng và radius; trả thông tin cơ bản, vị trí, khoảng cách |
| Venues | `GET /venues/{id}` | Public | Chi tiết venue published |
| Availability | `GET /venues/{id}/availability` | Public | Các ca 30 phút còn trống |
| Availability | `POST /availability/quote` | Public/Customer | Tính lại giá có expiry |
| Pricing | `PUT /operator/courts/{id}/pricing-rules` | Owner có business membership active bao phủ court | Cấu hình bảng giá riêng cho sân theo ngày/khung giờ; kiểm tra quyền sở hữu, độ ưu tiên và biên ca 30 phút |
| Bookings | `POST /bookings` | Customer | Đặt vãng lai bằng các ca liên tiếp |
| Series | `POST /booking-series/quote` | Customer | Preview lịch tuần từ 1 tháng và conflict |
| Series | `POST /booking-series` | Customer | Tạo lịch cố định, mỗi buổi từ 2 giờ |
| Series | `GET /booking-series/{id}` | Customer/operator scope | Xem series và occurrence |
| Bookings | `GET /me/bookings` | Customer | Lịch sử của tôi |
| Bookings | `GET /bookings/{id}` | Booking owner/operator scope | Chi tiết và timeline |
| Payment F05 | `GET /bookings/{id}/qr` | Customer sở hữu booking | QR private theo snapshot, no-store |
| Payment | `POST /bookings/{id}/transfer-evidence` | Booking owner | Báo đã chuyển khoản |
| Payment | `GET /payments/{id}` | Customer/operator scope | Xem trạng thái payment |
| Operator | `GET /operator/businesses/{id}` | Active business membership | Xem doanh nghiệp và các cơ sở được phép |
| Operator | `POST /operator/businesses/{id}/venues` | `venue.create` | Tạo cơ sở ở trạng thái `DRAFT` |
| Operator | `POST /operator/venues/{id}/submit` | `venue.submit` | Gửi cơ sở để Admin duyệt |
| Operator | `POST /operator/businesses/{id}/invitations` | `member.invite` | Mời nhân viên vào business/venue scope |
| Operator | `GET /operator/venues/{id}/bookings` | Active membership | Danh sách vận hành |
| Operator | `POST /operator/bookings/{id}/confirm-payment` | `payment.confirm` | Xác nhận `PAID` |
| Operator | `POST /operator/bookings/{id}/reject-payment` | `payment.confirm` | Review hoặc từ chối |
| Operator | `PUT /operator/venues/{id}/payment-account` | `payment-account.manage` | Cập nhật tài khoản/QR |
| Operator | `POST /operator/courts/{id}/maintenance` | `court.maintenance.manage` | Khóa court bảo trì |
| Operator | `GET /operator/venues/{id}/revenue` | `report.read` | Doanh thu theo venue |
| Reviews | `POST /bookings/{id}/review` | Booking owner | Một review tùy chọn khi booking `CONFIRMED` đã qua `ends_at` |
| Uploads | `POST /uploads/presign` | Resource scope | Presigned PUT URL |
| Uploads | `POST /uploads/{id}/complete` | Cùng actor/scope | Verify và liên kết object |
| Admin | `GET /admin/approval-requests` | `venue.approve` | Danh sách hồ sơ chờ duyệt |
| Admin | `POST /admin/approval-requests/{id}/approve` | `venue.approve` | Duyệt business/venue và kích hoạt owner |
| Admin | `POST /admin/approval-requests/{id}/request-changes` | `venue.approve` | Trả hồ sơ kèm nội dung cần sửa |
| Admin | `POST /admin/business-memberships/{id}/revoke` | Admin | Thu hồi quyền toàn doanh nghiệp |
| Admin | `POST /admin/venue-memberships/{id}/revoke` | Admin | Thu hồi quyền |
| Admin | `GET /admin/disputes` | Admin | Tranh chấp thanh toán chưa được xác nhận |
| Admin | `POST /admin/disputes/{id}/decision` | Admin + step-up | Ra quyết định có audit |

Customer không có endpoint cancel/reschedule.

## 3. Contract tạo booking vãng lai

Contract F05 đã triển khai: authenticated Customer ACTIVE `POST /api/v1/availability/quote` nhận `{courtId,date,startsAt,endsAt}` với ngày `YYYY-MM-DD` và giờ địa phương `HH:mm`. Quote có hiệu lực 120 giây, horizon 60 ngày theo timezone venue và giữ chỗ tạm đến expiresAt. Response có `quoteId`, expiry, interval UTC, từng ca/giá, tổng VND và policy block/minimum/hold. Tạo đơn cần Customer ACTIVE; operator scope ở bảng catalog thuộc F06, chưa được cấp trong F05.

```http
POST /api/v1/bookings
Authorization: Bearer <access-token>
Idempotency-Key: 0199fca5-...
Content-Type: application/json

{
  "courtId": "0199fc70-...",
  "startsAt": "2026-10-01T11:00:00Z",
  "endsAt": "2026-10-01T13:00:00Z",
  "quoteId": "0199fc80-..."
}
```

```json
{
  "data": {
    "bookingId": "0199fca6-...",
    "bookingNo": "BK2610018F2Q",
    "status": "AWAITING_TRANSFER",
    "paymentDeadline": "2026-09-22T10:25:30Z",
    "amount": 240000,
    "currency": "VND",
    "payment": {
      "bankCode": "VCB",
      "accountName": "NGUYEN VAN A",
      "maskedAccountNumber": "******6789",
      "qrUrl": "/api/v1/bookings/0199fca6-.../qr",
      "transferContent": "BK2610018F2Q"
    },
    "version": 1
  },
  "traceId": "00-a1b2..."
}
```

Kết quả chính: `201`; slot conflict `409 SLOT_UNAVAILABLE`; quote hết hạn `409 QUOTE_EXPIRED`; giá/policy/QR thay đổi `409 QUOTE_CHANGED` yêu cầu báo giá mới; cùng idempotency key nhưng request khác `409 IDEMPOTENCY_KEY_REUSED`. Retry cùng key/body trả lại booking hiện có kể cả quote cũ đã hết hạn. Booking code F05 gồm `BK` + ngày tạo `yyMMdd` + UUID không dấu, không phụ thuộc ví dụ code rút gọn trên.

`startsAt` và `endsAt` phải nằm trên biên ca 30 phút **địa phương venue**; các ca liên tiếp và thời lượng đạt `minimumBookingMinutes`. Theo nghiệm thu 2026-10-07, đặt vãng lai được thêm từng ca 30 phút sau minimum, không yêu cầu tổng chia hết `bookingBlockMinutes`. Sân minimum 120 phút chấp nhận 4, 5, 6, 7... ca còn trống/có giá. Ví dụ 18:00–20:30 tương ứng năm ca nhưng API vẫn lưu một khoảng `[18:00,20:30)`. Deadline bắt đầu khi tạo đơn, theo `holdMinutes` snapshot (mặc định 20 phút). Múi giờ có offset lẻ không bị ép UTC phút 00/30; DST ambiguous/invalid bị từ chối.

Payment snapshot lưu account/QR upload/object key/checksum, không lưu signed URL. `qrUrl` là route private ổn định, UI fetch bằng bearer; local trả bytes, S3 cấp signed GET 5 phút khi có cấu hình. List không có tài khoản/QR, detail mask số tài khoản. Giá và QR đơn cũ giữ nguyên sau owner sửa cấu hình. Contract đầy đủ: `docs/features/F05-casual-booking.md`.

### Contract quote lịch cố định

```http
POST /api/v1/booking-series/quote
Authorization: Bearer <access-token>
Content-Type: application/json

{
  "courtId": "0199fc70-...",
  "dayOfWeek": "TUESDAY",
  "localStartTime": "18:00",
  "durationMinutes": 120,
  "startsOn": "2026-10-01",
  "endsOn": "2026-11-01"
}
```

`durationMinutes` tối thiểu `120` và chia hết cho `30`; `endsOn` phải bằng hoặc sau `startsOn` cộng một tháng. Quote trả toàn bộ occurrence, tổng tiền và danh sách xung đột. `POST /booking-series` nhận `quoteId`; khi thành công, tất cả occurrence được giữ trong cùng transaction.

F07 duyệt 2026-10-07: FULL_SERIES100%, cửa sổ trong60ngày địa phương/max12buổi, quote120s/holdMinutes sân. Quote xung đột allocation trả200 preview+conflicts/canCreate=false/quoteId=null, không giữ chỗ một phần; quote hợp lệ giữ toàn kỳ đến expiresAt. Create xung đột trả409 SERIES_CONFLICT và rollback toàn kỳ. Response tạo đơn dùng `bookingId` payment anchor và DTO booking có `series`/occurrences; amount/payment.expectedAmount là tổng cả kỳ. List một dòng mỗi nhóm, datefilter owner match bất kỳ buổi. Chi tiết API/error/privacy/lock/migration ở `features/F07-designer-notes.md`.

## 4. Contract báo đã chuyển khoản

```http
POST /api/v1/bookings/0199fca6-.../transfer-evidence
Authorization: Bearer <access-token>
Idempotency-Key: 0199fd00-...
If-Match: "1"

{
  "proofUploadId": "0199fca6-0000-7000-8000-000000000001",
  "note": "Chuyển từ tài khoản NGUYEN VAN B"
}
```

Response trả `AWAITING_OWNER_CONFIRMATION`, giao diện hiển thị **Chờ xác nhận**. Transaction đồng thời ghi outbox để gửi thông báo trong ứng dụng đến chủ sân của booking, kèm liên kết chi tiết. Quá deadline trả `409 PAYMENT_DEADLINE_EXPIRED`; version mismatch trả `412 PRECONDITION_FAILED`.

Contract F06 chi tiết và trạng thái triển khai ở `docs/features/F06-payment-confirmation.md`. `proofUploadId` phải READY/PAYMENT_PROOF và gắn đúng booking/customer/venue; key do server tra, không nhận key/path/URL của client. Cùng endpoint xử lý bổ sung từ NEEDS_REVIEW, không áp dụng deadline giữ chỗ ban đầu. Evidence/history append-only. Policy theo nghiệm thu 2026-10-07: bỏ ô mã giao dịch customer/owner, ảnh chụp màn hình/ghi chú tùy chọn; API giữ bankReference tùy chọn và chuẩn hóa bỏ/null/trắng thành null cho tương thích dữ liệu/client cũ. Report cho phép `{}`. Confirm đúng expectedAmount, SLA 30 phút từ báo chuyển đầu tiên tới owner + Admin một lần, không giải phóng sân khi chậm xác nhận.

## 5. Contract operator xác nhận

- Chỉ operator có active business membership hoặc venue membership bao phủ cơ sở của booking và có `payment.confirm`.
- Request có `Idempotency-Key`, `If-Match`, confirmed amount; bank reference/note tùy chọn. UI owner không yêu cầu nhập mã giao dịch theo nghiệm thu 2026-10-07.
- Transaction lock booking/payment, ghi `confirmed_by`, `confirmed_at`, payment `PAID`, booking `CONFIRMED` và outbox.
- Response thành công trả booking `CONFIRMED`; cổng khách và cổng chủ sân hiển thị **Đã xác nhận** khi tải/cập nhật trạng thái, outbox gửi thông báo kết quả đến khách.
- Reject bắt buộc `reasonCode`; terminal reject mới release allocation.
- F06 phân biệt body `{resolution,reasonCode,reason}` với `resolution=NEEDS_REVIEW|FINAL_REJECTION`; cả hai có lý do. Confirm body `{confirmedAmount,note?,bankReference?}`; các command cần Idempotency-Key và booking If-Match. Operator reads dùng route `/operator/bookings/{id}` riêng, backend kiểm OWNER ACTIVE đúng business/venue; không mở GET customer F05 cho mọi operator.

## 6. Security review

| Khu vực | Yêu cầu |
|---|---|
| Authentication | Access token 5-15 phút; refresh rotation; token hash; revoke family khi reuse |
| Account type | Customer endpoint tạo customer; partner endpoint tạo pending operator; client không được gửi/chọn account type |
| Partner onboarding | Xác minh contact; một pending operator chỉ tạo business thuộc chính mình; rate limit và chống hồ sơ rác |
| Approval | Chỉ Admin có `venue.approve`; lock request khi xử lý; quyết định có lý do, snapshot và audit |
| Invitation | Chỉ dùng mời nhân viên; token ngẫu nhiên, chỉ lưu hash, dùng một lần, có expiry và có thể revoke |
| Authorization | Kiểm tra account type + active business/venue membership + permission + resource scope |
| Brute force | Rate limit theo IP/identifier, delay tăng dần, lỗi generic |
| Input | DTO allowlist, giới hạn length/range/enum, EF Core parameterized query |
| XSS/CSRF | React escaping, CSP; nếu dùng cookie thì SameSite + anti-forgery |
| CORS | Chỉ allowlist ba HTTPS origin của customer/partner/admin; không wildcard với credentials |
| Upload | Private bucket, server-generated key, size/type/checksum, verify sau upload |
| S3 | Block Public Access, encryption, least-privilege IAM, presigned URL ngắn hạn |
| Payment | Mask tài khoản theo role; ảnh biên lai private; booking code duy nhất; audit xác nhận |
| Audit | Append-only, actor/action/entity/correlation/time; không ghi secret |
| Logging | Redact PII/token/account number/evidence; TTL và quyền truy cập rõ ràng |
| Availability | Rate limit, timeout, circuit breaker, DB pool limit và backpressure |

## 7. Traceability Matrix

| Yêu cầu | Use Case/API | Dữ liệu | Test quan trọng |
|---|---|---|---|
| Không trùng lịch | Create booking/series | allocations, bookings | 20 request đồng thời chỉ một thành công |
| Lưới ca 30 phút | Quote/create | allocations, booking series | Từ chối mốc 18:10 hoặc duration 45 phút |
| Tìm sân gần nhất | `GET /venues/nearby` | venue location, trạng thái published | Khoảng cách, bán kính và fallback location; không yêu cầu ngày/giờ, không kiểm tra slot; chọn sân để vào luồng đặt chung |
| Đặt cố định | Series quote/create | series, bookings, allocations | Tối thiểu 2 giờ/1 tháng; all-or-none; khóa đúng mọi occurrence |
| Customer báo chuyển | transfer-evidence | payment, evidence, outbox | Retry không tạo notification lặp |
| Operator xác nhận đúng scope | confirm-payment | payment, business/venue membership, audit | Cơ sở B không xác nhận cơ sở A nếu ngoài scope |
| Auto-expiry an toàn | Worker | booking, allocation | Race với transfer report có đúng một transition |
| Customer không thể hủy/đổi | Chỉ GET status | policy snapshot | Không có route/action cho customer |
| Invitation an toàn | accept invitation | user, invitation, membership | Token hết hạn/đã dùng bị từ chối |
| Chủ sân tự đăng ký | partner onboarding/approval | user, business, venue, approval request | Hồ sơ chưa duyệt không xuất hiện public hoặc nhận booking |

## 8. ADR chính

| ADR | Quyết định |
|---|---|
| ADR-001 | Modular monolith cho MVP |
| ADR-002 | PostgreSQL là source of truth cho booking/payment |
| ADR-003 | Unified court allocation + GiST exclusion constraint |
| ADR-004 | Transactional outbox và idempotency record |
| ADR-005 | UUIDv7 cho aggregate IDs |
| ADR-006 | UTC cho instant, IANA timezone cho venue |
| ADR-007 | S3 private + presigned upload |
| ADR-008 | Customer và partner có endpoint đăng ký riêng; server tự gán account type; partner phải qua onboarding approval |
| ADR-009 | PostGIS cho nearby search; không lưu location customer lâu dài |
| ADR-010 | Booking cố định lặp một khung giờ hàng tuần, tối thiểu 2 giờ/buổi và kéo dài ít nhất 1 tháng |
| ADR-011 | Thanh toán QR 100%; operator xác nhận thủ công |
| ADR-012 | Ba React app độc lập: customer, partner và admin; dùng chung một API |
| ADR-013 | Domain: `tenmien.vn`, `partner.tenmien.vn`, `admin.tenmien.vn`, `api.tenmien.vn` |
| ADR-014 | MapTiler cho giao diện/geocoding địa chỉ; PostGIS là nguồn truy vấn sân gần nhất |
| ADR-015 | Frontend trên S3 private + CloudFront; API/Worker trên EC2; PostgreSQL trên RDS |
| ADR-016 | Cả vãng lai và cố định dùng lưới ca 30 phút; allocation vẫn lưu khoảng thời gian liên tục |

## 9. Roadmap

### Giai đoạn 0 - Chốt nghiệp vụ

- Chốt payment deadline, SLA operator, payment plan series và booking horizon.
- Chốt MapTiler và kênh notification; cấu hình quota và giới hạn API key theo domain.
- Hoàn thiện wireflow, threat model và acceptance criteria.

### Giai đoạn 1 - Nền tảng

- Identity, refresh rotation, customer/partner registration và staff invitation.
- Partner onboarding, business owner membership, venue approval, court, operating hours, pricing, images và payment account.
- PostGIS location và map search.

### Giai đoạn 2 - Booking core

- Availability theo ca 30 phút, quote, future date và casual booking.
- Exclusion constraint, idempotency, payment deadline và expiry worker.
- Operator booking list.

### Giai đoạn 3 - Thanh toán QR

- Transfer evidence, S3 upload, outbox notification.
- Operator confirm/reject/needs-review.
- SignalR/polling và cảnh báo quá SLA.

### Giai đoạn 4 - Booking cố định và vận hành

- Series tối thiểu 2 giờ/1 tháng, quote/create all-or-none, conflict preview và payment plan.
- Đánh giá tùy chọn, doanh thu và tranh chấp thanh toán chưa được xác nhận. Luồng booking kết thúc tại `CONFIRMED`; không triển khai API check-in/check-out hoặc cập nhật hoàn thành/vắng mặt.

### Giai đoạn 5 - Production

- Load/security test, backup/restore drill và migration rehearsal.
- Route 53, bốn domain HTTPS, ba CloudFront/S3 frontend, blue-green API deploy, monitoring và budget alarm.

## 10. Definition of Done cho booking core

- Mọi command có authorization, validation, idempotency và audit phù hợp.
- Concurrent tests chứng minh không double booking khi chạy nhiều API instance.
- Series all-or-none và timezone boundary được kiểm thử.
- API từ chối thời gian lệch lưới 30 phút; series dưới 2 giờ hoặc dưới 1 tháng.
- Duplicate evidence và duplicate confirmation không tạo side effect lặp.
- UI hiển thị rõ ngày/giờ local, QR, countdown và trạng thái xác nhận.
- Log có correlation ID và không lộ secret/PII.
- Backup, restore và rollback/compensation plan đã được diễn tập trên staging.
