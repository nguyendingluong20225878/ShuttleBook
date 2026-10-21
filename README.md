# ShuttleBook

ShuttleBook là ứng dụng đặt sân gồm ba giao diện:

- **Customer**: tìm sân, đặt lịch vãng lai hoặc lịch cố định, gửi bằng chứng chuyển khoản.
- **Partner**: quản lý cơ sở, sân, lịch, giá và xác nhận thanh toán.
- **Admin**: duyệt hồ sơ cơ sở và theo dõi thông báo quản trị.

Hệ thống dùng React, TypeScript và Vite cho giao diện; ASP.NET Core cho API và Worker; PostgreSQL/PostGIS làm cơ sở dữ liệu.

## Yêu cầu trên máy

Cài các công cụ sau:

- Git
- Docker Desktop có Docker Compose
- Node.js 22.12 trở lên trong nhánh 22, hoặc Node.js 24
- Windows PowerShell

.NET SDK đúng phiên bản có thể được cài riêng cho dự án bằng script bên dưới.

## Clone và chuẩn bị

Mở PowerShell:

~~~powershell
git clone https://github.com/nguyendingluong20225878/ShuttleBook.git
cd ShuttleBook

powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Initialize-Local.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Install-DotNet.ps1
npm.cmd ci
~~~

Script khởi tạo tạo file **.env** riêng trên máy với secret ngẫu nhiên. File này đã bị Git bỏ qua.

Kiểm tra môi trường:

~~~powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Doctor.ps1
~~~

## Chạy ứng dụng local

Khởi động PostgreSQL/PostGIS và hộp thư thử Mailpit:

~~~powershell
npm.cmd run db:up
npm.cmd run db:migrate
~~~

Sau đó mở năm cửa sổ PowerShell riêng và chạy:

~~~powershell
npm.cmd run dev:api
npm.cmd run dev:worker
npm.cmd run dev:customer
npm.cmd run dev:partner
npm.cmd run dev:admin
~~~

Các địa chỉ local:

| Thành phần | Địa chỉ |
|---|---|
| Customer | http://localhost:5173 |
| Partner | http://localhost:5174 |
| Admin | http://localhost:5175 |
| API | http://localhost:5080 |
| Mailpit | http://localhost:8025 |

Khi đăng ký local, OTP email xuất hiện trong Mailpit. Không cần SES hoặc email thật.

### Tạo tài khoản Admin local

Chạy lệnh sau và nhập thông tin theo lời nhắc:

~~~powershell
npm.cmd run admin:bootstrap
~~~

Mật khẩu được nhập trực tiếp tại terminal và không được ghi vào repository.

## Kiểm tra trước khi gửi thay đổi

~~~powershell
npm.cmd run typecheck
npm.cmd run build
npm.cmd run test:api
npm.cmd run test:db
npm.cmd run test:web
~~~

Database integration test cần Docker đang chạy. Browser test cần cài Playwright browser một lần:

~~~powershell
npm.cmd run setup:browsers
~~~


## Dùng domain trên máy chủ

Cấu hình trong **deploy/compose.staging.yml** dành cho private staging qua SSH tunnel. Không mở trực tiếp cấu hình này ra Internet.

Trước khi dùng domain, chuẩn bị một máy chủ có IP ổn định và ba hostname, ví dụ:

- **app.example.com** cho Customer
- **partner.example.com** cho Partner
- **admin.example.com** cho Admin

### 1. DNS và firewall

Trỏ bản ghi DNS của ba hostname về IP máy chủ. Chỉ mở cổng 80 và 443 ra Internet. Không mở PostgreSQL 5432 hoặc API nội bộ 8080.

### 2. Cấu hình ứng dụng

Tạo **.env.staging** từ file mẫu:

~~~bash
cp deploy/staging.env.example .env.staging
chmod 600 .env.staging
~~~

Thay các origin localhost bằng domain HTTPS:

~~~text
AllowedHosts=app.example.com;partner.example.com;admin.example.com
Cors__AllowedOrigins__0=https://app.example.com
Cors__AllowedOrigins__1=https://partner.example.com
Cors__AllowedOrigins__2=https://admin.example.com
BrowserSession__CustomerOrigin=https://app.example.com
BrowserSession__PartnerOrigin=https://partner.example.com
AdminSession__AllowedOrigin=https://admin.example.com
~~~

Khi build Web, đặt ba API base URL thành đúng ba origin HTTPS. Mỗi portal gọi đường dẫn **/api** trên chính domain của nó; reverse proxy chuyển request đó vào service API.

### 3. Reverse proxy và HTTPS

Caddy hoặc reverse proxy tương đương cần ba virtual host. Mỗi host phục vụ SPA tương ứng và chuyển **/api/** vào **api:8080**.

Ví dụ rút gọn cho Customer:

~~~caddy
app.example.com {
    handle /api/* {
        reverse_proxy api:8080
    }
    handle {
        root * /srv/customer
        try_files {path} /index.html
        file_server
    }
}
~~~

Làm tương tự cho thư mục **/srv/partner** và **/srv/admin**. Caddy tự xin chứng chỉ khi DNS đúng, cổng 80/443 truy cập được và email ACME đã cấu hình.

Nên tạo một Compose override riêng trên máy chủ để:

- truyền ba domain vào build args của Web;
- publish cổng 80 và 443;
- mount Caddyfile dành cho domain;
- giữ certificate trong Docker volume.

Không commit override có hostname, ARN hoặc thông tin môi trường riêng. Khi đã có override, khởi động bằng cả hai file:

~~~bash
docker compose \
  --env-file .env.staging \
  -f deploy/compose.staging.yml \
  -f /đường-dẫn-riêng/compose.domain.yml \
  config --quiet

docker compose \
  --env-file .env.staging \
  -f deploy/compose.staging.yml \
  -f /đường-dẫn-riêng/compose.domain.yml \
  up -d --build --wait
~~~

### 4. Dịch vụ bên ngoài

- Giữ S3 bucket ở chế độ private và chỉ thêm ba HTTPS origin cần thiết vào CORS.
- Với Amazon SES, verify domain và DKIM trong đúng Region rồi xin production access trước khi gửi OTP tới email bất kỳ.
- Giới hạn API key bản đồ theo các hostname thật.
- Lưu backup database và Data Protection key ở nơi khác máy chủ.

Không dùng **docker compose down -v** trên máy có dữ liệu cần giữ vì lệnh đó xóa volume PostgreSQL và key ring.

## Cấu trúc chính

~~~text
apps/           Ba ứng dụng React
packages/ui/    Thành phần giao diện dùng chung
backend/src/    API, Worker, Migrator và Infrastructure
backend/tests/  Test API và PostgreSQL/PostGIS
tests/web/      Browser test
scripts/        Script setup, chạy và kiểm thử
deploy/         Dockerfile, Compose và cấu hình mẫu
~~~
