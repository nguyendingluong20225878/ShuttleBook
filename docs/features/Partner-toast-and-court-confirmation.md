# Toast Partner và xác nhận sân — 2026-10-12

> **Cập nhật 2026-10-14:** [Contract mới](Partner-court-profile-followup-2026-10-14.md) bỏ nút xác nhận sân riêng. Phần mô tả và kết quả test confirm bên dưới là lịch sử; sân nháp tại cơ sở đã công bố tự ACTIVE khi Owner lưu giờ/giá hợp lệ.

## Quyết định và phạm vi trước code

Yêu cầu mới thay quy tắc sân bổ sung phải qua Admin: trong business ACTIVE và venue PUBLISHED, Owner thêm sân nháp, cấu hình lịch/giá rồi bấm **Xác nhận** để công bố sân. Không tạo COURT_ADDITION mới. Business ban đầu và venue mới vẫn gửi Admin duyệt, cùng các sân thuộc hồ sơ đó. Quyền Owner/scope, QR, version, snapshot booking và kiểm duyệt thay đổi thông tin nhạy cảm hiện có vẫn giữ; thay đổi này xử lý công bố sân mới, không thay luồng đối chiếu tiền.

Người dùng xác nhận lại cùng lượt: **giữ Admin duyệt đổi tài khoản/QR nhận tiền; sân tự xác nhận**. Không mở quyền đổi QR trực tiếp cho Partner.

Thông báo kết quả thao tác Partner hiển thị thành toast cố định ở đầu viewport hiện tại, tự ẩn sau khoảng 3 giây. Không cuộn trang, không chiếm chiều cao nội dung, phân biệt thành công/thất bại bằng màu và nội dung; thao tác liên tiếp (kể cả cùng nội dung) khởi động lại thời gian. Toast dùng live region và có nút đóng. Lỗi tải dữ liệu/trạng thái cần xử lý vẫn có thông tin và nút thử lại trong nội dung.

## Acceptance

- Owner đúng scope tạo sân INACTIVE trong cơ sở PUBLISHED; trước xác nhận không public/availability/quote.
- Xác nhận chỉ khi business/membership ACTIVE, venue PUBLISHED, sân INACTIVE, lịch/giá hợp lệ và QR cơ sở READY. Ảnh cơ sở tùy chọn, không ảnh sân.
- Thành công chuyển đúng sân ACTIVE, tăng version, audit trong transaction; Customer thấy sân và có thể xin quote ngay. Không tạo approval/outbox gửi Admin; sân/cơ sở/booking cũ giữ nguyên.
- Nút Xác nhận ngắn, aria-label chứa sân và cơ sở, không bị khóa bởi một đề nghị sửa pháp lý/QR không liên quan. Sân trong venue DRAFT/PENDING vẫn theo hồ sơ cơ sở, không được công bố độc lập.
- Hai xác nhận cạnh tranh chỉ một lần thành công; version stale bị chặn. Customer/owner khác/suspended không xác nhận được.
- COURT_ADDITION legacy pending không bị mất dữ liệu hoặc tự công bố khi khởi động: Owner dùng Xác nhận để công bố, bản đề nghị cũ thành SUPERSEDED, không được Admin duyệt nữa. Danh sách Admin không hiển thị COURT_ADDITION; UI chỉ còn doanh nghiệp/cơ sở và revision nhạy cảm hiện hành. Migration bổ sung chỉ nới CHECK status approval để lưu SUPERSEDED trung thực; không chuyển đổi dữ liệu hoặc sửa DB development tự động. Down không được xóa/đổi trạng thái lịch sử để né constraint.
- Toast ở mọi khu vực thao tác Partner chính: hồ sơ/cơ sở, lịch/giá, quyết định thanh toán, đánh dấu thông báo; thành công/lỗi thấy được khi cuộn thấp trên desktop/mobile và ẩn sau 3 giây. Không ẩn lỗi tải hoặc cảnh báo nghiệp vụ quan trọng chỉ vì hết thời gian toast.

## API/error/data

- `POST /api/v1/partner-onboarding/courts/{courtId}/confirm`, không body/query, bắt buộc `If-Match` version sân. Trả data `{id, status, version}` với status ACTIVE và ETag mới. Route `/courts/{id}/submit` cũ không tạo approval (ngừng map).
- 401/403 identity/role; 404 sai scope hoặc không tìm thấy; 428 thiếu If-Match; 412 stale; 400 body/query không hợp lệ; 409 STATE_CONFLICT sai trạng thái cha/con và INCOMPLETE_PROFILE thiếu lịch/giá/QR.
- Khóa theo thứ tự tương thích approval/business/court; recheck scope/status/version/schedule trong transaction. SUPERSEDED chỉ áp approval COURT_ADDITION đúng sân sau confirm thành công; giữ snapshot/historical submitted actor, không giả lập Admin reviewer. Ghi audit actor Owner, không xóa schema legacy.

## Testcase và bằng chứng

