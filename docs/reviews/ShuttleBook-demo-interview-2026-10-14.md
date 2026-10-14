# Đánh giá ShuttleBook cho demo phỏng vấn — 14/10/2026

Phạm vi: nhánh `codex/f09-local-readiness`, ba cổng Customer/Partner/Admin, dữ liệu giả tại local. Đây là ước lượng mức sẵn sàng để trình diễn một sản phẩm cá nhân, không phải tỷ lệ hoàn thành code. Prompt có thể dùng lại: [ShuttleBook-demo-interview-evaluation.md](../prompts/ShuttleBook-demo-interview-evaluation.md).

## Kết luận theo mục tiêu

| Mục tiêu | Ước lượng | Kết luận và bằng chứng |
|---|---:|---|
| Demo local với dữ liệu giả để phỏng vấn | 80/100 | Có thể trình diễn có hướng dẫn sau khi kiểm tra thủ công dữ liệu demo. Ba cổng build/typecheck, API 96/96, PostGIS 150/150, browser fixture 332 PASS và live 8/8 PASS trên source nhánh đã push. Hosted CI commit `64f0258` PASS cả web/backend; không còn warning Node 20. |
| Link demo công khai trên AWS | 45/100 | NO-GO hiện tại: chưa có HTTPS/domain, email/SMS OTP thật, cấu hình Linux/reverse proxy, S3 thật, backup/restore ngoài EC2, alert hoặc thử deployment. Tài khoản AWS Free Tier chỉ chứng minh có thể bắt đầu setup. |
| Production với người dùng/giao dịch thật | Chưa chấm GO | NO-GO: ngoài các gate public demo còn cần quy trình hỗ trợ, bảo vệ dữ liệu, diễn tập phục hồi, theo dõi và kiểm định giao dịch thật. |

Điểm local theo rubric 100 điểm: luồng nghiệp vụ 27/30; backend/dữ liệu 22/25; UX/UI/accessibility 16/20; kiểm thử/CI 12/15; vận hành/deploy 3/10. Các điểm này là nhận định của reviewer, không phải chỉ số tự động. Giữ mức 80/100 dù CI đã PASS vì kiểm định tay trên dữ liệu demo và các gate vận hành vẫn chưa hoàn tất.

## Điểm mạnh có thể nói trong phỏng vấn

- Mô hình business → venue → court nhất quán; QR thuộc cơ sở, giá theo từng sân, booking chốt giá và sân cụ thể. Owner tự thêm sân trong cơ sở đã duyệt; Admin chỉ duyệt doanh nghiệp/cơ sở mới và thay đổi QR/tài khoản nhận tiền.
- PostgreSQL/PostGIS là nguồn dữ liệu, có ràng buộc tránh trùng giờ; các luồng đặt vãng lai và cố định cùng kiểm tổng tiền, xung đột và một quote hold/customer. Thanh toán là báo chuyển và Owner xác nhận, phù hợp phạm vi dự án.
- Ba cổng có UI thống nhất hơn, thông báo nhấn để đọc/đi tới việc cần làm, màn hình lịch/giá Partner và giá ở Customer có testcase desktop/mobile. OTP local có browser→API→Mailpit; không nhận Mailpit là email thật.
- CI và kiểm thử tự động đã được xây dựng; người dùng đã cung cấp ảnh run hosted của `64f0258` PASS cả web/backend, phần Annotations có 2 notices và 0 warning.

## Việc cần làm trước link CV, theo thứ tự

1. Lưu bằng chứng hiện có: PostGIS 150/150, live browser→API→Worker→PostGIS/Mailpit 8/8 và hosted CI `64f0258` PASS. Khi code thay đổi tiếp, chạy lại các gate bị ảnh hưởng trên commit ứng viên trước khi đưa link.
2. Tự kiểm trên dữ liệu giả ở ba vai trò: thêm sân 8 sau sân 7, một nút lưu mọi số sân, giá riêng sân 1/sân 2 theo ca/ngày đặc biệt, Customer thấy giá vừa lưu, Customer/Partner sửa tên và F5, Admin duyệt QR mới. Xem checklist ở `docs/testing/Partner-followup-manual.md` và `docs/testing/F09-ui-manual-checklist.md`.
3. Làm đường chạy Linux HTTPS thật cho ba frontend/API/Worker, `AllowedHosts`, forwarded headers, cookie/CORS, DataProtection và cấu hình qua secrets; giữ database chỉ nội bộ EC2.
4. Chọn đăng ký chỉ email để giảm phạm vi demo **hoặc** làm SMS OTP thật với giới hạn chi phí. Nâng cấp SMTP có TLS/auth, SES identity/sandbox, test một hộp thư thật; không dùng Mailpit khi public.
5. S3 private cho ảnh/QR/bằng chứng và bản backup ngoài EC2; test quyền, checksum, presign và restore vào DB thử, đặt budget/alert trước khi bật tài nguyên. Xem [runbook OTP/AWS](../runbooks/F09-demo-otp-aws-steps-2026-10-14.md).

## Kịch bản demo 5 phút

1. Partner tạo cơ sở/QR, thêm sân đánh số tăng dần, cấu hình giá riêng theo sân và lịch; Admin duyệt cơ sở. Nêu rõ tại sao thêm sân trong cơ sở đã duyệt không cần Admin.
2. Customer tìm cơ sở, xem giá đúng sân/ca, tạo đơn vãng lai hoặc cố định, nhận QR chung cơ sở và báo chuyển. Partner xem lịch và xác nhận tiền; đơn chuyển CONFIRMED.
3. Minh họa test PostGIS chống đặt trùng và snapshot giá của đơn cũ khi Partner sửa giá; mở GitHub Actions trên đúng SHA và trình bày một tình huống từng fail rồi được sửa.

## Quyết định của chủ dự án trước public demo

- Chốt mức chi AWS hàng tháng và cảnh báo; Free Tier/credit không phải cam kết chi phí bằng 0.
- Chọn chỉ email hay cả số điện thoại cho đăng ký công khai. Giữ số điện thoại thì cần SMS provider và ngân sách SMS.
- Chọn tên miền hoặc địa chỉ HTTPS ổn định, nơi/retention backup ngoài EC2, RPO/RTO và người nhận cảnh báo. Dùng dữ liệu giả cho CV cho đến khi gate production riêng đạt.

Không đề xuất thêm đặt hộ, thanh toán tự động hay dịch vụ bán thêm chỉ để nâng điểm phỏng vấn: chúng mở thêm bài toán tiền, audit và đối soát ngoài phạm vi sản phẩm hiện tại.
