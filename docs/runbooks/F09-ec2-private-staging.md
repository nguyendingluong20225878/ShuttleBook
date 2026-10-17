# F09 — staging riêng trên EC2 (sau khi bật máy)

Ngày 2026-10-17. Chỉ dùng **dữ liệu giả**. Đây là gói thử qua SSH tunnel, chưa mở web cho Internet hoặc sử dụng người dùng thật. EC2 hiện Stop theo người dùng; phần dưới là các bước để người dùng tự triển khai sau khi xem mã và ngân sách.

## 1. Chuẩn bị trên EC2

Trong EC2 Instance Connect/Session Manager, dùng Amazon Linux 2023 x86_64. Clone đúng nhánh và SHA đã kiểm; không copy `.env` từ máy Windows lên máy chủ. Cài Docker Engine và Docker Compose plugin, cho user vận hành quyền Docker (quyền này tương đương root), mở phiên shell mới. Ví dụ cài Docker Engine trên AL2023 theo [AWS](https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/install-docker.html):

```bash
sudo dnf update -y
sudo dnf install -y docker git openssl
sudo systemctl enable --now docker
sudo usermod -aG docker ec2-user
```

Đăng xuất/đăng nhập lại để nhận group. Nếu `docker compose version` chưa chạy, cài Compose plugin theo [Docker](https://docs.docker.com/compose/install/linux/) rồi kiểm lại; đừng dùng lệnh `docker-compose` cũ. Chỉ cần outbound để tải image/NuGet/npm, SES và S3. Security Group tiếp tục không mở 5432/5080/5173–5175; SSH chỉ My IP nếu dùng tunnel. IAM instance role cần quyền media S3, không đặt AWS access key trong `.env.staging`. Kiểm **Metadata response hop limit = 2** trong EC2 → Instance → Actions → Instance settings → Modify instance metadata options để container lấy role qua IMDSv2; [AWS khuyến nghị 2 cho container](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/configuring-IMDS-new-instances.html). Giữ IMDSv2 required.

Từ thư mục gốc repo trên EC2:

```bash
cp deploy/staging.env.example .env.staging
chmod 600 .env.staging
```

Sửa `.env.staging` tại chỗ. Tạo `POSTGRES_PASSWORD` dạng random chữ/số và điền **cùng giá trị** vào `ConnectionStrings__ShuttleBook`. Tạo độc lập `Identity__JwtSigningKey`, `Identity__OtpPepper`, PFX export password. Điền bucket media private đã tạo, SES SMTP credentials Singapore và email sender Verified. Không đưa mật khẩu/OTP vào shell command, ảnh, log hay Git. Khi SES còn sandbox, chỉ dùng email recipient Verified. `VITE_MAPTILER_API_KEY` là khóa browser public; giới hạn referrer phù hợp cho `http://localhost:5173–5174` khi thử.

Tạo certificate cho khóa phiên ASP.NET; các file `.key/.crt` tạm và `.pfx` đều nằm dưới `deploy/secrets/` bị Git ignore:

```bash
mkdir -p deploy/secrets
chmod 700 deploy/secrets
umask 077
openssl req -x509 -newkey rsa:3072 -nodes -days 365 -subj '/CN=ShuttleBook Staging DataProtection' -keyout deploy/secrets/dp.key -out deploy/secrets/dp.crt
openssl pkcs12 -export -out deploy/secrets/dp.pfx -inkey deploy/secrets/dp.key -in deploy/secrets/dp.crt
rm deploy/secrets/dp.key deploy/secrets/dp.crt
sudo chown 1654:1654 deploy/secrets/dp.pfx
chmod 600 deploy/secrets/dp.pfx
```

`openssl pkcs12 -export` hỏi password tương tác: dùng đúng `DataProtection__CertificatePassword` đã điền. Giữ bản sao PFX và password ở nơi bảo mật ngoài EC2; mất chúng sẽ làm các session cookie hiện có không giải mã được sau khôi phục. Nếu subnet `172.30.54.0/24` trùng mạng hiện có, đổi subnet và IP web trong `deploy/compose.staging.yml` cùng `ReverseProxy__KnownProxy` trước khi chạy.

## 2. Kiểm cấu hình, build và chạy

Lệnh dưới đây chạy ở **root repo trên EC2**. `config --quiet` kiểm Compose parse; không dùng `config` không có `--quiet` vì có thể in secret. `up` chạy PostGIS → migrator → API/Worker → web, migrator phải exit 0 trước traffic.

```bash
docker compose --env-file .env.staging -f deploy/compose.staging.yml config --quiet
COMPOSE_PARALLEL_LIMIT=1 docker compose --env-file .env.staging -f deploy/compose.staging.yml build
docker compose --env-file .env.staging -f deploy/compose.staging.yml up -d --wait
docker compose --env-file .env.staging -f deploy/compose.staging.yml ps
curl -i http://127.0.0.1:5173/api/v1/no-such-route
```

Lệnh `curl` cuối kiểm proxy nối đến API; HTTP 404 JSON từ API là kết quả mong đợi. Kiểm health trực tiếp qua web bằng `curl -i http://127.0.0.1:5173/` (200) và API bằng `docker compose --env-file .env.staging -f deploy/compose.staging.yml exec api curl -fsS http://127.0.0.1:8080/health/ready` (200). Kiểm `ps`: `postgres`, `api`, `worker`, `web` running/healthy; `migrator` exited 0. Không dùng `docker compose down -v`, vì sẽ xóa dữ liệu DB và key ring.

Nếu DB mới cần Admin demo, sau migration chạy tương tác:

```bash
docker compose --env-file .env.staging -f deploy/compose.staging.yml --profile tools build admin-cli
docker compose --env-file .env.staging -f deploy/compose.staging.yml --profile tools run --rm admin-cli bootstrap
```

Nhập email Admin demo và mật khẩu tại prompt; không đặt trong lệnh. Nếu `up --wait` lỗi: `docker compose --env-file .env.staging -f deploy/compose.staging.yml ps` và `... logs --tail=80 api worker migrator web postgres`; không chia sẻ log thô nếu có PII. Khi cập nhật release: giữ backup DB/key/certificate, checkout SHA mới, `build`, rồi `up -d --wait`; migration một chiều, rollback ứng dụng không tự downgrade schema.

## 3. Thử trên browser từ máy Windows

Sau khi instance Running, dùng PowerShell **trên máy cá nhân** trong phiên còn key pair. Thay đường dẫn key và Public IPv4 của EC2 (IP có thể đổi sau Stop/Start); không gửi key vào hội thoại:

```powershell
ssh.exe -i 'C:\duong-dan-rieng\shuttlebook-demo-key.pem' -N -L 5173:127.0.0.1:5173 -L 5174:127.0.0.1:5174 -L 5175:127.0.0.1:5175 ec2-user@<PUBLIC_IPV4>
```

Giữ terminal SSH mở. Mở Customer `http://localhost:5173`, Partner `http://localhost:5174`, Admin `http://localhost:5175`. Nếu port local đang bị Vite chiếm, tắt Vite trước. Không mở browser bằng public IP: ba cổng chỉ bind loopback trên EC2.

Kiểm bằng dữ liệu giả: tạo Customer/Partner bằng recipient SES đã Verified, OTP email, login/logout/F5, Admin duyệt, tạo sân/giá, quote/booking, proof và QR S3, owner xác nhận, notification. Network tab phải gọi `/api` trên đúng `localhost:517x`; login Set-Cookie phải HttpOnly/SameSite/Secure phù hợp; thử origin lạ phải 403. Với bucket S3, CORS cho `http://localhost:5173` và `http://localhost:5174` cùng các method/header thực dùng cho presigned upload, không mở public bucket. Xác nhận object private và role truy cập đúng prefix. Tắt EC2 sau phiên thử để giữ credit, nhưng lưu ý EBS/snapshot/public IPv4 vẫn có thể phát sinh phí.

## 4. Còn thiếu trước public link

- Domain thật, TLS 443, cấu hình hostname/origin và cookie cho URL thật; không công khai cổng staging trên Internet.
- SES production access sau khi có domain/sender phù hợp để email bất kỳ nhận OTP; SMS vẫn tắt.
- Backup DB ra bucket khác + restore drill, giám sát Worker/outbox/disk, log, budget, quy trình rollback, browser test qua HTTPS. Không coi Compose build là nghiệm thu product.
