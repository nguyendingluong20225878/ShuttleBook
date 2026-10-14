# F09 — phương án AWS cho demo CV, chưa triển khai

Ngày 2026-10-12. Người dùng muốn đăng dự án cá nhân làm nền tảng CV bằng tài khoản AWS Free Tier. Đây là **demo dùng dữ liệu giả** theo đề xuất mới, không phải nghiệm thu production có giao dịch thật. Chưa tạo tài nguyên AWS hoặc thay đổi cấu hình tài khoản.

## Quyết định đề xuất

| Mục | Phương án gọn để thử | Điều phải kiểm trước khi tạo |
|---|---|---|
| Ngân sách | Mục tiêu **0 USD trả thêm** khi còn Free plan/credit; xem plan, credit và ngày hết hạn ở Billing trước. Tạo cảnh báo chi phí nhỏ (ví dụ 1/5 USD) gửi email cá nhân, kiểm hằng tuần. | AWS Free plan hiện tối đa 6 tháng hoặc hết credits, tùy điều kiện đến trước; sau đó dịch vụ/dữ liệu có thể bị ngừng. Cảnh báo Budget không là giới hạn chi tiêu tức thời. [Free Tier FAQ](https://aws.amazon.com/free/free-tier-faqs/), [Budget practices](https://docs.aws.amazon.com/cost-management/latest/userguide/budgets-best-practices.html). |
| Region | Một region cho EC2, EBS và S3; so chi phí dự kiến với độ trễ người xem ở Việt Nam trước khi chọn. Ảnh Console từng ở Stockholm không chốt region triển khai. | Chọn instance/AMI có nhãn Free Tier eligible trong chính tài khoản và kiểm estimate gồm instance, EBS, public IP, S3, transfer. [EC2 Free Tier usage](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/ec2-free-tier-usage.html). |
| Compute | Một EC2 nhỏ cho API, Worker, PostgreSQL/PostGIS và reverse proxy; ba web có thể phục vụ tệp tĩnh cùng proxy trong demo đầu tiên. Tránh RDS, ALB và NAT Gateway khi chưa có nhu cầu. | Đo RAM/CPU/disk với các container thực tế, không giả định mọi instance Free Tier chạy nổi. DB chỉ nghe nội bộ; security group chỉ public 80/443, quản trị qua kênh hạn chế. |
| Domain/TLS | Chọn tên miền sau khi kiểm toàn bộ chi phí; HTTPS và origin/cookie/forwarded headers phải qua staging trước khi đăng link CV. | Domain không mặc định miễn phí; không mở auth/API công khai bằng HTTP. |
| OTP | Với demo công khai, chọn **đăng ký email thật** hoặc cấp sẵn tài khoản demo giả sau khi mailer phù hợp; Mailpit chỉ để local. Đề xuất tạm ẩn đăng ký số điện thoại cho demo vì hiện OTP phone chỉ đến địa chỉ giả Mailpit. | Trước khi có người dùng thật, cần delivery email an toàn hoặc SMS thật và kiểm abuse/rate limit. Không dùng OTP/mailbox thật trong ảnh CV. |
| Media | S3 bucket private cho ảnh/QR/proof nếu muốn thể hiện tích hợp S3; bật Block Public Access, chỉ cấp IAM tối thiểu cho instance role. Thử presigned PUT/GET bằng dữ liệu giả và quyền sai. | Giá S3 phụ thuộc lưu trữ, request và truyền dữ liệu; đối chiếu [S3 pricing](https://aws.amazon.com/s3/pricing/) và trang Free Tier trong tài khoản. |
| Backup | `pg_dump` + media lưu ngoài EC2, ví dụ bucket backup private riêng; diễn tập restore vào DB thử. **Mục tiêu đề xuất cho demo:** backup mỗi ngày, giữ 7 ngày, RPO 24 giờ, RTO 4 giờ; chỉ gọi đây là mục tiêu cho tới khi diễn tập đạt. | Snapshot EBS có thể bổ sung nhưng không thay `pg_dump` và thử restore. AWS không tự sao lưu EBS; [EBS snapshots](https://docs.aws.amazon.com/ebs/latest/userguide/ebs-snapshots.html). |
| Cảnh báo | Email cá nhân nhận báo chi phí/credit, health API, backup thất bại, disk cao và Worker/outbox bị kẹt. | Chỉ đánh dấu PASS khi thử làm lỗi có kiểm soát và nhận cảnh báo thật; không đưa secrets/PII vào log. |

## Thứ tự thực hiện và gate

1. Xác minh Billing: Free/Paid plan, credits, ngày hết hạn và cảnh báo; lập estimate cho một region. **Chưa tạo tài nguyên chỉ từ nhãn Free Tier trên Console Home.**
2. Chốt demo dùng dữ liệu giả, phương án email OTP/tài khoản demo, domain và mức chi phí chấp nhận sau khi credits hết.
3. Hoàn thành local U04, sửa OTP Customer, HTTPS reverse proxy/AllowedHosts/forwarded headers, secrets Linux, đóng gói EC2 và migration/rollback.
4. Tạo staging có phép: EC2 + PostGIS nội bộ, static web, bucket media/backup private; test Customer → Partner → Admin với dữ liệu giả, S3 presigned và restore drill. Theo dõi chi phí từ ngày đầu.
5. Chỉ đăng link CV sau khi HTTPS, demo account, backup, health/alert và giới hạn tài nguyên hoạt động. Nếu về sau nhận booking/chuyển khoản thật, quay lại toàn bộ gate production F09.

Các ngưỡng RPO/RTO/retention ở đây là **đề xuất cho demo**, chưa được người dùng chốt hoặc test. Trần 10 triệu VND của booking không phải ngân sách AWS.
