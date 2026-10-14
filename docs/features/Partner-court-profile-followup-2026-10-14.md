# Sân mới và hồ sơ Customer — quyết định 2026-10-14

## Phạm vi và điều kiện chấp nhận

- Mỗi khối cơ sở có hai hành động cạnh nhau: **Lưu tất cả số sân** và **Thêm sân**. Thêm sân trực tiếp vào đúng cơ sở, đề xuất số lớn nhất hiện có cộng 1 (không lấp số trống). Khi số sân đang sửa chưa lưu, yêu cầu lưu trước để tránh tạo sai số. Backend vẫn kiểm tra số trùng và scope. Sân mới có trạng thái nháp cho tới khi cấu hình xong.
- Không hiển thị nút xác nhận riêng cho từng sân. Với cơ sở đã công bố, Owner lưu lịch mở cửa và giá hợp lệ cho sân nháp: backend kiểm quyền, version, QR dùng chung của cơ sở và công bố sân trong **cùng transaction**. Customer chỉ thấy/đặt sân sau commit. Cơ sở chưa được duyệt giữ sân nháp cho tới khi Admin duyệt cả cơ sở/doanh nghiệp. Sân legacy đang chờ duyệt riêng được Owner công bố bằng cách lưu lại lịch/giá hợp lệ; hồ sơ duyệt sân cũ được đánh dấu superseded, Admin không cần duyệt sân.
- Hồ sơ cá nhân Customer dùng cấu trúc tiêu đề và card thống nhất với Partner. Sau GET profile hoặc PUT tên thành công, phần tài khoản Customer hiển thị tên đã lưu và chữ tắt theo tên. Liên hệ đăng nhập vẫn chỉ đọc. Đăng xuất/xóa phiên xóa tên khỏi UI; F5 khôi phục tên qua GET có xác thực, không lưu token hay tên vào localStorage.

## Contract và lỗi

- `POST /api/v1/partner-onboarding/venues/{venueId}/courts` giữ body `{name:"Sân N"}` và response 201. Client chọn `N=max+1` theo tập sân của **cơ sở đó**. Backend `409 COURT_NUMBER_EXISTS` khi số bị chiếm, `404` sai owner scope, `409 STATE_CONFLICT` khi business/venue không cho thêm.
- `PUT /api/v1/partner-onboarding/courts/{courtId}/schedule` giữ body/If-Match và validation hiện có. Với venue `PUBLISHED`, schedule hợp lệ + QR `READY` trả status `ACTIVE`, version mới; QR thiếu trả `409 INCOMPLETE_PROFILE`, stale `412`, sai scope `404`. Với venue `DRAFT`, trả status `INACTIVE`. Endpoint xác nhận cũ chỉ giữ tương thích dữ liệu/API trước đây và không còn nút UI.
- `GET/PUT /api/v1/auth/profile` giữ nguyên. Client tải tên theo session có xác thực và cập nhật ngay sau PUT; request cũ không được ghi đè tên vừa lưu.

## Testcase

1. Cơ sở có sân 1, 3, 7: nút Thêm sân tạo sân 8 trong đúng venue; F5 còn sân 8. Sửa số chưa lưu thì nút Thêm không gửi request. Sân 1 của cơ sở khác không ảnh hưởng.
2. Sân mới ở cơ sở PUBLISHED không xuất hiện công khai khi thiếu lịch/giá; lưu lịch/giá hợp lệ tự thành ACTIVE và có slot/giá public. QR thiếu hoặc scope/version sai không công bố, không ghi lịch một phần; booking cũ không đổi.
3. Sân nháp thuộc cơ sở DRAFT vẫn chờ Admin duyệt; legacy pending court có thể công bố sau khi Owner lưu lại lịch/giá, approval cũ không còn chờ.
4. Customer/Partner profile desktop/mobile, Customer hiển thị tên trong vùng tài khoản sau lưu và F5; logout xóa tên; GET profile lỗi vẫn cho điều hướng với nhãn mặc định, không rò dữ liệu tài khoản trước.
