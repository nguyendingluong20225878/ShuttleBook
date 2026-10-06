# F02 — Chủ sân khai báo và Admin duyệt

> Bổ sung 2026-10-06: partner UI dùng MapTiler SDK + Geocoding API để gợi ý, chọn và xác nhận địa chỉ khi tạo/sửa venue hoặc gửi revision; không yêu cầu chủ sân nhập vĩ/kinh độ. API giữ DTO address/lat/lng và lưu tọa độ PostGIS. Cần một MapTiler API key trong local `.env`; smoke test provider thật chưa chạy. Xem `../setup.md` và `../prompts/F01-F03-post-acceptance-logic.md`.

Trạng thái 2026-10-05: **IN_PROGRESS** theo điều kiện bàn giao S3; luồng local trên PostgreSQL/PostGIS và adapter media private đã PASS. Contract được chốt trước khi triển khai.

## Phạm vi và quyết định

- `VENUE_OPERATOR/PENDING_ONBOARDING` đã xác minh tạo business `DRAFT`; transaction đồng thời gắn `OWNER/PENDING` cho chính user trong token. Owner quản lý venue/court nháp trong business của mình. Mỗi pending owner chỉ có một hồ sơ chưa được duyệt tại một thời điểm để hạn chế hồ sơ rác.
- F02 thu thập địa chỉ, contact riêng của venue, tọa độ WGS84, timezone IANA, ảnh cơ sở, QR nhận tiền và giá/giờ nháp đủ để Admin xét hồ sơ. F03 sẽ mở cấu hình vận hành, nhưng **không có đường tắt duyệt khi thiếu QR, giờ hoặc giá**. F02 lưu lịch tuần và giá theo court để F03 kế thừa; F03 phải bổ sung hiệu lực theo ngày/ưu tiên quy tắc giá trước khi tạo booking.
- Owner submit toàn bộ business và các venue nháp hợp lệ trong một approval request. Khi chờ duyệt, nội dung nháp bị khóa. Admin yêu cầu sửa thì trả toàn bộ về `DRAFT` kèm lý do; duyệt thì business/membership/user `ACTIVE`, venue `PUBLISHED`, court `ACTIVE` trong một transaction.
- Thay đổi địa chỉ hoặc tài khoản nhận tiền sau publish phải đi qua revision chờ duyệt, trong khi bản published hiện hành tiếp tục phục vụ. F02 không nhận booking, nearby, mời nhân viên, maintenance hoặc thao tác thanh toán.

## Actor, quyền và trạng thái

| Actor | Quyền F02 |
|---|---|
| Pending owner đã xác minh | Tạo business của mình, sửa draft, gửi duyệt, xem lý do trả sửa |
| Active owner | Xem business đã duyệt, tạo revision nhạy cảm để gửi lại duyệt; không tự publish |
| Admin active | Xem hồ sơ pending, phê duyệt hoặc yêu cầu sửa |
| Customer, operator khác, token hết hạn | Không sửa/duyệt hồ sơ; resource ngoài scope trả 404 |

Mọi lệnh dùng user hiện tại từ DB, kiểm tra account type/status và owner membership. Không lấy owner/scope/status từ body hoặc chỉ tin JWT claim.

## Dữ liệu và transaction

Migration tăng dần tạo `businesses`, `business_memberships`, `venues` (PostGIS `geography(Point,4326)`), `courts`, `court_operating_hours`, `pricing_rules`, `venue_payment_accounts`, `media_uploads`, `approval_requests`, `outbox_messages` và `notifications`. F02 dùng một `venues.image_upload_id` cho ảnh đại diện; bảng album `venue_images` và tiện ích trong tài liệu kiến trúc được để F04. FK, unique, check constraint và index bảo vệ quan hệ, một request pending/business và giá theo slot 30 phút. Snapshot approval dùng `jsonb`; audit dùng `audit_events` F01. S3 object private có key server cấp, checksum và trạng thái `PENDING` → `READY` sau HEAD và đọc lại object để kiểm tra byte ảnh. Không lưu presigned URL trong snapshot.

Submit, request-changes và approve khóa business/approval row trong transaction PostgreSQL. Kiểm tra lại dữ liệu tại approve; quyết định lặp là conflict hoặc trả kết quả cũ mà không nhân đôi side effect. Giao dịch thất bại không được để business/venue/membership ở trạng thái nửa chừng.

## API và lỗi

Prefix `/api/v1`, JSON camelCase và envelope `{data,traceId}`. Body strict allowlist; field `ownerId`, `role`, `status`, `accountType`, `businessId` ngoài đúng route bị `400 UNSUPPORTED_FIELD`.

