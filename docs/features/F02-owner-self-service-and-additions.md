# F02 mở rộng — Owner tự quản nội dung và gửi duyệt mục mới

> **Thay đổi mới nhất:** [Sân mới và hồ sơ Customer 2026-10-14](Partner-court-profile-followup-2026-10-14.md) thay thao tác xác nhận sân riêng: Owner lưu giờ/giá hợp lệ để tự công bố sân trong cơ sở đã duyệt. [Contract xác nhận sân cũ](Partner-toast-and-court-confirmation.md) và luồng COURT_ADDITION/Admin duyệt sân dưới đây là lịch sử; chỉ doanh nghiệp ban đầu/cơ sở mới cần Admin duyệt để công bố.

Ngày chốt contract: 2026-10-12. Trạng thái: triển khai và kiểm thử local hoàn tất; chờ nghiệm thu tay/CI. Quyết định người dùng ưu tiên hơn guard nháp của F02 cũ. [Bằng chứng và giới hạn](../testing/F02-owner-self-service-results.md).

**Thay đổi hiện hành về ảnh:** [Chỉ ảnh cơ sở tùy chọn](Venue-only-optional-images.md) thay các route/cột DTO/acceptance ảnh sân bên dưới. Không có ảnh riêng court ở sản phẩm; không ảnh cơ sở vẫn đủ điều kiện gửi duyệt nếu cấu hình bắt buộc khác đủ. Owner có DELETE ảnh cơ sở, không qua Admin; QR vẫn bắt buộc. Các đoạn về COURT_IMAGE bên dưới ghi thiết kế lịch sử trước khi người dùng đổi yêu cầu, không dùng triển khai mới.

Quyết định tiếp theo cùng ngày: tên sân khi tạo/đổi được chuẩn hóa theo số trong từng cơ sở; [contract mới](Partner-court-numbering-and-actions.md) thay phần nhập tên hiển thị tự do, giữ API name và dữ liệu cũ đọc/vận hành được.

## Phạm vi và acceptance

- Owner ACTIVE thêm venue DRAFT trong business ACTIVE; thêm court INACTIVE trong venue PUBLISHED. Chỉ mục mới được gửi duyệt, không khóa business hay mục cũ. Admin duyệt VENUE_ADDITION/COURT_ADDITION; request-changes trả đúng mục về nháp với lý do. Hồ sơ ban đầu giữ ONBOARDING.
- Owner đổi ảnh venue/sân đã hoạt động không qua Admin. Một ảnh đại diện/sân, tùy chọn; venue vẫn phải có ảnh khi gửi hồ sơ. Có thể gỡ ảnh sân; venue phải thay ảnh khác, không gỡ ảnh bắt buộc.
- Owner đổi tên hiển thị sân trực tiếp; định danh pháp lý business và địa chỉ/QR venue dùng revision Admin. Lịch/giá/bảo trì giữ contract F03, giá booking cũ không đổi.
- Court INACTIVE/PENDING_APPROVAL không public, không nhận quote/booking. Mục cũ và business vẫn ACTIVE. Quyền tại backend kiểm user/membership/parent, không tin role/status client.

## Dữ liệu

- `Court.ImageUploadId: Guid?`; `MediaUpload.CourtId: Guid?`, purpose COURT_IMAGE. Composite FK court/venue chống gắn sân ngoài venue, FK ảnh và validation purpose/READY/owner tại API. COURT_IMAGE có courtId, không bookingId; QR/VENUE_IMAGE không courtId; PAYMENT_PROOF giữ contract.
- `ApprovalRequest.CourtId: Guid?`, kinds ONBOARDING/VENUE_REVISION/VENUE_ADDITION/COURT_ADDITION/BUSINESS_REVISION. CHECK kind/resource; giữ unique pending/business hiện có để serialize một đề nghị mỗi business, UI giải thích STATE_CONFLICT. Snapshot bất biến; không cho sửa child pending. Parent business ACTIVE không chuyển trạng thái khi gửi mục mới.
- Venue mới: DRAFT → PENDING_APPROVAL → PUBLISHED hoặc DRAFT; court mới: INACTIVE → PENDING_APPROVAL → ACTIVE hoặc INACTIVE. ONBOARDING vẫn như cũ. Migration tăng dần, fresh/upgrade/repeat không đổi booking/ảnh cũ.

## API

Prefix /api/v1, envelope/error như hiện có. Các route hiện có mở thêm đúng trạng thái:

