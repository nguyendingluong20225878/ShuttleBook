# Partner — trang chi tiết đơn riêng

Yêu cầu ngày 2026-10-09, sau người dùng xác nhận các lệnh typecheck/build/backend build/test:api/test:db và các ca nghiệm thu tay đều PASS. Thay đổi UI này nghiệm thu riêng; F08 vẫn DEFERRED.

## Phạm vi và acceptance

- Danh sách Partner chỉ ghi `Xem đơn` trên nút của mỗi thẻ; mã BK/SR vẫn ở nội dung thẻ.
- Bấm mở trang chi tiết riêng tại `#/bookings/{bookingId}`. Trang này không hiển thị danh sách, bộ lọc hay nút Xem thêm đơn bên trên.
- Trang chi tiết có `Quay lại danh sách đơn`; giữ bộ lọc và các trang danh sách đã tải trong phiên điều hướng cùng doanh nghiệp. Browser Back/Forward đúng danh sách/chi tiết, F5 khôi phục phiên M03 và mở đúng đơn.
- Đường dẫn cũ `#/bookings?bookingId={id}` được chuyển sang đường dẫn con bằng replaceState, không thêm bước Back. ID chỉ nhận UUID; không tạo đường dẫn ngoài hệ thống.
- Notification mở cùng trang chi tiết; giữ cơ chế chuyển doanh nghiệp đúng scope. Đổi doanh nghiệp từ chi tiết trở về danh sách mới, không giữ thông tin đơn cũ.
- Chi tiết vãng lai/cố định, biên lai và quyết định thanh toán vẫn dùng component/guard hiện hành. Có loading, lỗi và retry; focus tiêu đề khi mở trang riêng.

## Contract và dữ liệu

Không đổi API/schema/migration hoặc chính sách giữ sân/thanh toán. GET `/api/v1/operator/bookings/{id}` và các command F06/F07 giữ nguyên quyền, error contract và idempotency. Cookie/session M03 giữ nguyên. Danh sách/bộ lọc lưu memory hiện hành, không cam kết giữ qua F5.

## Testcase

1. Từ danh sách lọc, nút có đúng nhãn Xem đơn → URL con, chỉ chi tiết; quay lại giữ bộ lọc; Back/Forward đúng màn hình.
2. F5 URL chi tiết giữ phiên, không xuất hiện danh sách; link cũ chuẩn hóa URL.
3. Notification sang doanh nghiệp khác, đổi doanh nghiệp, pending owner, biên lai private, retry/conflict/deny của payment chạy hồi quy.
4. Cố định vẫn có toàn bộ buổi/tổng kỳ, confirm/review/reject tại anchor; pagination denial trên màn hình danh sách dọn dữ liệu private.
5. Desktop/mobile không tràn; typecheck/build và browser affected. Không chạy lại API/DB vì contract/backend không đổi; gate người dùng trước sửa ghi riêng.

## Kết quả và test tay bản mới

- PASS: `npm.cmd run typecheck`, `npm.cmd run build` (ba portal).
- PASS: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Web.ps1 tests/web/f06-partner.spec.ts tests/web/f07-partner-series.spec.ts tests/web/ui-partner-history.spec.ts tests/web/m03-browser-sessions.spec.ts --workers=2`: **74/74**, không skip/fail, 50,5s desktop/mobile. Fixtures UI; không dùng kết quả này thay cho DB integration. API/DB/live mới NOT RUN vì không đổi backend; live locator đã cập nhật, không gọi đó là live PASS mới.
- Review tuần tự, đã xem ảnh casual desktop và fixed mobile; không có review độc lập mới. Evidence ignored `.local/partner-booking-route/` và `test-results/`.
- Test tay: vào localhost:5174 → Đơn đặt sân; mỗi thẻ chỉ có nút **Xem đơn**. Mở cả đơn BK và SR: URL là `#/bookings/{UUID}`, chỉ có chi tiết, không danh sách/bộ lọc. Với SR vẫn có bảng các buổi và tổng kỳ. F5 còn phiên mở lại đúng đơn; Back/Forward và **Quay lại danh sách đơn** đúng màn hình. Chọn bộ lọc trước khi mở, quay lại vẫn giữ bộ lọc trong phiên cùng doanh nghiệp. Kiểm link từ Thông báo. Vite dev nhận code mới; nếu dùng preview phải restart preview từ build mới. Chưa có nghiệm thu tay của người dùng cho bản UI này.
