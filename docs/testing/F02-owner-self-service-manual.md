# Tự kiểm định quyền ba vai trò và cơ sở/sân bổ sung

Ngày: 2026-10-12. Dùng tài khoản và dữ liệu thử local. Kết quả tự kiểm định ghi PASS/FAIL và thao tác lỗi, không gửi mật khẩu/OTP/token.

## Chuẩn bị

Trong PowerShell tại `C:\Users\luong\Desktop\CLong`, bật Docker/PostGIS/Mailpit. Đóng API đang chạy bằng Ctrl+C trong terminal của API, áp migration local bằng `npm.cmd run db:migrate`, rồi mở lại `npm.cmd run dev:api`, `npm.cmd run dev:worker` và ba app `dev:customer`, `dev:partner`, `dev:admin` ở các terminal riêng. Migration `20261012095502_F02OwnerSelfService` thuộc đợt F02 mở rộng; `20261012152851_CourtOwnerConfirmation` nới constraint status approval để giữ lịch sử SUPERSEDED khi Owner xác nhận sân từng chờ Admin. Không tự chuyển sân đang chờ thành ACTIVE; không xóa booking/media. Chỉ áp vào DB local của dự án, không đổi connection string sang production. Nếu DB local có dữ liệu cần giữ, tạo backup theo [runbook local](../runbooks/local-predeploy.md) trước khi nâng cấp. Agent chỉ áp migration vào DB thử, chưa nâng cấp DB development của bạn.

- Customer: `http://localhost:5173`; Partner: `http://localhost:5174`; Admin: `http://localhost:5175`.
- Mailpit: `http://localhost:8025`; `/health/ready` của API local phải trả 200.
- Cần một Owner đã ACTIVE cùng cơ sở PUBLISHED/sân ACTIVE, một Admin thử và Customer thử. Admin không đăng ký public; dùng tài khoản bootstrap local đã có.

## Các ca tự thử

| ID | Thao tác | Kết quả mong đợi |
|---|---|---|
| M01 | Partner → Cơ sở & sân, chọn cơ sở đã hoạt động, tải ảnh giới thiệu mới | Ảnh preview hiện thật, tải lại trang vẫn có; Admin không nhận hồ sơ duyệt ảnh. Upload sai loại hoặc quá 5 MB báo lỗi và giữ ảnh đang có. Không có ảnh riêng sân. |
| M02 | Cùng trang, thay rồi gỡ ảnh cơ sở; Customer mở chi tiết cơ sở | Customer thấy ảnh cơ sở nếu có, placeholder nếu không. Sân vẫn hoạt động, không thay giá/booking và không chờ Admin. Không có gallery ảnh sân hoặc API ảnh sân. |
| M03 | Partner đổi Số sân đang hoạt động, ví dụ từ 1 sang 3 | Lưu trực tiếp thành Sân 3, Customer thấy tên mới; các ID/lịch/giá/booking không đổi. Nếu Sân 3 đã có trong cùng cơ sở thì báo trùng và giữ sân cũ. Sân 1 ở cơ sở khác được phép. |
| M04 | Partner → Lịch & giá, sửa giá ca tương lai; Customer lấy báo giá mới | Giá mới đúng từng ca; đơn đã tạo trước đó giữ tổng tiền/QR/lịch cũ. Không phát sinh yêu cầu Admin duyệt giá. |
| M05 | Partner → Cơ sở & sân → thêm sân trong cơ sở đang hoạt động; sang Lịch & giá cấu hình sân nháp; quay lại bấm Xác nhận | Sân nháp chưa xuất hiện trên Customer; thiếu lịch/giá/QR báo lỗi. Xác nhận đủ thông tin → ACTIVE và Customer thấy/đặt ngay, không gửi Admin. Sân khác/business vẫn hoạt động. Sân nằm trong cơ sở mới chưa duyệt vẫn theo hồ sơ cơ sở, không tự công bố. |
| M06 | Cuộn thấp ở Partner rồi lưu/confirm; thử thao tác thiếu dữ liệu hoặc trùng số sân, thử lặp và đóng thông báo | Toast thành công/lỗi ở đầu màn hình đang xem, không cuộn lên đầu trang, tự ẩn sau khoảng3giây; thao tác mới đặt lại thời gian. Nếu version stale, toast ẩn nhưng cảnh báo/nút tải lại vẫn có và chưa cho lưu tiếp; tải phiên bản mới rồi thử lại. Admin không có hồ sơ Sân mới. |
| M07 | Partner thêm cơ sở mới: địa chỉ MapTiler, sân, giờ/giá, tài khoản/QR; không tải ảnh và gửi cơ sở duyệt | Không ảnh vẫn gửi/duyệt được. Thiếu QR hoặc lịch/giá mới báo INCOMPLETE_PROFILE; chỉ cơ sở mới pending. Cơ sở/sân cũ tiếp tục nhận đặt. Ảnh cơ sở thêm sau nếu muốn. |
| M08 | Partner → Hồ sơ doanh nghiệp gửi thay đổi pháp lý; hoặc Thanh toán & QR gửi thay đổi địa chỉ/tài khoản nhận tiền | Bản hiện hành giữ nguyên trong lúc chờ. Admin thấy bản cũ/bản đề xuất và duyệt đúng loại; booking cũ giữ QR/tài khoản snapshot. Trong lúc chờ vẫn đổi/gỡ ảnh cơ sở đang hoạt động được. |
| M09 | Customer có quote còn hiệu lực rồi xin quote khác; thử tổng 10.000.000 và lớn hơn trần | Quote mới bị từ chối, quote cũ giữ nguyên; đúng trần được phép, vượt trần bị chặn từ quote. Kỳ cố định áp tổng cả kỳ. |
| M10 | Customer đăng ký email thử mới, xem Network register và Mailpit, xác minh OTP | Register 202, email thử nhận OTP và xác minh được; nếu lỗi ghi HTTP/code và thời gian chờ. 202 với contact đã tồn tại không bảo đảm gửi OTP. Không chia sẻ mã/email/mật khẩu trong bằng chứng. |
| M11 | Thử Owner khác và Customer gọi API sửa ảnh/tên/sân hoặc approve bằng Postman | Sai role/scope nhận 401/403/404 phù hợp, dữ liệu không đổi. Ẩn menu không phải bằng chứng kiểm quyền. |