| Route | Contract |
|---|---|
| POST partner-onboarding/businesses/{id}/venues | DRAFT hoặc ACTIVE đúng Owner; tạo venue DRAFT |
| POST partner-onboarding/venues/{id}/courts | Venue DRAFT editable hoặc PUBLISHED trong business ACTIVE; tạo court INACTIVE |
| PUT partner-onboarding/venues/{id} | Chỉ venue DRAFT trong business DRAFT/ACTIVE; If-Match venue |
| PUT partner-onboarding/courts/{id} | Đổi tên sân nháp hoặc ACTIVE đúng scope; If-Match cho ACTIVE; tăng version/audit |
| PUT partner-onboarding/courts/{id}/schedule | Chỉ sân nháp editable, không pending; ACTIVE dùng F03 |
| PUT partner-onboarding/venues/{id}/payment-account | Chỉ venue DRAFT editable; published dùng revision |
| POST partner-onboarding/venues/{id}/submit | Owner ACTIVE, venue DRAFT đủ ảnh/QR/court/lịch/giá → VENUE_ADDITION |
| POST partner-onboarding/courts/{id}/submit | Owner ACTIVE, court INACTIVE dưới venue PUBLISHED đủ lịch/giá → COURT_ADDITION; kế thừa QR venue |
| POST partner-onboarding/businesses/{id}/revisions | ACTIVE; {name,legalName,contact}, If-Match business; BUSINESS_REVISION |
| PUT/DELETE operator/courts/{id}/image | {uploadId} / no body, If-Match court; direct, audit; draft editable hoặc ACTIVE |
| PUT operator/venues/{id}/image | {uploadId}, If-Match venue; draft editable hoặc PUBLISHED; direct/audit |
| POST uploads/presign | Bổ sung courtId tùy chọn, bắt buộc khi purpose COURT_IMAGE; giữ field cũ bắt buộc |
| GET courts/{id}/image | Public chỉ business ACTIVE/venue PUBLISHED/court ACTIVE và ảnh READY đúng scope; bytes local, redirect S3 theo adapter hiện có |
| GET partner-onboarding/businesses/{id} | Court version/imageUploadId; approval kind/venueId/courtId và danh sách approvals gần nhất để biết từng mục |
| GET/POST admin/approval-requests | Filter kinds mới; snapshot additions cùng shape profile {name,legalName,contact,venues:[...courts...]}; chỉ đúng mục được publish |

401/403/404 theo identity/scope; 400 validation/purpose/upload; 409 STATE_CONFLICT/INCOMPLETE_PROFILE; 428/412 version. Approve/request-changes khóa approval rồi business rồi child nhất quán; kiểm dữ liệu, snapshot và scope trước publish; audit/outbox trong cùng transaction. Không đưa sửa ảnh/lịch/giá vào approval.

## UI và kiểm thử

Partner Cơ sở & sân: thêm nháp cho ACTIVE, form lịch/giá/QR nháp dùng được cho venue/court bổ sung, trạng thái/lý do/gửi duyệt theo mục, ảnh riêng cho sân, đổi tên/ảnh trực tiếp. Admin phân biệt năm kinds, chỉ mục mới hoặc revision; Customer hiển thị ảnh sân đã publish. Không có timezone kỹ thuật trên Customer/Partner.

Testcase: SS01 Owner sửa ảnh ACTIVE/không approval; SS02 upload sai scope/purpose/READY và stale version không đổi ảnh; SS03 ảnh court nháp public 404, sau approve trả bytes; SS04 thêm venue/court dưới ACTIVE nhưng public/quote/availability chặn trước duyệt; SS05 approve/request-changes race chỉ một quyết định, child đúng trạng thái, mục cũ nguyên vẹn; SS06 request-changes/sửa/gửi lại; SS07 migration upgrade/repeat; SS08 Customer/owner khác/suspended bị chặn; SS09 business revision giữ bản cũ đến duyệt; SS10 browser desktop/mobile ba vai trò; SS11 OTP live với Mailpit; regression giá/snapshot/cap/quote theo suite đã có. Kết quả ban đầu NOT RUN; ghi bằng chứng vào progress/test report sau khi chạy.

Kết quả 2026-10-12: OnboardingFlowTests PostGIS **22/22 PASS**, recheck quote/availability **1/1 PASS** (thuộc tập trên), API **96/96 PASS**, browser fixture **56/56 PASS**, live **8/8 PASS**; typecheck/build PASS. SS01–SS11 được phủ trong các nhóm tương ứng ở báo cáo; không tính hosted CI/S3/SMTP production hoặc nghiệm thu tay là PASS. Trần tiền/quote quota giữ contract và bằng chứng F09 trước, không chạy lại toàn bộ các class DB ở mốc này.
