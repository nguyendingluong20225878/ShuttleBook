# F02 — testcase và bằng chứng

Trạng thái 2026-10-05: các testcase F02 dưới đây **PASS trên môi trường local** với PostgreSQL/PostGIS thật, API, Worker, browser desktop/mobile và adapter media file private. S3 private thật **NOT RUN** vì chưa có endpoint/bucket/IAM trong môi trường này; F02 giữ **IN_PROGRESS** theo điều kiện bàn giao của prompt. Không suy diễn kết quả local thành nghiệm thu S3 hoặc CI.

| ID | Mục tiêu / dữ liệu / bước | Kỳ vọng | Loại | Kết quả |
|---|---|---|---|---|
| F02-T01 | Pending owner đã xác minh tạo business hợp lệ; đọc DB | Một business DRAFT và một OWNER/PENDING cùng transaction | API+DB | **PASS** — flow và race DB |
| F02-T02 | Gửi ownerId/role/status ngoài contract; customer/no token | 400 unsupported hoặc 401/403; không tạo dữ liệu | API+DB | **PASS** — strict body, customer 403, 12 route auth |
| F02-T03 | Pending owner tạo nhiều hồ sơ đồng thời | Chỉ một hồ sơ chưa duyệt | DB concurrency | **PASS** — 1 Created, 1 Conflict |
| F02-T04 | Owner A đọc/sửa venue/court của B | 404, dữ liệu B không đổi | API+DB | **PASS** — business/venue/court scope |
| F02-T05 | Tạo venue địa chỉ/contact/tọa độ/timezone và court/giờ/giá | Lưu PostGIS chính xác, validate range và lưới 30 phút | DB+API | **PASS** — `POINT(106.7 10.8)`, lịch giá |
| F02-T06 | Upload ảnh/QR: type, size, checksum, scope, xác minh object và complete lặp | Chỉ object private hợp lệ thành READY, không gắn sai resource | API+adapter local | **PASS local** — S3 HEAD/PUT riêng NOT RUN |
| F02-T07 | Submit thiếu venue/court/giờ/giá/QR/ảnh | 409 INCOMPLETE_PROFILE, không đổi trạng thái | API+DB+UI | **PASS** — các bước thiếu và browser |
| F02-T08 | Submit đủ hồ sơ hai request đồng thời; thử sửa khi pending | Một approval snapshot, draft khóa | DB concurrency | **PASS** — 1 OK, 1 Conflict |
| F02-T09 | Customer/owner tự gọi Admin approve | 403, không đổi dữ liệu | API+DB | **PASS** — hai actor bị chặn |
| F02-T10 | Admin request-changes có/không lý do, owner sửa rồi resubmit | Lý do bắt buộc, về DRAFT, tạo request mới | API+DB+UI | **PASS** — DB và browser live |
| F02-T11 | Hai Admin approve/request-changes cùng request | Một quyết định; trạng thái nhất quán, audit một lần | DB concurrency | **PASS** — 1 OK, 1 Conflict |
| F02-T12 | Admin approve hợp lệ; partner login lại | User/business/member ACTIVE, venue PUBLISHED, court ACTIVE; UI vào hồ sơ | API+DB+browser | **PASS** — DB và browser live |
| F02-T13 | Danh sách public trước/sau duyệt | Draft/pending không lộ; không lộ payment account | API | **PASS F02** — F02 không đăng ký endpoint public venue; contract đọc F04 còn ở feature sau |
| F02-T14 | Sửa địa chỉ/contact/tài khoản sau publish và chờ duyệt | Bản published hiện hành còn nguyên tới khi Admin duyệt | API+DB | **PASS** — revision snapshot và DB trước/sau approve |
| F02-T15 | Migrate DB mới, chạy lại, nâng cấp từ F01 có dữ liệu | Không mất user/session; schema và PostGIS đúng | PostgreSQL migration | **PASS** — migrate 2 lần, nâng cấp F01 có session |
| F02-T16 | Partner/admin browser desktop/mobile qua API/DB/adapter local thật | Draft → submit → request changes → resubmit → approve | Browser E2E | **PASS** — 2 ca F02 live; cả script 8/8 |
| F02-T17 | Submit/quyết định tạo outbox; Worker retry và thông báo scoped | Notification đúng người, không nhân đôi, đánh dấu đã đọc có scope | DB+Worker+API | **PASS** — lỗi, retry, replay, read scope |
| F02-T18 | Provider Goong thật: tìm `31 ngõ 16 Hoàng Cầu - Hà Nội`, chọn suggestion, xem marker và tọa độ Place Detail | Gợi ý đúng địa chỉ Hà Nội, marker ở vị trí người dùng xác nhận, API nhận formatted address/lat/lng | External provider + browser | **SUPERSEDED** — Goong đã được thay bằng MapTiler theo yêu cầu mới; ca live hiện hành là F02-T21 |
| F02-T19 | Goong mock autocomplete/detail → xác nhận → lưu venue | UI bắt xác nhận; payload chứa đúng formatted address/latitude/longitude mock | Browser mock desktop/mobile | **SUPERSEDED** — kết quả Goong 6/6 là lịch sử; ca mock hiện hành là F02-T20 |
| F02-T20 | MapTiler Geocoding mock `31 ngõ 16 Hoàng Cầu - Hà Nội` → chọn suggestion → preview marker → xác nhận → lưu venue | Query country VN; formatted address và GeoJSON coordinates truyền đúng sang payload API sau xác nhận | Browser mock desktop/mobile | **PASS** — `npm.cmd run test:web -- tests/web/f02-onboarding.spec.ts`, 6/6; build dùng key giả và toàn bộ request MapTiler được mock |
| F02-T21 | MapTiler thật: tìm `31 ngõ 16 Hoàng Cầu - Hà Nội`, kiểm tra suggestion, marker và tọa độ | Gợi ý đúng địa chỉ; người dùng xác nhận marker; API nhận formatted address/latitude/longitude | External provider + browser | **PASS (người dùng xác nhận thủ công)** — người dùng báo đã nghiệm thu F02–F03 sau khi thêm key; gợi ý/toạ độ live cụ thể không được ghi lại |

