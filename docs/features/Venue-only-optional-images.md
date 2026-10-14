# Chỉ ảnh cơ sở, không có ảnh riêng sân

Quyết định người dùng ngày2026-10-12 thay các đặc tả ảnh sân trước đó.

## Scope/acceptance/API/dữ liệu trước code

- Chỉ có một ảnh đại diện tùy chọn theo venue. Không có ảnh riêng court, gallery court, placeholder ảnh court hoặc form upload ảnh court trên Partner/Customer/Admin.
- Owner thêm/thay/gỡ ảnh cơ sở đúng scope/version không qua Admin. Không có ảnh vẫn được gửi duyệt hồ sơ ban đầu hoặc venue/court bổ sung nếu các thông tin bắt buộc khác (QR, sân, giờ/giá, liên hệ/địa chỉ) đủ.
- Giữ `PUT operator/venues/{id}/image` với uploadId; bổ sung `DELETE operator/venues/{id}/image` không body, If-Match; version/audit giống PUT, draft/published editable, pending venue khóa. Public venue.imageUrl null nếu không ảnh; Customer placeholder cơ sở hiện có, không giả ảnh sân.
- Ngừng map API court image PUT/DELETE/public GET, presign từ chối purpose COURT_IMAGE/courtId; không trả imageUploadId/imageUrl trong DTO court hoặc snapshot court mới. Admin không hiển thị ảnh court kể cả snapshot cũ. Không phục vụ media COURT_IMAGE qua view/complete cho client.
- Ảnh venue nếu đã gắn phải READY đúng purpose/scope; null hợp lệ. QR giữ bắt buộc/nhạy cảm, không phải ảnh giới thiệu.
- Không xóa ảnh dữ liệu đang lưu hoặc tự áp migration vào DB development. Cột court media đã bổ sung trong migration chưa commit có thể giữ như dữ liệu legacy tương thích upgrade; không có chức năng đọc/ghi mới ở ứng dụng, không coi là feature ảnh sân. Ghi rõ lựa chọn này, tránh xóa dữ liệu khi chưa biết migration đã được áp ở phiên người dùng hay chưa.
- Approval mới không snapshot court image; venue image vẫn thuộc thông tin đề nghị mới. Một business pending, số sân/phân cấp/booking/payment giữ contract hiện tại.

## Kiểm thử

- IMG01 hồ sơ ban đầu và venue/court addition không ảnh vẫn submit/approve được; QR hoặc lịch/giá thiếu vẫn chặn.
- IMG02 Owner thêm/thay/gỡ ảnh venue ACTIVE, public null/ảnh đúng sau thao tác; stale/scope/purpose/unready/concurrency và pending bị chặn.
- IMG03 API court image không tồn tại, presign COURT_IMAGE/courtId bị400, private view/complete không expose court media legacy; public/detail/approval DTO không cung cấp ảnh court mới.
- IMG04 Partner/Admin/Customer desktop/mobile chỉ còn ảnh cơ sở; cơ sở không ảnh không làm mất quyền thêm sân/submit. Live/browser và PostGIS thật phân biệt rõ, kết quả ban đầu NOT RUN.

## Bằng chứng 2026-10-12

| Lệnh tại repo root PowerShell | Kết quả | Phạm vi |
|---|---|---|
| `npm.cmd run typecheck` | PASS 4 workspaces | TypeScript ba app/UI chung |
| `npm.cmd run build` | PASS 3 portal | `.local/venue-only-web-build.log` |
| `scripts/dotnet.ps1 build backend/ShuttleBook.slnx -c Release --no-restore` | PASS, 0 warning/error | `.local/venue-only-build.log` |
| `scripts/dotnet.ps1 test backend/tests/ShuttleBook.Api.Tests --no-build --configuration Release` | PASS 96/96 | `.local/venue-only-api.log` |
| `scripts/Test-Database.ps1 --no-build -c Release --filter FullyQualifiedName~OnboardingFlowTests` | PASS 27/27, 0 skipped, 6 phút | IMG01–03 và regression trên DB PostGIS thử riêng; runner exit 0; `.local/venue-only-db.log` |
| `scripts/Test-Web.ps1 tests/web/f02-self-service.spec.ts tests/web/partner-workspace.spec.ts tests/web/f02-onboarding.spec.ts tests/web/f04-discovery.spec.ts tests/web/ui-admin-approvals.spec.ts --workers=2 --reporter=line` | PASS 60/60, 40,8s | Desktop/mobile fixtures IMG04 và regression; `.local/venue-only-web.log` |
| `scripts/Test-Identity-Live.ps1 -ApiPort 5081 -Configuration Release` | PASS 8/8, 58,3s, runner exit 0 | Browser/API/Worker/PostGIS/Mailpit thật desktop/mobile: duyệt hồ sơ không ảnh, thêm/gỡ/thêm lại ảnh venue ACTIVE không tạo approval, duyệt sân mới và booking/payment regression; `.local/venue-only-live.log` |

Reviewer độc lập đọc source không thấy blocker; root đã xem ảnh Customer mobile không ảnh cơ sở, không gallery/placeholder ảnh sân. DB live thử đã kiểm tra đúng tên tạm và drop thành công, không sửa dữ liệu development; runner khôi phục web build cho cấu hình local. MapTiler trong live vẫn stub, provider thật không được kiểm lại trong mốc này. Backend mới chưa áp vào API development đang chạy; restart API để dùng code mới. Không có migration mới, không xóa bytes media/cột legacy, không commit/push/merge/deploy. Hosted CI, full database mọi class và AWS/S3/SMTP production NOT RUN trong mốc này; giữ gate F09 riêng.
