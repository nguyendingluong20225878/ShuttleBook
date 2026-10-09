# Prompt — cải thiện giao diện khách đặt sân trong F07

## Bối cảnh và phạm vi

Đọc AGENTS.md, process/progress, các contract F01/F04/F05/F06 trước khi sửa. Tham khảo checklist [UI UX Pro Max](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill): thiết kế theo nhiệm vụ, tương phản, semantic HTML, focus, responsive và trạng thái loading/error/empty. Áp dụng nền sáng, card phẳng, teal và font hệ thống hỗ trợ tiếng Việt để đồng nhất với partner; không dùng bố cục marketing, số liệu giả hoặc cài thêm dependency.

Phạm vi độc lập của phần này: shell dùng chung, đăng ký/xác minh/đăng nhập khách, trang tìm cơ sở và thông báo. Root sẽ tích hợp shell vào chi tiết cơ sở và booking/F07. Không thay policy của lịch cố định hoặc API trong phần UI này.

## Thiết kế và cấu trúc

- `components/CustomerShell.tsx`: header thương hiệu/SVG, navigation hiện tại, skip link tới nội dung và trạng thái mục đang mở. Header xếp lại trên điện thoại; không ẩn chức năng chính sau hover.
- `styles.css`: tokens teal #0F766E, dark #115E59, text #172B2A, muted #536665, nền #F3F6F6; control/touch target ít nhất 44px, focus rõ, reduced-motion, long text tự xuống dòng.
- Identity: form một cột có label rõ, autocomplete đúng loại, trạng thái gửi, feedback có thể focus; phần giới thiệu ngắn và bước đăng ký/xác minh. Phiên đã đăng nhập có đường dẫn tìm sân, đơn và thông báo.
- Search: nhóm tìm tên/địa chỉ và tìm quanh khu vực rõ ràng; card địa chỉ dài không tràn; empty/error có hành động thực tế; map chỉ là phần bổ trợ, geolocation bị từ chối vẫn tìm bằng tên được.
- Notifications: heading, số chưa đọc từ API, badge chữ đã/chưa đọc, thời điểm và hành động rõ; tải lại/xem thêm giữ nguyên cơ chế hiện có.
- Giữ nguyên bảng lịch 30 phút và kích thước cột cuộn ngang; không thu nhỏ cột để ép bảng vào viewport.

## Contract và giới hạn

Không đổi endpoint, DTO, auth/session/refresh, polling, private media, scope hay nghiệp vụ. Token chỉ trong memory; logout/F5 và guard tiếp tục hoạt động như hiện tại. Chỉ hiển thị counters API; không lưu dữ liệu riêng vào browser storage. Giữ nhãn/route hành động cũ để tránh phá luồng khách và browser regression. Không commit/push hoặc migrate DB.

## Acceptance và testcase

1. Header/navigation/skip link hoạt động bằng bàn phím, mục đang mở có `aria-current`; touch target ≥44px; mobile không overflow toàn trang.
2. Đăng ký → xác minh → đăng nhập → tìm sân, lỗi generic, refresh/logout và storage rỗng giữ nguyên; nhãn input/autocomplete phù hợp.
3. Search query gửi đúng API, tải lỗi có retry, empty state không bịa dữ liệu; địa chỉ/tên dài, geolocation bị từ chối và card CTA đọc được ở 375/768/1024/1440px.
4. Thông báo đếm/đọc/link hợp lệ/pagination/retry giữ nguyên; trạng thái có chữ, không chỉ dựa vào màu.
5. Typecheck/build customer, browser UI riêng và regression identity/F04/F06 chạy thực tế. Browser mock chỉ chứng minh UI, không thay bằng chứng API/PostGIS. Xem screenshot desktop/mobile; ghi PASS/FAIL/NOT RUN và lệnh, báo root các rủi ro tích hợp.

Thứ tự: prompt/AC → shell/form/search/notification/CSS → typecheck/build → browser và xem screenshot → sửa lỗi → bàn giao root.

## Mở rộng triển khai F07 — chính sách đã được duyệt

Người dùng đồng ý thu 100% cả kỳ qua một QR/một xác nhận, horizon 60 ngày địa phương/tối đa 12 buổi, quote 120 giây/holdMinutes sân. Đọc contract chính xác trong `docs/features/F07-designer-notes.md`.

- Trang chi tiết cơ sở có lựa chọn Vãng lai/Cố định; cả hai dùng cùng bảng tất cả sân × ca 30 phút. Cố định cần `max(120, minimumBookingMinutes)`; không ép bội block. Khách chưa đăng nhập có thể xem/chọn lịch, sau đó đăng nhập để tiếp tục review.
- `/series-review`: giữ court/venue/date/start/duration từ lịch; biểu mẫu một thứ trong tuần, giờ bắt đầu, thời lượng, đầu/cuối kỳ. Validate lưới 30 phút, tối thiểu hai giờ/court min, một tháng lịch, horizon; backend kiểm tra lịch/giá/tối đa buổi/concurrency.
- Quote authenticated trả từng ngày/từng ca, exact total, deadline, số buổi và conflict dates. Conflict không cho tạo và không tự bỏ buổi. Form đổi phải bỏ quote cũ; lấy quote mới và khách xác nhận lại.
- Create `{quoteId}` + idempotency key; retry cùng intent khi mất response, không tự tạo khi mount/F5. Thành công vào `/bookings/{anchorbookingId}` dùng lại transfer/private screenshot/F06. Một payment cho toàn kỳ; không tạo nút thanh toán theo từng buổi.
- List/detail dùng optional `Booking.series`; một group card có mã kỳ, kỳ, số buổi, tổng tiền. Chi tiết từng buổi có trạng thái và giá riêng; QR/số tiền thanh toán luôn tổng kỳ. Không thêm hủy/đổi/hoàn thành/check-in.
- Test UI: cùng grid và auth handoff; form calendar month/min/grid/horizon; conflict fourth week; exact total và breakdown; lost response idem retry; quote expired/changed; group detail/report{} và F5 memory guard; grouped list; old casual regression. Mock chỉ chứng minh UI, root chạy live/PostGIS để nghiệm thu nghiệp vụ.