Các ca concurrency, FK/constraint, migration và idempotency phải dựa trên test PostGIS thật trong báo cáo tự động. Tự thử UI không thay thế chúng. U04 rộng hơn (bàn phím/zoom/màn hình/ba cổng), hosted CI đúng code, S3/EC2 thật và cấu hình vận hành vẫn là các gate riêng.

Theo yêu cầu đánh số sân: tại Thêm sân, chọn cơ sở để được gợi ý số nhỏ nhất còn trống, có thể sửa số trước Lưu sân; thử hai cơ sở cùng có Sân 1. Danh sách phải xếp Sân 1, Sân 2, Sân 10 theo số. Nút Thêm sân/Xác nhận/Gửi duyệt cơ sở/Gỡ ảnh ngắn theo card; tên dài không làm nút dài ra, mobile vẫn có vùng bấm tối thiểu44px. Sân tên cũ không tự đổi; chỉ nhập số khi chủ động muốn đổi tên đó.

## Chạy lại tự động

Tất cả lệnh dưới chạy ở repo root PowerShell:

```powershell
npm.cmd run typecheck
npm.cmd run build
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Database.ps1 --filter FullyQualifiedName~OnboardingFlowTests
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Web.ps1 tests/web/f02-self-service.spec.ts tests/web/partner-workspace.spec.ts tests/web/f02-onboarding.spec.ts tests/web/ui-admin-approvals.spec.ts tests/web/f04-discovery.spec.ts --workers=2
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/dotnet.ps1 build backend/ShuttleBook.slnx --configuration Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Identity-Live.ps1 -ApiPort 5081 -Configuration Release
```

DB tests và live script tự tạo DB thử riêng. Live cần cổng 5081 và các preview 5173–5175 trống; không dừng tiến trình của người dùng tự động. Script chạy API/Worker/ba preview riêng rồi dọn đúng DB thử. Không suy ra S3/SMTP production PASS từ adapter local/Mailpit.
