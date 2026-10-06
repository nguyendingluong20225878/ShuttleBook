# F03 — Vận hành giờ, giá, QR và bảo trì theo sân

> Bổ sung 2026-10-06: owner đặt `bookingBlockMinutes` (30/60/90), `minimumBookingMinutes` (bội số block, đến 480), `holdMinutes` (5–60, mặc định 20) qua `PUT /operator/courts/{id}/booking-policy` có `If-Match`; GET operations trả các giá trị này. Preview kiểm tra min/block. F05 phải áp dụng hạn giữ trước báo chuyển; F04/F05/F07 dùng block 30 phút để hiện lịch/quote/series. Giá theo khách cố định/hội viên và sửa giá trong đơn chưa có booking/entitlement để thực thi trong F03; xem prompt `../prompts/F01-F03-post-acceptance-logic.md`.

Trạng thái: **IN_PROGRESS — code và kiểm thử local đạt; người dùng xác nhận nghiệm thu tay và tạm thời chấp thuận ngày 2026-10-06**. Contract chốt ngày 2026-10-05 trước khi code. F03 tích hợp trên F02, kiểm thử trên database PostGIS tạm; F02 vẫn còn nghiệm thu S3 private.

## Phạm vi và quyết định

- F02 đã lưu lịch tuần/giá cơ bản theo court và QR/tài khoản theo venue để gửi duyệt. F03 mở sửa vận hành **sau khi publish** cho owner active. Giá vẫn thuộc từng court, dùng ngày địa phương của venue và ca 30 phút. Không làm F04 availability, F05 booking/quote/payment hoặc F08 staff invitation.
- Giá F02 được nâng cấp thành quy tắc cơ bản `priority=0`, hiệu lực mọi ngày. F03 cho owner thay toàn bộ lịch tuần cùng giá cơ bản nguyên tử và thêm quy tắc ưu tiên `priority>0` có khoảng ngày `[startsOn,endsOn]`, một thứ trong tuần, khung giờ và giá/ca. Ca chọn quy tắc có priority cao nhất; cùng priority không được giao nhau về ngày lẫn giờ. Quy tắc ưu tiên chỉ nằm trong giờ mở; lịch cơ bản phải bao phủ toàn bộ giờ mở. Mỗi court có version để chống ghi đè đồng thời.
- Maintenance là khoảng UTC `[starts_at,ends_at)` lưu từ ngày/giờ địa phương venue. Không dùng trạng thái `MAINTENANCE` toàn court cho một khoảng ngắn: court vẫn `ACTIVE`, `court_allocations` loại `MAINTENANCE` giữ khoảng thời gian. F05 sẽ tạo allocation loại `BOOKING` trên cùng bảng/constraint. Không tạo maintenance trong quá khứ, không cho overlap cùng court; cancel đổi allocation sang `RELEASED`, giữ lịch sử/audit.
- QR/tài khoản sau publish chỉ đổi qua `POST /partner-onboarding/venues/{id}/revisions` của F02 và Admin duyệt. F03 không mở đường cập nhật trực tiếp. Bản đang published còn hiệu lực cho đến khi revision được duyệt.

## Actor và quyền

| Actor | Lịch/giá | Bảo trì | QR |
|---|---|---|---|
| Owner `VENUE_OPERATOR/ACTIVE`, membership `OWNER/ACTIVE`, business `ACTIVE`, venue `PUBLISHED` | Xem/sửa court trong scope | Tạo/cancel trong scope | Gửi revision chờ Admin |
| Pending/suspended owner, customer, owner business khác | 403 hoặc 404 ngoài scope | 403 hoặc 404 | Không tự publish |
| Admin | Không sửa giá owner | Chưa có UI quản trị bảo trì F03 | Duyệt revision theo F02 |

Mọi route xác minh user hiện tại từ DB và membership/resource scope. Không lấy businessId, ownerId, status hoặc quyền từ body.

## API và lỗi

Prefix `/api/v1`; JSON camelCase; thành công `{data,traceId}`, lỗi `application/problem+json` có `status`, `code`, `traceId`. `If-Match: "<version>"` bắt buộc khi PUT lịch/giá; thiếu `428 PRECONDITION_REQUIRED`, sai `412 PRECONDITION_FAILED`. Unknown/duplicate/missing field: `400 UNSUPPORTED_FIELD`/`VALIDATION_FAILED`.

