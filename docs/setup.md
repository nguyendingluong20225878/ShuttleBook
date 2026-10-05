# Setup local — Windows PowerShell

## 1. Môi trường và phiên bản

Chạy các lệnh dưới đây tại **thư mục gốc repo**, ví dụ:

```powershell
Set-Location 'C:\Users\luong\Desktop\CLong'
```

- Git đã cài; repo có `origin/main`. Kiểm tra `git status --short --branch` trước khi thay đổi.
- Node 22.12+ trong dòng 22 hoặc Node 24; dùng `npm.cmd` trên PowerShell. Lockfile npm được lưu trong repo.
- .NET SDK 10.0.401 (`global.json`, cho phép bản vá trong cùng SDK band); script dùng SDK riêng `.tools/dotnet` nếu có.
- React 19.3.0, Vite 8.3.0, TypeScript 5.9.3, Playwright 1.63.0 được pin trong manifest/lockfile.
- Docker Desktop chạy Linux containers và Docker Compose v2 để chạy `postgis/postgis:17-3.5` local.

.NET 10 là LTS; .NET 8 hiện có trên máy không được dùng để build project mới này. Nguồn kiểm tra: [Microsoft support policy](https://dotnet.microsoft.com/en-us/platform/support/policy), [Vite requirements](https://vite.dev/guide/), [PostGIS image](https://github.com/postgis/docker-postgis). Phiên bản package đã đối chiếu npm/NuGet, không lấy từ ví dụ tài liệu cũ.

## 2. Khởi tạo cấu hình và SDK

```powershell
.\scripts\Initialize-Local.ps1
.\scripts\Install-DotNet.ps1
.\scripts\Doctor.ps1
```

`Initialize-Local.ps1` tạo `.env` có mật khẩu random, không in secret và không ghi đè file đã có. `Install-DotNet.ps1` tải SDK Microsoft, kiểm tra SHA-512, giải nén riêng vào `.tools`; không sửa PATH/máy toàn cục. Lượt triển khai đầu đã làm hai bước này; chạy lại sẽ giữ cấu hình và bỏ qua SDK có sẵn.

`.env` ở gốc repo giữ thông tin PostgreSQL, khóa identity, `ASPNETCORE_ENVIRONMENT`, `DOTNET_ENVIRONMENT`, `ASPNETCORE_URLS` và `VITE_API_BASE_URL`. Docker Compose và các script npm bên dưới tự nạp cấu hình này; không cần gõ từng lệnh `$env:...` trong terminal. Chỉ `.env.example` có placeholder được đưa vào Git.

Doctor trả exit code 1 khi còn thiếu công cụ. Nếu PowerShell chặn script, có thể chạy từng lệnh bằng `powershell -ExecutionPolicy Bypass -File .\scripts\Doctor.ps1`; tùy chọn chỉ áp dụng tiến trình đó, không đổi execution policy toàn máy.

## 3. Docker Desktop

Docker Desktop đã được cài trên máy phát triển. Nếu setup máy khác, cài theo [hướng dẫn chính thức Docker cho Windows](https://docs.docker.com/desktop/setup/install/windows-install/), chọn WSL2/Linux containers. Mở Docker Desktop và chờ Engine chạy.

Mở PowerShell mới, kiểm tra:

```powershell
docker version
docker compose version
.\scripts\Doctor.ps1
```

Kết quả cần có cả Client và Server; Compose v2 chạy được. Không cần cài PostgreSQL riêng trên Windows.

## 4. Restore và build

```powershell
npm.cmd ci
npm.cmd run typecheck
npm.cmd run build
.\scripts\dotnet.ps1 restore backend/ShuttleBook.slnx --locked-mode
.\scripts\dotnet.ps1 build backend/ShuttleBook.slnx --no-restore
```

`scripts/dotnet.ps1` chọn SDK local, đặt cache vào thư mục bị ignore và nạp `.env` vào connection string của tiến trình. Lệnh không in secret. Không chạy `dotnet` hệ thống SDK 8 thay cho wrapper nếu chưa cài SDK 10 toàn máy.

Kết quả: ba thư mục `apps/*/dist`, backend build không lỗi. Những artifact này không đưa vào Git.

## 5. Database và migration nền

```powershell
npm.cmd run db:up
npm.cmd run db:migrate
npm.cmd run db:migrate
```

PostgreSQL bind `127.0.0.1:54329`; named volume giữ dữ liệu khi restart. Migrator tạo extensions, EF migration history và các bảng identity của F01; lần hai không áp dụng lại migration đã có. Không tự migrate trong API/Worker. Chưa có bảng booking/sân.

Không dùng `docker compose down -v` khi cần giữ dữ liệu. Thay mật khẩu trong `.env` không tự thay mật khẩu của volume đã khởi tạo; cần cập nhật database có chủ đích, không xóa dữ liệu để xử lý cho nhanh.

## 6. Chạy các ứng dụng

Mỗi lệnh chạy lâu cần một terminal PowerShell riêng, cùng cwd gốc repo:

```powershell
# Terminal API (Development chỉ dùng local; nạp .env)
npm.cmd run dev:api
```

```powershell
# Terminal Worker: khung BackgroundService, chưa xử lý notification/expiry
npm.cmd run dev:worker
```

```powershell
# Một terminal cho mỗi cổng
npm.cmd run dev:customer
npm.cmd run dev:partner
npm.cmd run dev:admin
```

Mở `http://localhost:5173`, `http://localhost:5174`, `http://localhost:5175`. Cổng khách/đối tác có luồng identity; cổng admin có đăng nhập và thông báo chưa có module quản trị. Chưa có chức năng booking. CORS Development chỉ chấp nhận chính xác ba origin này; dùng 127.0.0.1 thay localhost là origin khác.

Trong môi trường local, OTP được gửi đến Mailpit tại `http://localhost:8025`, kể cả khi bạn nhập một địa chỉ email thật; Mailpit không chuyển tiếp thư ra ngoài. Hãy tìm thư theo địa chỉ vừa đăng ký. Phản hồi đăng ký `202` là thông báo chung để tránh lộ tài khoản tồn tại: nếu contact đã xác minh hoặc thuộc loại tài khoản khác, hệ thống không gửi mã mới. Dùng contact thử nghiệm mới khi kiểm tra luồng đăng ký.

```powershell
Invoke-RestMethod http://localhost:5080/health/live
Invoke-RestMethod http://localhost:5080/health/ready
```

Live trả Healthy nếu API còn phục vụ; Ready chỉ Healthy khi DB và baseline sẵn sàng. DB lỗi/thiếu migration thì Ready trả HTTP 503, không làm giả kết nối thành công.

### Khởi tạo và quản lý Admin local

Sau `npm.cmd run db:migrate`, người vận hành có quyền truy cập DB chạy tại **repo root trong PowerShell**:

```powershell
npm.cmd run admin:bootstrap
```

CLI hỏi loại contact, contact và password trong terminal; contact/password được nhập ẩn, không đặt trong lệnh, `.env`, log hoặc tài liệu. Contact phải do người vận hành kiểm soát; bootstrap chỉ tạo một `ADMIN/ACTIVE` đã xác minh và chạy lại không đổi tài khoản. Không chạy bootstrap trên database ngoài môi trường đã được phép. Không có endpoint đăng ký Admin công khai.

Khi cần thay mật khẩu, tạm khóa, mở lại hoặc thu hồi mọi phiên Admin hiện hành:

```powershell
npm.cmd run admin:rotate
npm.cmd run admin:suspend
npm.cmd run admin:activate
npm.cmd run admin:revoke-sessions
```

`rotate` và `suspend` thu hồi mọi phiên; `activate` chỉ cho phép đăng nhập phiên mới. Các lệnh thao tác trên database từ `ConnectionStrings__ShuttleBook` mà script local nạp; hãy xác nhận đúng môi trường trước khi chạy. Đăng nhập Admin ở `http://localhost:5175` sau khi API local hoạt động.

## 7. Kiểm thử

```powershell
npm.cmd run test:api
npm.cmd run test:db
```

API tests xác minh HTTP/error/CORS contract bằng test host; chúng không thay thế DB tests. DB tests cần PostgreSQL/PostGIS thật, tạo database test tên riêng rồi dọn đúng database đó; tài khoản cần quyền tạo database. Test helper chỉ chấp nhận host loopback và database điều khiển `postgres`; không trỏ `SHUTTLEBOOK_TEST_CONNECTION_STRING` tới server production.

```powershell
npm.cmd run setup:browsers
npm.cmd run build
npm.cmd run test:web
```

Script mở ba preview server ẩn và dừng đúng tiến trình do nó tạo sau khi kiểm thử desktop/mobile. Dừng các dev server trên 5173–5175 trước khi chạy; nếu cổng bận script báo lỗi và không dừng tiến trình khác. Browser đã tải trong lượt đầu thì không cần tải lại. Các ca live cần API/Mailpit thật có điều kiện và được ghi riêng trong testcase, không coi các ca mock là bằng chứng end-to-end.

Để chạy riêng browser E2E dùng API/PostgreSQL/Mailpit thật trên **database tạm local** (repo root, Docker đã healthy, các cổng 5080 và 5173–5175 trống):

```powershell
.\scripts\dotnet.ps1 build backend/ShuttleBook.slnx --no-restore
npm.cmd run test:identity-live
```

Script đọc `.env` local, ép PostgreSQL về `127.0.0.1`, kiểm tra tên database tạm qua EF trước khi migrate/dọn, build web với API `http://localhost:5080`, tạo Admin test bằng CLI và chạy 6 ca desktop/mobile. Password test được sinh trong tiến trình, không in ra console. Script dừng API/preview do nó tạo và xóa đúng database tạm sau khi kết thúc. Nếu phiên terminal bị ngắt đột ngột, có thể còn database với tiền tố `shuttlebook_f014_live_`; kiểm tra tên cụ thể trước khi dọn, không chạy lệnh xóa database theo tên mặc định.

CI `.github/workflows/ci.yml` có web/backend jobs; backend dùng service PostgreSQL/PostGIS thật. Workflow chưa được chạy trên GitHub vì chưa có remote/push; chỉ ghi CI pass sau khi có run thành công.

## 8. Lỗi thường gặp

| Dấu hiệu | Kiểm tra/xử lý |
|---|---|
| SDK 10 không tìm thấy | Chạy Install-DotNet, dùng wrapper; kiểm tra `global.json` |
| Docker không tìm thấy/không có Server | Cài/mở Docker Desktop, mở terminal mới, kiểm tra WSL2/Linux containers |
| Port 54329/5173–5175 đã dùng | Dừng đúng tiến trình của bạn hoặc thay config/cổng tương ứng; không kill process không rõ nguồn |
| Ready 503 | Kiểm tra DB đã healthy, `.env`, rồi chạy Migrator; xem lỗi nội bộ local đã redact |
| Restore bị chặn mạng | Kiểm tra truy cập npmjs.org/api.nuget.org hoặc proxy; không bỏ kiểm tra TLS |
| Playwright không thấy browser | Đặt cùng PLAYWRIGHT_BROWSERS_PATH lúc install và test |
| DB tests thiếu env/server | Dùng Test-Database.ps1 sau compose up; không skip rồi ghi pass |

## WSL

Workspace này đang dùng toolchain Windows. Nếu chuyển sang WSL, dùng checkout và toolchain Linux riêng, cài lại dependencies theo lockfile; không dùng chung node_modules hoặc gọi Windows dotnet.exe với dependency Linux. Không cần chuyển WSL để tiếp tục hướng dẫn này.
