# Kiểm thử F01 trên giao diện và Postman

Ngày cập nhật: 2026-10-02. Các bước dùng môi trường local Development, trình duyệt và Postman Desktop. Không cần chạy testcase bằng lệnh PowerShell. API base URL mặc định là `http://localhost:5080`; Mailpit là `http://localhost:8025`.

## 1. Mở ứng dụng local

Khởi động Docker Desktop, sau đó mở bốn terminal tại thư mục gốc repo và chạy lần lượt các lệnh dưới đây (mỗi terminal giữ một tiến trình):

```text
npm.cmd run db:up
npm.cmd run db:migrate
npm.cmd run dev:api
npm.cmd run dev:customer
npm.cmd run dev:partner
```

Mở `http://localhost:5173` để test khách, `http://localhost:5174` để test chủ sân, và `http://localhost:8025` để xem thư OTP local. Mailpit không gửi thư ra ngoài. Các lệnh trên chỉ khởi động ứng dụng local; thao tác kiểm thử thực hiện trong browser/Postman.

Development local có giới hạn riêng để test nhiều luồng: tối đa 50 request register/resend và 100 request verify/login/refresh theo IP mỗi 15 phút; tối đa 20 register/resend theo contact mỗi 15 phút. API phải được khởi động lại sau khi đổi code/cấu hình. Môi trường khác vẫn mặc định 5 và 10 request/IP, 3 request/contact trong 15 phút; giới hạn này không bị tắt.

## 2. Luồng khách bằng giao diện

1. Mở `http://localhost:5173`, đăng ký bằng email thử nghiệm mới dạng `qa+<mã-ngẫu-nhiên>@example.test` và mật khẩu test.
2. Chọn liên kết Mailpit trên màn hình xác minh hoặc mở `http://localhost:8025`; mở thư mới nhất gửi đến email vừa nhập.
3. Nhập OTP vào giao diện khách. Kỳ vọng xác minh thành công và có thể chuyển tới đăng nhập.
4. Đăng nhập bằng thông tin vừa đăng ký. Kỳ vọng vào được phiên khách.
5. Tải lại trang. Theo F01.2, token chỉ nằm trong React memory nên phiên client mất sau reload; đăng nhập lại là kết quả mong đợi.

Mỗi lần test luồng OTP hãy dùng contact thử nghiệm mới. API cố ý trả `202` chung nhưng không gửi mã cho contact đã xác minh hoặc thuộc loại tài khoản khác, để tránh tiết lộ trạng thái/tồn tại tài khoản. Nếu API trả `503 IDENTITY_DELIVERY_UNAVAILABLE`, Mailpit chưa nhận thư; nếu trả `429 RATE_LIMITED`, chờ theo `Retry-After`.

Màn `/verify` chỉ có ô OTP. Contact được giữ trong browser history state để request verify/resend hoạt động sau F5; nếu mở URL trực tiếp mà không có trạng thái đăng ký, UI báo quay lại đăng ký thay vì hiện ô email/số điện thoại.

## 3. Luồng chủ sân bằng giao diện

1. Mở `http://localhost:5174`, đăng ký bằng email thử nghiệm mới và mật khẩu test.
2. Mở Mailpit từ liên kết trên trang hoặc tại `http://localhost:8025`; lấy OTP của email này và xác minh trên portal.
3. Đăng nhập. Kỳ vọng thấy trạng thái chủ sân đang chờ khởi tạo hồ sơ (`PENDING_ONBOARDING`); F02 chưa thuộc phạm vi bước này.
4. Thử nhập mật khẩu không khớp/OTP sai để quan sát thông báo lỗi. Dùng contact mới nếu muốn lặp ca đăng ký.

## 4. Gọi API bằng Postman

Import collection [`F01-Identity.postman_collection.json`](./F01-Identity.postman_collection.json) vào Postman. Mở tab **Variables** của collection và đặt `apiBase` thành `http://localhost:5080`. Chạy từng request trong thư mục **Customer** hoặc **Partner**. Điền `contact` và `password` bằng dữ liệu thử nghiệm; sau request Register, mở Mailpit để lấy OTP và gán vào biến `otp` trước khi chạy Verify.

Collection lưu access/refresh token vào biến collection để nối tiếp Login → Refresh → Logout. Các biến này chỉ nên tồn tại trong collection local của bạn; không export/share collection khi đã chạy, không chụp response có token. Xóa giá trị biến token sau khi test.

Các request gồm đăng ký, xác minh, resend, login, refresh, logout, login sai và field không hỗ trợ. Tab **Tests** kiểm tra status/code cơ bản. Mong đợi: register/resend `202`; verify/login/refresh `200`; logout `204`; sai credentials `401 INVALID_CREDENTIALS`; field lạ `400 UNSUPPORTED_FIELD`.

Customer và partner cố ý dùng chung `POST /api/v1/auth/login`, refresh và logout; backend xác định account type/status từ dữ liệu tài khoản, không tin role do client gửi. Đăng ký vẫn dùng endpoint riêng: `/api/v1/auth/register` cho customer và `/api/v1/partner-auth/register` cho partner.

## 5. Kiểm tra sâu qua giao diện

- **Refresh rotation/reuse:** Postman có thể chạy Refresh một lần, rồi thử lại request cũ bằng refresh token đã dùng; lần hai phải `401 INVALID_REFRESH_TOKEN`. Vì script test cập nhật biến sang token mới, hãy sao chép token cũ chỉ trong bộ nhớ Postman trước lần đầu, không đưa nó vào ghi chú/chia sẻ.
- **Refresh đồng thời:** Postman Collection Runner chạy tuần tự, không chứng minh race thật. Ca này hiện cần integration test tự động trên PostgreSQL; không đánh dấu PASS chỉ từ thao tác Postman tuần tự.
- **Rate limit:** Postman có thể gửi request theo collection, nhưng limiter theo IP/contact có thể ảnh hưởng các request khác. Chưa có collection riêng cho stress/rate-limit để tránh gây nhầm trạng thái; dùng assertion API/DB test hiện hữu cho đến khi có kịch bản GUI chuyên biệt.
- **Audit và migration:** Có thể xem database local bằng DBeaver/pgAdmin nếu đã cài, kết nối PostgreSQL `localhost:54329` với database/user/password từ `.env` (không chụp hoặc gửi thông tin kết nối). Chỉ chạy SELECT; kiểm tra `audit_events`, `refresh_sessions`, `__EFMigrationsHistory`. Không paste dữ liệu nhạy cảm vào báo cáo. Nếu không cài DB client, bỏ qua bước này và ghi **NOT RUN**.

## 6. Báo kết quả

Ghi ngày, trình duyệt/Postman, URL local, testcase đã thao tác và kết quả PASS/FAIL/NOT RUN. Với API chỉ ghi HTTP status và `code`; với UI ghi màn hình/trạng thái mong đợi. Không gửi password, OTP, access/refresh token, email/số điện thoại thật, `.env`, connection string hoặc response body chứa bí mật. Một luồng UI happy path không làm các ca concurrency, audit, rate limit hoặc migration thành PASS.