- PC01 tạo/thiếu lịch/thiếu QR/cha chưa publish/scope: chưa công bố, không approval mới.
- PC02 đủ lịch/giá, không ảnh, confirm → ACTIVE/version/audit/public/quote, không approval; sibling/booking snapshot giữ nguyên.
- PC03 stale/thiếu version/concurrent confirm, suspended/Customer/Owner khác; legacy pending superseded, Admin queue/decision không còn xử lý sân.
- PT01 toast thành công/lỗi khi cuộn, fixed bounding box trong viewport, desktop/mobile; 3 giây ẩn, nhắc lại cùng message reset timer, đóng tay.
- PT02 browser fixtures và live API/PostGIS/Mailpit: nút Xác nhận sân, Customer đồng bộ; business/venue Admin duyệt regression; giá/lịch/booking/payment và toast không làm hỏng nghiệp vụ.

## Bằng chứng local

Lệnh dưới chạy tại repo root PowerShell. Kết quả runtime không suy từ đọc source/fixtures.

| Lệnh | Kết quả | Bằng chứng |
|---|---|---|
| `npm.cmd run typecheck` | PASS 4 workspaces | `.local/court-confirm-typecheck.log` |
| `npm.cmd run build` | PASS 3 portal | `.local/court-confirm-web-build.log` |
| `scripts/dotnet.ps1 build backend/ShuttleBook.slnx -c Release --no-restore` | PASS 0 warning/error | `.local/court-confirm-build.log` |
| `scripts/dotnet.ps1 test backend/tests/ShuttleBook.Api.Tests --no-build --configuration Release` | PASS 96/96 | `.local/court-confirm-api.log` |
| `scripts/Test-Database.ps1 --no-build -c Release --filter FullyQualifiedName~OnboardingFlowTests` | 29 PASS / 1 FAIL lượt đầu,0skipped,7m32s; ca sửa recheck PASS 1/1,35s,exit0 | `.local/court-confirm-db.log` và `court-confirm-db-recheck.log`; assertion cũ đọc canCreate của series trong casualquote HTTP200, sửa sang courtId/quoteId; không đổi production code |
| `scripts/Test-Database.ps1 --no-build -c Release --filter FullyQualifiedName~Owner_confirms_court_additions_and_admin_only_publishes_new_venues` | PASS 1/1,exit0; tổng30ca đã xác minh | Ca sửa chạy đầy đủ cả Owner confirm/public/quote và Admin duyệt venue phía sau |
| `scripts/Test-Web.ps1 tests/web/f02-self-service.spec.ts tests/web/partner-workspace.spec.ts tests/web/f02-onboarding.spec.ts tests/web/ui-admin-approvals.spec.ts tests/web/ui-partner-history.spec.ts tests/web/f06-partner.spec.ts tests/web/f07-partner-series.spec.ts --workers=2 --reporter=line` | 90 PASS / 4 FAIL lượt đầu; 4/4 recheck PASS | `.local/court-confirm-web.log` và `court-confirm-web-recheck.log`; 4ca sửa locator toast/error và fixture venue PUBLISHED đúng API, không sửa sản phẩm để né lỗi |
| `scripts/Test-Web.ps1 tests/web/f07-partner-series.spec.ts tests/web/ui-partner-history.spec.ts --grep 'F07 partner shows\|Partner preview reports' --workers=2 --reporter=line` | PASS 4/4,7,8s | Chạy lại đúng4ca lỗi trước |
| `scripts/Test-Web.ps1 tests/web/f02-self-service.spec.ts --grep 'Partner toast stays' --workers=2 --reporter=line` | PASS 2/2,4,3s | `.local/court-confirm-toast-visual.log`; ảnh `.local/court-confirm-screenshots/` |
| `scripts/Test-Identity-Live.ps1 -ApiPort 5081 -Configuration Release` | PASS 8/8,1phút,runnerexit0 | `.local/court-confirm-live.log`; desktop/mobile thật Partner confirm sân→Customer public ngay, Admin không có COURT_ADDITION; OTP/Mailpit/giá/bảo trì/booking/payment regression |

Reviewer độc lập đọc source: hai findings về nút confirm route courts và stale recovery Operations đã sửa, re-review không còn blocker. Root xem ảnh mobile toast ở viewport đang cuộn. Live đã kiểm đúng tên DB tạm và drop thành công, runner khôi phục web build local; không dùng DB development hoặc mock để kết luận backend/OTP PASS. Migration chỉ áp DB thử; API/DB development cần cập nhật trước nghiệm thu tay (xem [hướng dẫn](../testing/F02-owner-self-service-manual.md)). Không xóa ảnh/booking/approval history; thông báo/outbox COURT_ADDITION cũ giữ lịch sử, có thể còn dispatch; không tạo mới. Không commit/push/merge/deploy. Hosted CI, full DB suite mọi class, MapTiler thật (live vẫn stub), AWS/S3/SMTP production NOT RUN mốc này; gate F09 giữ riêng.
