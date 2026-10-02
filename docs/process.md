# Quy trình phát triển ShuttleBook

## Nguồn thông tin

- `AGENTS.md`: chỉ dẫn agent, nghiệp vụ bắt buộc và giới hạn tự động hóa.
- `docs/README.md`, `00`–`05`: thiết kế nghiệp vụ/kỹ thuật. `hi.md` chỉ minh họa quan hệ.
- `docs/features/`: đặc tả, contract và acceptance của từng feature.
- `docs/testing/`: testcase và kết quả thực tế.
- `docs/progress.md`: trạng thái, quyết định, blocker và bước tiếp theo. File này khác `process.md` (quy trình).
- `docs/setup.md`: lệnh setup và cách kiểm tra local.

## Một vòng feature

1. **Planner:** đối chiếu tài liệu/code, ghi scope/non-goals, actor, happy path, lỗi/biên, các câu hỏi thực sự chặn feature.
2. **Designer:** chốt dữ liệu/migration, API request/response/error, quyền, transaction, UI states, acceptance và kế hoạch test trước khi code.
3. **Implementer:** hoàn thành một lát cắt database/API/UI tích hợp, thêm test theo rủi ro. Migration tăng dần theo feature, không dựng trước toàn schema.
4. **Reviewer + QA:** review độc lập và chạy testcase; có thể song song khi không sửa chung file. Ghi lỗi với cách tái hiện/bằng chứng.
5. **Sửa và xác minh:** implementer sửa, reviewer kiểm tra bản cuối; chạy lại các kiểm thử bị ảnh hưởng.
6. **Bàn giao:** đối chiếu acceptance, cập nhật tài liệu/tiến độ và hướng dẫn người dùng tự chạy. Commit/merge/push khi được yêu cầu.

Agent điều phối tổng hợp kết quả và quyết định trạng thái dựa trên bằng chứng. Không coi nhiều agent đồng ý là đủ để nghiệm thu. Khi không có sub-agent thì thực hiện tuần tự và ghi rõ review chưa độc lập.

## Nhánh và phân công

- Repository local khởi tạo ở `main`. Feature sau có thể dùng `feat/f01-identity`, `feat/f02-onboarding`… khi đã có baseline commit.
- Agent làm song song phải có phạm vi file tách biệt; nếu đã có commit nền, dùng worktree riêng khi cần. Không thay đổi contract giữa chừng mà không thông báo bên phụ thuộc.
- Giữ diff nhỏ, không sửa file người dùng đang làm ngoài phạm vi. Đọc lại Git trước bàn giao.

## Định nghĩa hoàn thành

- Tất cả acceptance criteria đạt; build và kiểm thử phù hợp đã chạy thành công.
- Không còn lỗi nghiêm trọng về quyền, dữ liệu, giao dịch hoặc luồng chính.
- Migration mới chạy trên DB mới và chạy lại an toàn; migration nâng cấp được kiểm tra khi có dữ liệu cũ.
- Giao diện được kiểm tra thực tế khi feature có UI; test transaction/constraint trên PostgreSQL/PostGIS thật.
- Secrets không vào repo; cấu hình, lệnh chạy và hạn chế được ghi rõ.
- Test `NOT RUN`/`BLOCKED` không tính là pass. Có thể hoàn thành một mốc con, nhưng feature tổng vẫn `IN_PROGRESS` nếu còn tiêu chí chưa đạt.

## Testcase và bằng chứng

Mỗi testcase có: ID, mục tiêu, tiền điều kiện, dữ liệu, các bước, kết quả mong đợi, loại test, kết quả thực tế.
Ghi ngày, môi trường, lệnh, PASS/FAIL/NOT RUN/BLOCKED và lý do. Không lưu token, connection string thật hoặc dữ liệu cá nhân vào log bằng chứng.

## Roadmap theo phụ thuộc

| Feature | Phạm vi | Phụ thuộc |
|---|---|---|
| F00 | Toolchain, khung 3 web/API/Worker, PostgreSQL/PostGIS, migration nền, health, build/test/CI, setup | Tài liệu hiện có |
| F01 | Đăng ký khách/chủ sân, xác minh contact, đăng nhập/refresh/logout, khởi tạo Admin an toàn | F00 |
| F02 | Business/venue/court nháp, owner membership, upload, Admin duyệt và publish | F01 |
| F03 | Giờ mở cửa, bảng giá từng sân/khung giờ, QR, maintenance | F02; contract giá/QR nháp thống nhất từ F02 |
| F04 | Danh sách, nearby, trang chi tiết và lịch ca | F02–F03 |
| F05 | Quote/booking vãng lai, chống trùng, idempotency, expiry, snapshot QR/giá | F03–F04 |
| F06 | Báo chuyển, outbox/in-app, owner xác nhận/từ chối/đối chiếu | F05 |
| F07 | Series, quote nhiều buổi, giữ toàn kỳ, thanh toán theo chính sách đã chốt | F05–F06 |
| F08 | Mời nhân viên và phân quyền cơ sở | F01–F03, tái kiểm tra F06 |
| F09 | Báo cáo/chức năng tùy chọn, backup/restore, giám sát, chuẩn bị production | Luồng chính đã nghiệm thu |

F02–F03 phải có owner hoàn tất hồ sơ và gửi duyệt trong luồng tích hợp. F05–F06 phải hoàn chỉnh trước khi nhận booking thật. Staff invitation thuộc phạm vi MVP dù triển khai sau owner flow. Không có feature check-in/check-out.

## Những điểm chưa chốt

| Vấn đề | Xử lý trước |
|---|---|
| Pending owner cần khai báo giá/QR nhưng guard hiện mô tả chỉ active membership | F02–F03: đặc tả guard nháp đúng chủ sở hữu; chưa tự cho pending owner vận hành booking |
| Hạn chuyển khoản, bằng chứng bắt buộc, booking horizon, giá quote thay đổi | F05 |
| Booking thiếu transition bổ sung evidence từ NEEDS_REVIEW; sequence từ chối còn gửi notification trực tiếp | F06: đồng bộ state machine và outbox trước code |
| SLA xác nhận; in-app đã bắt buộc, chỉ kênh email/SMS/Zalo còn tùy chọn | F06 |
| Thanh toán series từng buổi/tháng/cả kỳ, số occurrence tối đa và liên kết payment nhiều booking | F07 |
| Revision QR/tài khoản sau publish; QR hiện hành cho booking mới trong lúc chờ duyệt | F02–F03 |

Các điểm này không chặn F00. Không tạo trước schema/payment policy dựa trên giả định chưa được người dùng chốt.
