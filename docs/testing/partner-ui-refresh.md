# Nghiệm thu UI partner

## Testcase

| ID | Tiền điều kiện / dữ liệu | Thao tác | Mong đợi | Bằng chứng dự kiến |
|---|---|---|---|---|
| PUI-01 | Owner chưa login | Đăng ký → OTP → login/logout; login sai role | Giữ F01, feedback và labels; token không lưu browser storage | Browser regression + live |
| PUI-02 | Business nháp, 2 cơ sở, 3 sân | Mở Tổng quan và từng menu | Counter thật; mỗi mục chỉ hiện form liên quan; checklist chỉ đúng phần còn thiếu | Browser UI |
| PUI-03 | Desktop/mobile | Menu, Escape, Tab, skip link, Back/Forward/deep link | Focus rõ, active nav, menu không giữ focus ẩn; không overflow 375/768/1024/1440 | Browser geometry + ảnh |
| PUI-04 | Business nháp | Điền tên sân/chọn cơ sở; sang mục khác rồi trở lại; đổi business | Giữ input khi đổi mục; reset lựa chọn/input khi đổi scope; không gửi ID cũ | Browser request assertion |
| PUI-05 | PENDING_APPROVAL / revision PENDING | Mở mọi mục; xem thông tin | Không có form mutation nháp/revision; trạng thái/lý do thật | Browser UI + live duyệt |
| PUI-06 | Business ACTIVE | Policy, lịch tuần, rule giá, preview, khóa/hủy bảo trì | Các mutation cũ nối đúng sân/If-Match; preview và ca customer đổi theo API thật | Live PostGIS |
| PUI-07 | API lỗi, notifications unread | Tải lại hồ sơ/thông báo; đánh dấu đọc | Error/empty/loading rõ; có retry; badge giảm sau response | Browser UI |
| PUI-08 | Draft đủ dữ liệu | Địa chỉ MapTiler → sân/lịch → ảnh/QR → gửi; admin trả sửa/duyệt; revision QR | Luồng F02–F05 giữ nguyên; upload đúng scope; booking cũ giữ snapshot QR | Live PostGIS (MapTiler stub được ghi rõ) |

## Test tay (PowerShell trong terminal VS Code)

Tại repo root chạy API/Worker và partner bằng các terminal riêng: `npm.cmd run dev:api`, `npm.cmd run dev:worker`, `npm.cmd run dev:partner`; DB/Mailpit cần đang chạy. Mở http://localhost:5174. Nếu đang chạy dev server, Vite tự nạp thay đổi UI; build preview phải build lại.

Kiểm thử tự động: `npm.cmd run build`, `npm.cmd run test:web -- --workers=2`. Live dùng `npm.cmd run test:identity-live -- -ApiPort 5081` khi 5080 đang có API dev; cần backend đã build, PostgreSQL/PostGIS và Mailpit. Runner chỉ migrate/drop DB test tạm đã xác thực, tự khôi phục web build về API local khi kết thúc. Preview kiểm thử dùng IPv4 để tránh mở nhầm Vite dev IPv6; nếu có ứng dụng dùng 127.0.0.1:5173–5175, runner báo cổng bận.

