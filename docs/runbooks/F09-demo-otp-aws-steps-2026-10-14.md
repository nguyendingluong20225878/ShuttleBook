# ShuttleBook: lộ trình OTP thật, EC2 và S3 cho demo CV

Ngày 2026-10-14. **Hướng dẫn tự triển khai; chưa tạo AWS resource, chưa gửi OTP thật.** Mục tiêu gần nhất là demo với dữ liệu giả. Nếu nhận người đặt sân/chuyển khoản thật, áp dụng đầy đủ gate production F09 và diễn tập trên staging trước.

## 0. Kiểm tài khoản và giới hạn chi phí trước khi bấm Create

1. Trong AWS Console, mở **Billing and Cost Management → Free Tier**: ghi loại Free/Paid plan, credits còn lại, ngày hết hạn và dịch vụ nào đủ điều kiện trong **region định chọn**. Tài khoản AWS mới không đồng nghĩa EC2/S3/SMS/SES đều miễn phí; Free Plan hiện có thời hạn/credits và việc tiếp tục sau đó phụ thuộc nâng cấp. [AWS Free Tier plans](https://docs.aws.amazon.com/us_en/awsaccountbilling/latest/aboutv2/free-tier-plans.html).
2. Mở **Budgets → Create budget → Zero spend budget** và thêm cảnh báo số tiền nhỏ gửi email của bạn. Bật Free Tier usage alerts; đọc email xác nhận. Budget là cảnh báo, không cắt tài nguyên tức thì. [AWS Free Tier usage alerts](https://docs.aws.amazon.com/us_en/awsaccountbilling/latest/aboutv2/tracking-free-tier-usage.html).
3. Dùng AWS Pricing Calculator/estimate trên Console cho **EC2 instance + EBS volume/snapshot + public IPv4 + S3 storage/request/transfer + SES/SMS + domain**. Chọn một region gần người xem và có đủ dịch vụ; ảnh AWS Console Stockholm trước đây không phải quyết định region. Ghi ngân sách tối đa/tháng chấp nhận sau khi credit hết rồi mới tạo.

## 1. OTP email thật

**Hiện trạng code:** `SmtpContactVerificationDelivery` dùng Mailpit ở Development. Nó chưa có SMTP username/password, `EnableSsl=false`; vì vậy chỉ điền endpoint Amazon SES vào biến môi trường **không làm email thật chạy**. Cả đăng ký Customer/Partner dùng adapter này; OTP điện thoại đang gửi tới địa chỉ giả `@phone.shuttlebook.test`.

1. Chọn **một region SES**, xác minh email gửi hoặc tên miền trong **SES → Verified identities**. Nếu chưa có tên miền, có thể xác minh email cá nhân để thử, nhưng domain riêng + DNS SPF/DKIM/DMARC phù hợp hơn cho link CV.
2. Ở SES sandbox, xác minh cả địa chỉ người nhận thử; sandbox chỉ gửi tới recipient đã xác minh và có hạn mức. Sau test, muốn người lạ tự đăng ký bằng email phải xin **production access** cho region đó. [SES sandbox/production access](https://docs.aws.amazon.com/ses/latest/dg/request-production-access.html).
3. Tạo **SMTP credentials riêng** trong SES, không dùng root key hoặc AWS Console password. Credentials khác nhau theo region. Cất vào secret manager hoặc biến môi trường an toàn trên máy chủ, không commit/log/chụp ảnh. [SES SMTP credentials](https://docs.aws.amazon.com/en_en/ses/latest/dg/smtp-credentials.html).
4. Trước triển khai cần sửa adapter: dùng SMTP authentication + TLS (hoặc SES API qua IAM role), timeout và xử lý lỗi tạm thời; cấu hình `Identity__Delivery__From`, endpoint/port theo region; kiểm `register`, `resend`, expired/wrong OTP, rate limit và lỗi SMTP thật. Dùng một email thử mới, chỉ kiểm đã nhận thư và xác minh được, không in OTP vào log. [SES SMTP sending](https://docs.aws.amazon.com/ses/latest/dg/send-email-smtp.html).
5. Chỉ sau khi SES production access, domain/HTTPS và anti abuse đạt mới mở đăng ký email công khai. Tạm thời demo bằng tài khoản thử được cấp sẵn là lựa chọn rẻ/nhanh hơn khi SES chưa duyệt.

## 2. OTP số điện thoại thật

**Hiện trạng:** chưa có SMS adapter. Đừng bật đăng ký điện thoại công khai trên EC2 khi OTP vẫn tới Mailpit.

1. Quyết định giữ đăng ký điện thoại cho demo hay chỉ email. Nếu giữ, chọn Amazon SNS SMS hoặc AWS End User Messaging SMS theo khả năng gửi tới Việt Nam, kiểm giá theo nước/mạng và yêu cầu sender/origination đang có trong AWS Console trước khi gửi.
2. SNS SMS sandbox mới chỉ gửi tới số đã xác minh (tối đa 10 số); dùng số thử của chính bạn trước. Muốn gửi bất kỳ số nào cần hoàn tất thủ tục ra khỏi sandbox, quota/chi phí/định danh gửi theo AWS. [SNS SMS sandbox](https://docs.aws.amazon.com/sns/latest/dg/sns-sms-sandbox.html).
3. Thêm implementation `IContactVerificationDelivery` phân nhánh email và SMS, số chuẩn E.164, IAM quyền gửi tối thiểu, hạn mức chi tiêu SMS; test resend, sai/hết hạn, rate limit theo contact/IP, lỗi provider, không leak OTP. Không dùng email giả cho phone ngoài local.

## 3. EC2: từ staging dữ liệu giả đến link CV

1. Đóng gói API, Worker, PostgreSQL/PostGIS và reverse proxy; ba web là static bundle Vite. Đo RAM/CPU/disk thật trước khi chọn instance. Chọn AMI/instance có nhãn đủ điều kiện theo tài khoản và region; Free Tier/credits hiện không bảo đảm cấu hình bất kỳ đều miễn phí. [EC2 Free Tier](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/ec2-free-tier-usage.html).
2. Security Group: web `443` public sau khi có HTTPS, `80` chỉ để redirect/challenge; **không mở 5432, 5080, 8025, SSH cho cả Internet**. Quản trị bằng Session Manager nếu có thể; nếu dùng SSH, giới hạn nguồn. PostgreSQL chỉ trong máy/network riêng; mỗi service có user/quyền riêng.
3. Trước khi deploy, sửa blocker trong code: `AllowedHosts` đang chỉ `localhost;127.0.0.1`; các web build mặc định API `http://localhost:5080`; chưa có xử lý forwarded headers từ proxy; `DataProtection:KeysPath` hiện chỉ hỗ trợ Windows DPAPI, còn Linux không có nơi lưu khóa bền đã cấu hình. Chọn **một HTTPS origin** hoặc bộ subdomain cùng site, cấu hình CORS/cookie/CSRF và TLS đúng; thử F5/login/logout trên browser thật. Không dùng Development/Mailpit/secret `.env` local trên server.
4. Migrate DB **trước** khi mở traffic, theo release đã khóa version. Chạy API + Worker bằng systemd/container có restart policy, health `/health/live` và `/health/ready`, log giới hạn dung lượng, giám sát disk/RAM/outbox. Kiểm backup và phương án rollback ứng dụng/schema; không giả định EF migration có thể tự downgrade an toàn.
5. Chạy luồng Customer→Partner→Admin với dữ liệu giả trên staging: OTP test, thêm cơ sở/sân, giá theo từng sân, quote/booking, QR/proof S3, owner xác nhận, notification. Kiểm mobile, quyền sai scope, 10 triệu/một quote, một hold/account, thử chạy lại sau reboot. Chỉ đăng link CV sau khi HTTPS, email/tài khoản demo, chi phí, backup và cảnh báo hoạt động.

## 4. S3 media và backup ngoài EC2

1. Tạo bucket **media private** cùng region API. Bật **Block all public access**, encryption mặc định; không dùng S3 website/public bucket cho QR hay chứng từ. Cấu hình CORS chỉ cho origin browser thật và phương thức/header cần cho presigned PUT/GET. Dùng **EC2 instance role** có quyền tối thiểu cho prefix media, không lưu access key vào repo. [S3 access control](https://docs.aws.amazon.com/AmazonS3/latest/userguide/access-management.html), [EC2 IAM role](https://docs.aws.amazon.com/us_en/AWSEC2/latest/UserGuide/attach-iam-role.html).
2. Đặt `Media__Mode=S3`, `Media__S3Region`, `Media__S3Bucket` ở môi trường server, khởi động với credentials từ role. Code đã có đường presign/complete/view; vẫn phải **test thật** upload ảnh cơ sở, QR, chứng từ, checksum, object không thuộc scope, URL hết hạn, xóa quyền và lỗi mạng. Không xem unit/fixture là bằng chứng S3 live.
3. Tạo bucket/prefix **backup private riêng** với IAM tách quyền media; chạy `pg_dump` trên EC2 vào file mã hóa/giới hạn quyền, tải S3, đặt retention/lifecycle sau khi chốt chi phí. Đừng dùng chỉ EBS snapshot hay chỉ DB cùng EC2 làm backup. `scripts/Invoke-LocalBackupDrill.ps1` chứng minh diễn tập local; cần script Linux và **restore vào DB thử riêng từ object S3**, đối chiếu booking/payment/series/QR/proof trước khi coi PASS. [S3 versioning/security](https://docs.aws.amazon.com/AmazonS3/latest/userguide/security-best-practices.html).
4. Đối với demo cá nhân, mục tiêu tạm: backup mỗi ngày, giữ 7 ngày, RPO 24 giờ, RTO 4 giờ và email bạn nhận cảnh báo. Đây là **mục tiêu đề xuất chưa nghiệm thu**, không cam kết với người dùng thật.

## Điều kiện dừng và thứ tự đề xuất

Tiếp theo: (1) CI đúng SHA PASS; (2) chốt email-only hoặc demo account, ngân sách/region/domain; (3) sửa cấu hình Linux/HTTPS/SMTP; (4) staging EC2/S3 bằng dữ liệu giả với ngân sách cảnh báo; (5) restore drill và test link công khai; (6) mới đưa URL vào CV. **Không dùng người dùng hoặc thanh toán thật ở giai đoạn 1–5.**