| Route | Đầu vào / kết quả chính |
|---|---|
| `POST /partner-onboarding/businesses` | Tên, contact/pháp lý; `201` business DRAFT + owner membership |
| `GET /partner-onboarding/businesses` và `GET /partner-onboarding/businesses/{id}` | Hồ sơ đúng owner, venue/court/trạng thái/lý do sửa |
| `PUT /partner-onboarding/businesses/{id}` | Sửa business DRAFT, version guard |
| `POST /partner-onboarding/businesses/{id}/venues` | Tạo venue DRAFT với địa chỉ, contact, lat/lng, timezone |
| `PUT /partner-onboarding/venues/{id}` | Sửa venue DRAFT đúng owner |
| `POST /partner-onboarding/venues/{id}/courts`, `PUT /partner-onboarding/courts/{id}` | Khai báo court, giờ và giá theo ca |
| `PUT /partner-onboarding/venues/{id}/payment-account` | Tài khoản nhận tiền và upload QR READY |
| `POST /uploads/presign`, `POST /uploads/{id}/complete` | Cấp PUT ngắn hạn và xác minh object đúng owner/resource |
| `POST /partner-onboarding/businesses/{id}/submit` | Validate hoàn chỉnh, tạo snapshot + pending approval |
| `GET /admin/approval-requests/`, `GET /admin/approval-requests/{id}` | Tối đa 100 hồ sơ pending và chi tiết snapshot/trạng thái một hồ sơ |
| `POST /admin/approval-requests/{id}/approve` | Admin active, một quyết định duy nhất |
| `POST /admin/approval-requests/{id}/request-changes` | Admin active, lý do bắt buộc |
| `POST /partner-onboarding/venues/{id}/revisions` | Địa chỉ/tọa độ/timezone và tài khoản/QR mới; giữ bản published tới lúc duyệt |
| `GET /me/notifications/`, `POST /me/notifications/{id}/read` | Thông báo in-app đúng user từ transactional outbox |

Lỗi Problem Details: `401 UNAUTHORIZED`, `403 FORBIDDEN`, `404 NOT_FOUND` cho resource ngoài scope, `400 VALIDATION_FAILED`/`UNSUPPORTED_FIELD`, `409 STATE_CONFLICT`/`INCOMPLETE_PROFILE`/`VERSION_CONFLICT`, `412 PRECONDITION_FAILED`, `428 PRECONDITION_REQUIRED`, `409 UPLOAD_NOT_FOUND`/`UPLOAD_MISMATCH`, `429 RATE_LIMITED`, `503 MEDIA_UNAVAILABLE`. Không trả số tài khoản đầy đủ ở endpoint public.

### DTO và UI thực thi

- Business create/update: `{name,legalName,contact}`; update cần `If-Match: "<version>"`. Venue create/update: `{name,address,contact,timezone,latitude,longitude}`; `contact` là liên hệ riêng của cơ sở, update cũng cần `If-Match`. Court create/update: `{name}`. Body phải có đúng field đã liệt kê; field lạ, field trùng hoặc thiếu bị từ chối.
- Schedule `PUT /courts/{id}/schedule`: `{hours:[{dayOfWeek,opensAt,closesAt}],prices:[{dayOfWeek,startsAt,endsAt,pricePerSlot}]}`. Ngày 0–6 (Chủ nhật–Thứ bảy); `HH:mm` trên lưới 30 phút. Mỗi khoảng giờ mở phải được các khung giá liền nhau bao phủ hoàn toàn, giá nguyên dương theo ca 30 phút. Đây là lịch tuần phục vụ duyệt F02; F03 sẽ mở rộng cấu hình giá theo ngày/ưu tiên khi vận hành.
- Media presign: `{venueId,purpose,contentType,sizeBytes,sha256Base64}` với purpose `QR` hoặc `VENUE_IMAGE`, PNG/JPEG/WebP tối đa 5 MiB. Response gồm `id`, `uploadUrl`, `uploadHeaders`, hạn 5 phút; client PUT byte ảnh đúng header rồi gọi complete. `PUT /venues/{id}/image` nhận `{uploadId}`; payment account nhận `{bankCode,accountName,accountNumber,qrUploadId}`. Không nhận object key/URL từ client để gắn ảnh.
- Revision nhận `{address,contact,timezone,latitude,longitude,bankCode,accountName,accountNumber,qrUploadId}`. Admin request changes nhận `{reason}` dài 10–1000 ký tự; approve và submit không có body.
- Partner UI hiện draft, pending, lý do sửa, active và revision pending; Admin UI hiện danh sách trống/có hồ sơ, snapshot, ảnh private, quyết định và thông báo. Access/refresh token chỉ giữ trong React state. Thông báo được ghi cùng quyết định/gửi duyệt qua outbox, Worker retry và unique `(outbox_message_id,user_id)` ngăn nhân đôi.

Local Development/Testing dùng adapter file private ở `.media-local/` có HMAC URL, kích thước, SHA-256 và magic bytes. Production dùng `Media:Mode=S3` với region/bucket private và IAM credentials mặc định của SDK; cấu hình thiếu thì không khởi động S3. S3 PUT/CORS và bucket policy cần được nghiệm thu trên môi trường S3 tương thích trước production. F02 không có endpoint public venue; F04 phải chỉ đọc `PUBLISHED` cùng court `ACTIVE` và không trả payment account.

## Acceptance

1. Tạo business và membership OWNER/PENDING nguyên tử, không thể tự gán quyền hoặc chiếm business khác.
2. Draft business → venue → court → giờ/giá/QR/ảnh đúng scope; dữ liệu địa lý lưu bằng PostGIS.
3. Thiếu thông tin bắt buộc không submit; submit đúng tạo snapshot, khóa nháp và duy nhất một request pending.
4. Admin duyệt/yêu cầu sửa đúng quyền, transaction và audit; race/lặp không nhân đôi quyết định.
5. Approved chỉ kích hoạt đúng owner, business, venue và court hợp lệ; partner login nhận cả `ACTIVE`.
6. Hồ sơ draft/pending không lộ ở API công khai hoặc nhận booking. Revision nhạy cảm không thay dữ liệu đã publish trước duyệt.
7. Upload private xác minh MIME, size, checksum, scope và object tồn tại trước khi gắn.
8. Migration fresh/repeat/upgrade, API/DB/browser runtime, review và kiểm thử rủi ro đạt; không còn lỗi nghiêm trọng.

Testcase chi tiết và bằng chứng: `docs/testing/F02-test-cases.md`.
