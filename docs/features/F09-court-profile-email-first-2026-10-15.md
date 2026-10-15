# Lát cắt 2026-10-15: sân Partner, hồ sơ Customer và OTP email

## Quyết định và phạm vi

- Partner chỉ có **Thêm sân** trong khối cơ sở. Số sân là nhãn chỉ đọc, tự tăng từ số lớn nhất của cơ sở. Không có nút lưu hàng loạt hay xóa sân. API đổi số hiện có giữ để tương thích, nhưng UI không gọi nữa; mọi booking tiếp tục trỏ court ID cũ.
- Customer `/me/profile` có bố cục rõ phần nhận diện, thông tin có thể sửa và liên hệ đăng nhập chỉ đọc. Không đổi API `GET/PUT /api/v1/auth/profile`, quyền hay dữ liệu.
- Bản triển khai ngoài Development nhận đăng ký/xác minh bằng **email**. Số điện thoại không được tạo OTP qua địa chỉ Mailpit giả: register/resend/verify phone trả `400 CONTACT_TYPE_UNAVAILABLE` trước khi tạo challenge. UI production không đưa lựa chọn phone trong đăng ký; Development vẫn dùng Mailpit cho cả hai để giữ dữ liệu thử.
- SMTP production lấy host, port, From, username, password và STARTTLS từ cấu hình tiến trình; không ghi credential vào repo. Thiếu TLS hoặc credential trong môi trường ngoài Development thì gửi mã trả `503 IDENTITY_DELIVERY_UNAVAILABLE` an toàn, không in mã/địa chỉ/secret. Mailpit local giữ chế độ không TLS. Chưa có SES identity, SMTP credential hay hộp thư thử thật nên chưa đánh dấu email delivery production PASS.

## Acceptance và testcase

1. Partner tại mỗi cơ sở chỉ thấy một nút **Thêm sân**; bấm khi cao nhất là sân 7 tạo sân 8 tại đúng cơ sở, F5 giữ nguyên; không có input sửa số, nút lưu hàng loạt hoặc xóa. API batch cũ không được gọi bởi UI.
2. Hồ sơ Customer tại desktop/mobile hiển thị tên hiện có, email/số điện thoại chỉ đọc, trạng thái tải/lỗi/lưu rõ; cập nhật tên và F5 vẫn đúng, tên trên avatar cập nhật. Tab và focus dùng được; không thêm dữ liệu cá nhân mới.
3. Development: Customer/Partner email và phone vẫn gửi vào Mailpit. Ngoài Development: phone register/resend/verify bị chặn trước service; email dùng SMTP STARTTLS/auth khi đủ cấu hình, thiếu cấu hình trả 503 mà không lộ bí mật.
4. Typecheck/build, API tests, browser fixtures và các bài PostGIS/live bị ảnh hưởng phải PASS trên bản code cuối. Hosted CI chỉ PASS khi run trên commit được push. Gửi thư bằng SES thật, S3/EC2/HTTPS, backup ngoài máy và nghiệm thu tay vẫn là gate riêng.

## Dữ liệu, API và rủi ro

Không migration. `business → venue → court`, giá và booking snapshot giữ nguyên. Đổi giao diện sân không xóa/đổi số sân đã tồn tại. Lỗi SMTP xảy ra sau khi challenge được lưu theo flow cũ; khách có thể yêu cầu gửi lại khi provider phục hồi, có rate limit. Trước public deploy cần thử SES sandbox/production access, danh tính gửi, hộp thư nhận và toàn bộ lỗi provider trên staging; không suy từ Mailpit hoặc fixture.