| Route | Body / kết quả |
|---|---|
| `GET /operator/courts/{id}/operations` | Court/version, venue timezone, giờ/giá cơ bản và overrides; đúng owner scope. Maintenance lấy qua GET riêng bên dưới |
| `PUT /operator/courts/{id}/schedule` | `{hours:[{dayOfWeek,opensAt,closesAt}],prices:[{dayOfWeek,startsAt,endsAt,pricePerSlot}]}`; thay nguyên tử giờ/giá cơ bản, giữ override hợp lệ; tăng version |
| `PUT /operator/courts/{id}/pricing-rules` | `{rules:[{startsOn,endsOn,dayOfWeek,startsAt,endsAt,pricePerSlot,priority}]}`; thay nguyên tử toàn bộ override `priority>0`, giữ giá cơ bản; tăng version |
| `GET /operator/courts/{id}/price-preview?date=YYYY-MM-DD&startsAt=HH:mm&endsAt=HH:mm` | Danh sách ca và rule áp dụng, tổng VND; chỉ preview, không tạo quote/booking |
| `GET /operator/courts/{id}/maintenance` | Các khoảng bảo trì active/future của court trong scope |
| `POST /operator/courts/{id}/maintenance` | `{date,startsAt,endsAt,reason}` local venue; `201` maintenance + allocation; overlap `409 SLOT_CONFLICT` |
| `POST /operator/courts/{id}/maintenance/{maintenanceId}/cancel` | Không body; release allocation và audit; gọi lại trả cùng trạng thái không nhân đôi audit |

Ngày/giờ sai, giá không nguyên dương, giờ không trên lưới 30 phút, rule ngoài giờ mở hoặc thiếu giá cơ bản trả `400 VALIDATION_FAILED`. Preview ca ngoài giờ mở/thiếu giá trả `409 PRICE_UNAVAILABLE`. Court/maintenance ngoài scope `404 NOT_FOUND`; owner pending `403 FORBIDDEN`, token của owner bị suspend trả `401 UNAUTHORIZED` tại lớp xác thực F01; court chưa active hoặc venue chưa published `409 STATE_CONFLICT` khi thao tác. Collision transaction/range `409 SLOT_CONFLICT`; cập nhật bằng version cũ trả `412 PRECONDITION_FAILED`; lỗi DB không lộ SQL/PII.

## Thiết kế dữ liệu

- `courts.version bigint NOT NULL DEFAULT 1`, backfill an toàn cho court F02.
- `pricing_rules.starts_on date`, `ends_on date`, `priority integer` với dữ liệu F02 chuyển thành `0001-01-01` đến `9999-12-31`, priority 0. Check ngày, giờ, giá, priority. GiST exclusion chặn overlap cùng court/day/priority theo date range và time range, kể cả race.
- `court_allocations`: `id`, `court_id`, `kind` (`MAINTENANCE`/`BOOKING`), `starts_at`, `ends_at`, `status` (`RESERVED`/`RELEASED`), timestamps. Check end > start; GiST exclusion `(court_id WITH =, tstzrange(starts_at,ends_at,'[)') WITH &&) WHERE status='RESERVED'`.
- `court_maintenance`: `id`, `court_id`, `allocation_id` unique, `reason`, `status`, `created_by/at`, `cancelled_by/at`. FK không cascade delete; audit append-only. API tạo/cancel khóa court và ghi hai bảng cùng transaction.
- Không migrate/drop database development. Integration test tạo/dọn database tạm có tên được kiểm tra chặt, upgrade từ F02 có dữ liệu và chạy migration lặp.

## Acceptance

1. Owner active trong scope sửa được lịch/giá từng sân; pending/customer/owner khác không sửa được; đổi đồng thời không mất update.
2. Giá cơ bản bao phủ giờ mở theo ca 30 phút; override theo ngày/priority chọn đúng từng ca, rule cùng priority giao nhau bị DB/API từ chối; preview tổng đúng.
3. Migration giữ nguyên giá/lịch/QR F02, fresh/repeat/upgrade an toàn.
4. Maintenance lưu UTC theo timezone venue, không cho ca quá khứ/sai lưới/chồng allocation, kể cả race và SQL trực tiếp; cancel release và idempotent.
5. QR published chỉ thay sau revision Admin duyệt; partner UI không có bypass.
6. Partner UI desktop/mobile hoàn thành sửa lịch/giá, preview, tạo/cancel bảo trì qua API/PostGIS thật; không phá F02.
7. Build, API/DB/browser kiểm thử cần thiết PASS, không có lỗi nghiêm trọng. Chưa nghiệm thu S3/tay của F02 không được suy diễn thành F02 DONE.
