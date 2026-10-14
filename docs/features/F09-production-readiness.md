# F09 — củng cố kỹ thuật và chuẩn bị vận hành

Trạng thái cập nhật 2026-10-10: **IN_PROGRESS local, production NO-GO**. Đây là roadmap giải thích F09; contract và testcase chi tiết của từng lát cắt nằm ở các file F09.1–F09.6, chưa phải kế hoạch đã duyệt để deploy production.

## Quyết định mới 2026-10-10 về đích triển khai

Người dùng chọn **production có người dùng thật** trên hướng S3 + AWS EC2; hiện **chưa có tên miền hoặc ngân sách AWS hàng tháng**. Quyết định này thay phần "S3 thật tiếp tục hoãn" trong phạm vi cũ: cần kiểm provider S3 thật và đưa vào kế hoạch release trước go-live. Nó không cấp quyền tạo tài nguyên AWS tính phí, chuyển dữ liệu thật hoặc deploy. Staging dùng dữ liệu giả là gate kỹ thuật trước production. [Runbook kiểm CI đến production](../runbooks/F09-check-and-aws-staging.md) liệt kê các blocker hiện đã thấy: HTTPS/forwarded headers/AllowedHosts, OTP email/SMS thật, S3 live, key/secrets Linux, đóng gói EC2, backup/alert và pilot. F09 tổng vẫn **NO-GO**.

Người dùng bổ sung: **chỉ cần hướng dẫn tự làm, không yêu cầu agent triển khai**; PostgreSQL/PostGIS chọn chạy **cùng EC2 giai đoạn đầu**. Chưa chốt giữ hay bỏ đăng ký bằng số điện thoại thật; runbook nêu hai nhánh, không tự đổi policy. Vì DB cùng một máy với ứng dụng, backup ngoài EC2 và restore drill là gate bắt buộc trước người dùng thật.

## Phạm vi sản phẩm hiện tại

- Customer, chủ sân, Admin. **F08 CANCELLED**: không tài khoản staff, mời/ủy quyền/phân quyền nhân viên; owner vẫn cần membership và business/venue scope backend.
- F01–F07 và M03 đã có gate local; không coi local PASS là production PASS. Không thêm trạng thái sau CONFIRMED, customer hủy/đổi lịch, payment gateway hoặc notification ngoài hệ thống.
- S3 thật nay thuộc đường tới production theo quyết định mới ở trên; báo cáo doanh thu vẫn là phần tùy chọn. Không tạo tài nguyên trả phí hoặc deploy khi chưa được phép.

## Các lát cắt dự kiến và acceptance

| Lát cắt | Công việc | Bằng chứng nghiệm thu cần có |
|---|---|---|
| F09.1 — risk và CI | Chốt/sửa số tiền casual vượt JS safe integer; policy quota quote toàn tài khoản tránh giữ nhiều sân; sửa workflow Ubuntu gọi powershell.exe; pipeline build/test tương thích runner | Test biên tiền không làm tròn; quota/expiry/concurrency trên PostGIS; GitHub Actions thực chạy PASS, không gọi local build là hosted CI PASS |
| F09.2 — độ bền dữ liệu | Sao lưu PostgreSQL/PostGIS và media Local, lưu ngoài vị trí vận hành, cấu hình retention; phục hồi vào DB/thư mục thử riêng, đối chiếu dữ liệu, viết runbook | Restore drill thật: booking/payment/series/evidence/allocation nhất quán, ảnh/QR/proof đọc được đúng quyền; không overwrite DB development/production để thử |
| F09.3 — giám sát | Kiểm health API/Worker/DB, log có traceId không secrets, lỗi outbox/retry và đơn chờ owner quá SLA; xác định người nhận cảnh báo và thao tác khắc phục | Điều khiển lỗi local để kiểm quan sát/phục hồi; retry không gửi trùng, không tự release đơn đã báo chuyển; SLA nghiệp vụ hiện có giữ nguyên |
| F09.4 — release và cấu hình | Tách cấu hình môi trường, HTTPS/CORS/cookie, secrets ngoài repo, quy trình migration và rollback release; hướng dẫn dựng môi trường thử | Triển khai thử có phép, health và luồng tích hợp đạt; kiểm nâng cấp/migration với dữ liệu, kế hoạch phục hồi có thể thực hiện; downgrade schema không tự giả định an toàn |
| F09.4b — S3 media production | Bucket private, IAM role, CORS đúng origin, presigned PUT/GET, checksum và migrate media cũ nếu cần | Thử bucket riêng với dữ liệu giả; QR/proof đúng scope, object không ký bị chặn, mất mạng/quyền xử lý đúng; kiểm lại trên staging trước go-live |
| F09.5 — chạy thử owner | Kiểm tải/concurrency/authorization, browser mobile và luồng Customer → Owner → Admin; thử tại cơ sở mẫu, ghi lỗi và tiêu chí đạt | PostGIS chống trùng dưới tải, thanh toán/idempotency/scope đạt; biên bản pilot và checklist trước public. Ngưỡng tải, thời gian khôi phục và pilot phải chốt theo nhu cầu thực tế |

## Phần tùy chọn sau đó

- **Báo cáo chủ sân:** tiền đã nhận theo cơ sở/khoảng ngày, payment PAID, exact amount, không cộng trùng payment cả kỳ cố định; chỉ owner đúng scope. Không coi báo cáo đã được yêu cầu ở phiên này.

## Cách thực hiện

Ưu tiên F09.1 → F09.2 → F09.3 → F09.4 → F09.5. Mỗi lát cắt phải có scope, contract/dữ liệu nếu có, testcase trước code và gate riêng; không làm tất cả schema/production trong một lượt. F08 không là dependency. Trước triển khai F09.1 chốt policy tiền/quota; trước môi trường ngoài local chốt hosting/domain/secrets và quyền tạo/deploy tài nguyên. F09 chỉ DONE với acceptance đã được duyệt và bằng chứng phù hợp, không có lỗi nghiêm trọng chưa xử lý.
