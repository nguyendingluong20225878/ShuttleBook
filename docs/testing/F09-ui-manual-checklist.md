# F09 — checklist nghiệm thu giao diện ba cổng

Ngày 2026-10-10. Trạng thái: **NOT RUN**, chờ người dùng xem và ghi nhận. Mục tiêu là kiểm nhận diện xanh ShuttleBook và nút, ô nhập, thông báo, trạng thái, khoảng cách giữa các trang. Không cần chuyển khoản thật.

## Chuẩn bị trong PowerShell

Tại `C:\Users\luong\Desktop\CLong`, Docker PostGIS/Mailpit đã bật. Mở **năm terminal riêng**, chạy một lệnh trong mỗi terminal:

```powershell
npm.cmd run dev:api
npm.cmd run dev:worker
npm.cmd run dev:customer
npm.cmd run dev:partner
npm.cmd run dev:admin
```

Chờ `http://localhost:5080/health/ready` trả 200. Mở Customer `http://localhost:5173`, Partner `http://localhost:5174`, Admin `http://localhost:5175`. Dùng tài khoản/dữ liệu **thử local** sẵn có. Nếu thiếu tài khoản hoặc sân thử, ghi **BLOCKED** và báo agent chuẩn bị dữ liệu thử; không nhập mật khẩu, OTP hoặc thông tin ngân hàng vào chat/ảnh.

## Các màn hình cần xem

| Cổng | Màn hình và thao tác | Cần đạt |
|---|---|---|
| Customer | Đăng nhập/đăng ký; Tìm sân → lịch sân → báo giá vãng lai; Đơn của tôi → chi tiết đơn/QR; thông báo. | Cùng màu/nút/ô nhập/trạng thái; giá tiền dễ đọc; báo giá vượt 10 triệu giải thích rõ; khi còn quote hold, yêu cầu quote mới bị chặn và quote cũ giữ nguyên; QR không làm lộ số tài khoản đầy đủ ngoài màn hình đúng quyền. |
| Partner | Tổng quan; Hồ sơ doanh nghiệp; Cơ sở/Sân; Lịch & giá; Đơn đặt sân → chi tiết → quyết định thanh toán; Thanh toán & QR; thông báo. | Thành phần và thông báo cùng phong cách; trạng thái chờ/xác nhận/từ chối phân biệt được bằng chữ, không chỉ màu; chuyển cơ sở không mang dữ liệu cũ; nút quyết định dễ nhận biết và không gửi hai lần. |
| Admin | Đăng nhập; danh sách hồ sơ; chi tiết hồ sơ và phê duyệt/yêu cầu chỉnh sửa; thông báo. | Form, nút, cảnh báo cùng quy chuẩn; nội dung trước và sau thay đổi dễ đối chiếu; thao tác phê duyệt có bước xác nhận rõ. |

Với **mỗi cổng**, xem ít nhất một trang chính và một trang chi tiết ở độ rộng **375, 768, 1024, 1440 px** bằng DevTools Device Toolbar. Kiểm không có cuộn ngang ngoài vùng bảng được thiết kế cuộn riêng, chữ/nút không đè nhau, form vẫn thao tác được. Ở zoom **200%**, kiểm cùng các trang tại kích thước cửa sổ bạn thường dùng.

Thử **Tab, Shift+Tab, Enter và Escape**: focus phải nhìn thấy rõ, thứ tự đi theo nội dung, không kẹt trong hộp thoại. Nếu có NVDA, đọc tiêu đề, nhãn input, lỗi và trạng thái; nếu chưa cài hoặc không dùng được, ghi **NOT RUN** riêng. Kiểm tương phản chữ trên nền bằng quan sát; trường hợp nghi ngờ gửi ảnh để agent đo cụ thể.

## Cách gửi kết quả

Mỗi lỗi chỉ cần một dòng: **cổng/trang → độ rộng hoặc zoom → thao tác → thực tế → mong đợi**. Đính kèm ảnh nếu có; che QR, số tài khoản, OTP, email/số điện thoại thật. Nếu một mục chưa có dữ liệu để thử, ghi `NOT RUN` hoặc `BLOCKED`, không đánh dấu PASS thay.

Ví dụ: `Customer / chi tiết đơn → 375 px → mở QR → nút “Đã chuyển khoản” tràn khỏi thẻ → mong đợi nút nằm trọn và bấm được`.