Ghi lệnh, ngày, môi trường, PASS/FAIL/NOT RUN/BLOCKED và lý do sau từng lượt. Không ghi secret/token/contact thật.

## Bằng chứng lượt triển khai 2026-10-05

| Nhóm | Lệnh tại repo root PowerShell | Kết quả |
|---|---|---|
| Backend build | `dotnet build backend/ShuttleBook.slnx --no-restore` | **PASS** — 0 warning, 0 error; test DB F02 biên dịch |
| API host | `npm.cmd run test:api` | **PASS** — 70/70, trong đó 12 route F02 yêu cầu bearer; không thay thế DB integration |
| Frontend | `npm.cmd run typecheck`; `npm.cmd run build` | **PASS** — ba portal TypeScript/build |
| Browser mock | `npm.cmd run test:web` | **PASS** — 32, **SKIP** 8 ca live chủ đích; F02 UI desktop/mobile |
| DB/PostGIS | `npm.cmd run test:db -- --logger "console;verbosity=normal"` | **PASS** — 20/20 trên PostgreSQL/PostGIS thật; chạy trước khi thêm testcase Worker retry |
| F02 DB mở rộng | `npm.cmd run test:db -- --filter FullyQualifiedName~OnboardingFlowTests --logger "console;verbosity=normal"` | **PASS** — 6/6 sau khi thêm ca Worker retry và các assertion âm |
| Browser live | `npm.cmd run test:identity-live` | **PASS** — 8/8 qua API/PostgreSQL/PostGIS/Mailpit/adapter local; desktop/mobile, database tạm được dọn đúng tên |
| S3 private thật | Cấu hình S3/IAM và object store riêng | **NOT RUN** — môi trường local dùng adapter file private; cần môi trường S3 tương thích để nghiệm thu provider |

Còn lại để chốt F02 DONE theo prompt: nghiệm thu presigned PUT/HEAD/GET, checksum, bucket private và CORS trên S3 hoặc object store tương thích được cấu hình riêng; sau đó ghi lệnh/kết quả ở đây. Không chạy thử trên dữ liệu hoặc bucket production khi chưa được phép.
