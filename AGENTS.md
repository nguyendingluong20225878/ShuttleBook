# ShuttleBook — hướng dẫn cho agent

## Bắt đầu và tiếp tục công việc

1. Đọc file này, `docs/process.md`, `docs/progress.md` và đặc tả/testcase của feature đang làm.
2. Khi bắt đầu dự án hoặc chưa có ngữ cảnh, đọc toàn bộ tài liệu nghiệp vụ `docs/README.md`, `docs/00-*.md` đến `docs/05-*.md`; `docs/hi.md` là ghi chú minh họa.
3. Kiểm tra Git, mã nguồn và bằng chứng kiểm thử trước khi sửa. Tiếp tục từ phần chưa hoàn thành; không làm lại phần đã đạt nếu không có bằng chứng cần sửa.
4. Yêu cầu mới của người dùng ưu tiên hơn tài liệu cũ. Ghi rõ giả định, mâu thuẫn và quyết định; chỉ hỏi vấn đề chặn bước hiện tại.

## Quy tắc nghiệp vụ không tự thay đổi

- Phân cấp `business → venue → court`; booking giữ một sân cụ thể.
- Owner cấu hình giá từng sân theo ngày/khung giờ. Tính tổng từng ca 30 phút và snapshot khi tạo booking.
- Nearby chỉ tìm theo vị trí/bán kính. Chọn sân rồi mới vào luồng đặt chung Vãng lai/Cố định.
- Vãng lai: các ca liên tiếp. Cố định: hàng tuần, mỗi buổi ít nhất 2 giờ, kỳ ít nhất 1 tháng; conflict thì rollback cả kỳ.
- QR do owner cung cấp; khách báo đã chuyển → chờ xác nhận → gửi thông báo owner → owner xác nhận: payment `PAID`, booking `CONFIRMED`.
- `CONFIRMED` kết thúc luồng thành công. Không thêm check-in/check-out/hoàn thành/vắng mặt. Vấn đề sau xác nhận xử lý trực tiếp tại sân.
- Customer không tự hủy/đổi lịch. Phải kiểm tra quyền và business/venue scope tại backend.
- PostgreSQL/PostGIS là nguồn dữ liệu chuẩn; allocation và exclusion constraint chống đặt trùng. Không thay thế bằng cache hoặc mock.
- Booking đã báo chuyển không tự giải phóng vì owner xác nhận chậm. Notification qua transactional outbox và xử lý retry/idempotency.

## Kiến trúc và cách triển khai

- Ba app React/TypeScript/Vite độc lập: customer, partner, admin. ASP.NET Core modular monolith API + Worker; PostgreSQL/PostGIS.
- Triển khai từng feature Database → API → UI → kiểm thử → review; không sinh toàn bộ schema/ứng dụng cùng lúc.
- Trước code phải có phạm vi, acceptance criteria, API/error contract, thiết kế dữ liệu và testcase.
- Không ghi secrets vào repo/log/hội thoại. `.env.example` chỉ chứa placeholder. Không sửa cấu hình máy toàn cục khi không cần.
- Không deploy production, tạo tài nguyên trả phí hoặc thay đổi dữ liệu thật nếu chưa được người dùng cho phép.
- Không tự commit/push/merge nếu chưa được yêu cầu; có thể init Git theo yêu cầu hiện tại. Không xóa hoặc ghi đè thay đổi của người dùng.

## Agent và chất lượng

Người dùng cho phép phân công các vai trò Planner, Technical Designer, Implementer, Independent Reviewer, QA. Chỉ giao subtask độc lập có ích; chia rõ file, thống nhất contract trước khi chạy song song. Root chịu trách nhiệm tích hợp; reviewer độc lập với implementer khi có thể.

- Kiểm thử theo rủi ro: business rule, validation, authorization/scope, concurrency, idempotency, migration. Integration transaction/constraint phải dùng PostgreSQL/PostGIS thật.
- Báo đúng PASS/FAIL/NOT RUN/BLOCKED cùng lệnh và lý do. Không dùng mock hoặc đọc source thay cho bằng chứng runtime.
- Sửa lỗi review, kiểm tra lại phần bị ảnh hưởng; không refactor ngoài phạm vi.
- Feature chỉ DONE khi đạt acceptance, kiểm thử cần thiết pass và không còn lỗi nghiêm trọng chưa xử lý.

## Hướng dẫn và bàn giao

Giải thích tiếng Việt: mục đích, lệnh, thư mục chạy, file chính, kết quả mong đợi và lỗi thường gặp. Phân biệt PowerShell/WSL; ưu tiên thực hiện trực tiếp phần đã đủ điều kiện. Cập nhật `docs/progress.md` sau mỗi mốc với phần hoàn thành, bằng chứng, điểm còn mở và bước tiếp theo. Không đánh dấu toàn bộ F00 xong khi chỉ mới scaffold.
