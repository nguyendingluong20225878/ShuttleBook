# Prompt triển khai F03 — cấu hình vận hành sân

Tiếp tục từ code F02 hiện có trong ShuttleBook. Đọc `AGENTS.md`, `docs/process.md`, `docs/progress.md`, tài liệu nghiệp vụ `docs/README.md`, `docs/00-*.md`–`docs/05-*.md`, đặc tả/testcase F02 và kiểm tra Git trước khi sửa. Giữ mọi thay đổi F02 chưa commit; không migrate/drop database development, commit, push hoặc deploy.

## Mục tiêu

Hoàn thành F03 theo lát cắt Database → API → partner UI → kiểm thử trên PostgreSQL/PostGIS tạm:

1. Chỉ `VENUE_OPERATOR/ACTIVE` có `business_membership OWNER/ACTIVE` trên business `ACTIVE` và venue `PUBLISHED` mới sửa lịch vận hành, giá, bảo trì của court trong scope. Backend đọc user hiện tại từ DB, không tin riêng JWT/client. Customer/pending owner/owner khác bị chặn; resource ngoài scope trả 404. Admin không được tự sửa giá của owner.
2. Kế thừa `court_operating_hours` và `pricing_rules` F02. Mỗi court có lịch tuần/giá cơ bản theo ca 30 phút. F03 bổ sung khoảng ngày hiệu lực và độ ưu tiên cho giá; quy tắc ưu tiên cao nhất áp dụng cho từng ca, không cho hai quy tắc cùng priority giao nhau trong ngày/khung giờ. Không xóa hoặc viết lại giá nháp/đã duyệt khi migrate. Cập nhật lịch/giá nguyên tử, có version guard để tránh ghi đè thao tác đồng thời. Giá VND nguyên dương, khung giờ thẳng lưới 30 phút, không để giờ mở thiếu giá cơ bản.
3. Tạo `court_allocations` và `court_maintenance` cho bảo trì. Khoảng `[start,end)` UTC, cùng court không được có hai allocation `RESERVED` chồng nhau; PostgreSQL exclusion constraint dùng GiST và `btree_gist`. Tạo/cancel bảo trì trong transaction, audit, conflict trả 409; lịch quá khứ và time không thẳng lưới bị từ chối. Không tạo booking/quote/customer availability ở F03, nhưng schema allocation phải dùng lại được ở F05.
4. QR và tài khoản nhận tiền đã có trong F02: sau publish tiếp tục dùng revision chờ Admin duyệt, giữ bản published hiện hành trong lúc chờ. Partner UI F03 dẫn owner tới luồng này; không tạo endpoint thay QR trực tiếp hoặc lộ account đầy đủ ra public.
5. Thêm API `/api/v1/operator/courts/{id}/...` theo đặc tả F03: GET cấu hình, PUT lịch tuần, PUT giá theo ngày, GET preview giá và GET/POST/cancel maintenance. JSON strict allowlist, envelope `{data,traceId}`, Problem Details F01/F02. Ghi cụ thể request/response/error trong `docs/features/F03-court-operations.md` trước khi code.
6. Partner UI chỉ hiển thị cấu hình vận hành cho business active, có trạng thái loading/error/empty, xem giá/maintenance, sửa lịch tuần/giá theo ngày và tạo/cancel bảo trì. Không phá luồng onboarding, register/login, token refresh F02.

## Kiểm thử và bàn giao

- Viết `docs/testing/F03-test-cases.md` trước khi code. Kiểm thử migration fresh/repeat/upgrade từ F02 có dữ liệu; rule ưu tiên/overlap, thiếu giá, scope, pending/suspended user, race schedule/maintenance, exclusion constraint, cancellation/idempotency, UTC/timezone, API/browser desktop/mobile qua API/PostGIS thật.
- Chạy build/typecheck, API tests, DB integration trên PostgreSQL/PostGIS thật, browser tests và `git diff --check`. Báo PASS/FAIL/NOT RUN/BLOCKED với lệnh và giới hạn bằng chứng. Review tuần tự nếu không có reviewer độc lập; sửa lỗi và chạy lại phần ảnh hưởng.
- Cập nhật đặc tả, testcase, `docs/progress.md` và `docs/setup.md`. F02 vẫn IN_PROGRESS chờ nghiệm thu tay/S3; F03 chỉ DONE khi acceptance và kiểm thử bắt buộc đạt. Không coi mock hoặc đọc source là bằng chứng transaction. Không tự commit/push/merge/deploy.