1. Kiểm tra đăng ký, login/logout bằng tài khoản test hiện có. F5 vẫn theo session memory hiện hành; thay đổi UI không bổ sung cơ chế duy trì đăng nhập partner.
2. Login owner nháp: xem Tổng quan, đối chiếu số cơ sở/sân và checklist với dữ liệu thật. Nhấn các đường dẫn trong checklist để đến đúng mục.
3. Hồ sơ → sửa tên pháp lý; Cơ sở → nhập/chọn/xác nhận địa chỉ MapTiler rồi lưu; Sân → chọn cơ sở, tạo/sửa sân; Lịch & giá → chọn sân, thêm ngày/khung giá và lưu.
4. Ảnh cơ sở → tải PNG/JPEG/WebP ≤5MB; Thanh toán & QR → khai báo ngân hàng/chủ tài khoản/số tài khoản/QR rồi lưu. Sang mục khác rồi quay lại kiểm tra dữ liệu form chưa gửi; đổi business phải xóa lựa chọn và input tạm của scope cũ.
5. Nhấn Gửi hồ sơ duyệt trước khi đủ dữ liệu để thấy lỗi backend; bổ sung theo checklist rồi gửi lại. Pending khóa chỉnh sửa. Admin trả sửa phải hiện lý do; duyệt xong owner tải lại hồ sơ sẽ ACTIVE.
6. Owner ACTIVE → Lịch & giá: lưu block/min/hold, lịch tuần và giá theo ngày; Xem thử giá; khóa/hủy ca bảo trì. Đối chiếu trang customer F04/F05 hiện có.
7. Thanh toán & QR của ACTIVE → gửi revision đúng cơ sở. Trong lúc pending, thông tin đang publish vẫn dùng; không thấy form gửi lại. Giữ nguyên quy trình Admin duyệt.
8. Thông báo → Đã đọc; badge giảm. Tắt API rồi tải lại: có lỗi và nút retry, không báo thành công giả.
9. Thu hẹp cửa sổ còn 375px: menu mở/đóng, Escape và focus trở về nút menu; form một cột, không cuộn ngang toàn trang. Kiểm tra Tab, skip link và Back/Forward giữa các mục.

## Kết quả

Lập testcase trước code; kết quả 2026-10-07, Windows/PowerShell tại repo root:

- **PASS** `npm.cmd run typecheck`, `npm.cmd run build`, `git diff --check`.
- **PASS** `npm.cmd run test:web -- --workers=2`: bản cuối trên production preview IPv4, 58 PASS / 8 SKIP live có chủ đích trong 28,6s. 10 ca mới trong `partner-workspace.spec.ts` kiểm tra counters/forms/navigation/retained state/reset scope/pending/notifications/reflow; mock API và MapTiler chỉ chứng minh UI. PUI-01–PUI-05/PUI-07 phần browser đã pass; đã review ảnh auth/overview/operations desktop/mobile trong `artifacts/partner-ui/`.
- Lượt đầu identity test phát hiện startup read lặp khi StrictMode mount workspace và mobile chưa mở menu để logout; đã xử lý startup/mở menu trong helper. Test mới từng fail do locator label khớp cả nav, đã sửa sang role combobox exact và chạy lại pass.
- **PASS** `npm.cmd run test:identity-live -- -ApiPort 5081`: lượt cuối 8/8 trong 37,4s, gồm PUI-01/PUI-05/PUI-06/PUI-08 qua API/PostGIS/Mailpit/Worker thật, media local private, MapTiler stub. Luồng đầy đủ: nháp → incomplete-profile → ảnh/QR → gửi duyệt → trả sửa → duyệt → giá ưu tiên → policy block/min/hold (assert DTO/If-Match và giá trị sau reload) → preview → khóa/hủy bảo trì/customer availability → booking/QR snapshot → revision QR duyệt. DB tạm đã drop đúng mục tiêu; runner phục hồi build web bình thường; artifact không còn URL test API 5081.
- Lượt mặc định **BLOCKED** ở 5080 do API người dùng đang chạy; hai lượt 5081 **FAIL** do browser mở nhầm dev server IPv6 (2 pass/6 fail mỗi lượt). Preview/readiness và Chromium resolver đã cố định IPv4, guard origin không cho request tới API ngoài test, rồi live 8/8 pass. Browser suite đầu có thể dùng dev IPv6; đã chạy lại toàn suite trên production preview IPv4 cho bản cuối và pass như trên. Không dừng dev server/API của người dùng.
- **NOT RUN:** nghiệm thu tay UI mới bởi người dùng; S3/MapTiler provider thật; review độc lập. Không đổi trạng thái F05 hoặc bắt đầu F06.
