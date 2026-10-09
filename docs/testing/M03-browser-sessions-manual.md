# Nghiệm thu M03 — phiên Customer/Partner sau F5

Scope được duyệt 2026-10-09: owner vận hành, F08 DEFERRED. Idle30phút, absolute30ngày từ login, access10phút. Cookie HttpOnly riêng theo portal; polling/restore không gia hạn idle. Không thay deadline quote/payment/fixed.

## 1. Chạy backend mới

API development đang chạy lúc sửa code là binary cũ. Trong terminal API nhấn Ctrl+C rồi:

```powershell
Set-Location C:\Users\luong\Desktop\CLong
$env:Media__Mode = 'Local'
npm.cmd run dev:api
```

Nếu Worker chạy bản cũ, Ctrl+C và `npm.cmd run dev:worker` ở terminal Worker. Web dev cập nhật qua Vite; nếu cần restart từng dev:customer/dev:partner/dev:admin. M03 không thêm migration, dùng LastActivityAt đã có.

Terminal kiểm tra: `curl.exe -i http://localhost:5080/health/ready` phải200Healthy. Dùng localhost thống nhất: Customer5173, Partner5174. Nếu đổi Origin, cấu hình BrowserSession__CustomerOrigin/PartnerOrigin và CORS cùng lúc; `.env.example` có giá trị mẫu.

## 2. Test tay

### S01 — Customer F5/private URL

1. Login Customer ACTIVE → Đơn của tôi → mở một đơn thử; ghi mã/URL/tiền/trạng thái.
2. F5: sau loading ngắn, đúng URL/đơn; không login và không tự gửi lại create/report.
3. Mở URL đó ở tab mới cùng profile: restore rồi đọc đúng dữ liệu riêng.
4. Network: POST browser-auth/customer/restore body{} trả200; JSON không có refreshToken. Login returnTo nội bộ trở lại đúng trang.

### S02 — Partner F5

1. Owner login, vào Đơn đặt sân, mở detail. F5 giữ hash URL và đúng đơn/scope.
2. Notification/biên lai private vẫn đọc được khi còn quyền; access10phút hết hạn có thể restore cookie.
3. Pending owner restore được hồ sơ nháp nhưng vẫn không vận hành booking. Không cấp quyền mới.

### S03 — portal độc lập và cookie

1. Cùng profile login Customer/Partner/Admin, F5 Customer/Partner đều giữ phiên.
2. Logout Customer: tab Customer khác đóng phiên, Partner/Admin giữ phiên riêng.
3. Application → Cookies API host: customer/partner cookie khác tên/path, HttpOnly, SameSiteStrict. Secure trên HTTPS; DevelopmentHTTP không cần Secure.
4. Không có access/refresh token trong localStorage/sessionStorage. Khi logout lỗi mạng có thể có marker boolean chặn restore, không chứa credential.

### S04 — quote/đơn/quay lại từ ứng dụng ngân hàng

1. Quote5ca150phút đạt minimum; ghi deadline. F5 trong120s: quote thay thế cùng sân không kéo dài deadline gốc. Quote cũ hết hạn không hồi sinh.
2. Create → ghi mã → F5/rời trang/quay lại URL: vẫn một đơn, cùng QR/giá/deadline.
3. Sang app khác rồi quay lại khi còn idle: không login lại. Dùng ảnhTEST báo chuyển, Owner confirm; không cần chuyển tiền thật.
4. Report rồi F5: vẫn chờOwner, ảnh/history giữ; deadline thanh toán cũ không release.
5. Mất response create rồi F5: tìm đơn trong Đơn của tôi. Intent vẫn memory, không giả định F5 giữ Idempotency-Key; không tạo lại chỉ vì mất response.

### S05 — idle30phút

1. Login, mở trang có polling; không click/gõ/cuộn chuột/cảm ứng trong31phút.
2. Phiên hết hạn; thao tác private/F5 yêu cầu login. Tab nền cũng hết hạn backend.
3. Lượt khác: login → phút25 thao tác thật và đợi activity hoàn tất → phút37 từ login F5 vẫn còn phiên.
4. Polling/F5 restore không tự kéo dài idle; thao tác không kéo dài quote/payment.

Activity gửi có throttle60giây/trailing flush; server tính expiry từ activity được ghi nhận. Scroll do layout không tính thao tác. Biên đúng30phút và absolute30ngày kiểm bằng clock/PostGIS tự động; không đổi đồng hồ máy.

### S06 — logout hai tab/đổi tài khoản

1. Hai tab cùng Customer/profile đang xem private. Logout tabA → tabB đóng phiên, không tiếp tục đọc/ghi bằng phiên cũ.
2. Back/F5 không restore phiên logout; lặp Partner.
3. Đổi account ở tab khác: dữ liệu và response cũ không xuất hiện dưới account mới.

### S07 — lỗi mạng

1. DevTools Request blocking chặn API browser-auth, giữ frontend tải được; F5 URL private.
2. Lỗi khôi phục + Thử lại kết nối; không báo mật khẩu sai/không tự POST booking.
3. Bỏ chặn, Thử lại: đúng phiên/URL.
4. Chặn API lúc logout: UI đóng và báo máy chủ chưa xác nhận. F5/retry khi có mạng phải hoàn tất logout trước restore.
5. Offline toàn browser rồi F5 có thể không tải được HTML Vite; không dùng hiện tượng đó thay test lỗi restore.

## 3. Gate và kết quả

M03 browser32executions desktop/mobile gồm response login-body/restore/activity về muộn; BrowserSessionTests dùng PostGIS/clock và family lock barriers. Live F02–F07 kiểm frontend/API/Worker thật. Kết quả tại M03-browser-sessions-results.md, không tính fixtures là live.

Mốc2 auth/search/quyền được kiểm trong scope; quota toàn tài khoản, casual amount cực lớn và CI hosted còn gate riêng. Mốc4/F08 hoãn, không FAIL/DONE. Core mốc5 giữ nghiệp vụ; local không chứng minh S3/production/pilot.
